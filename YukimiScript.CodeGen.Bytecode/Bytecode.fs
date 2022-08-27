module YukimiScript.CodeGen.Bytecode

open YukimiScript.Parser
open YukimiScript.Parser.Elements
open System.IO
open type System.Text.Encoding


let inline private getBytes'''<'t, 'x when (^t or ^x): (static member GetBytes: ^x -> byte[])> (x: 'x) =
        ((^t or ^x): (static member GetBytes: ^x -> byte[]) x)


let inline getBytesLE (x: 'x) =
    let bytes = getBytes'''<System.BitConverter, 'x> x
    if System.BitConverter.IsLittleEndian
    then bytes
    else Array.rev bytes


let private writeBytes (stream: Stream) byte =
    stream.Write (byte, 0, Array.length byte)


let generateBytecode genDebug (Intermediate scenes) (target: FileStream) =
    use cstrBlock = new MemoryStream ()
    use extrBlock = new MemoryStream ()

    let cstrIndex = ref Map.empty
    let extrIndex = ref Map.empty

    let getString (str: string) = 
        match Map.tryFind str cstrIndex.Value with
        | Some offset -> offset
        | None -> 
            cstrIndex.Value
            |> Map.tryPick (fun s offset ->
                if not <| s.EndsWith str then None else
                    UTF8.GetByteCount(s.[..s.Length - str.Length - 1])
                    |> uint32
                    |> (+) offset
                    |> Some)
            |> Option.defaultWith (fun () ->    
                let pos = uint32 cstrBlock.Position
                let strBytes = UTF8.GetBytes(str: string)
                writeBytes cstrBlock strBytes
                cstrBlock.WriteByte(0uy)
                cstrIndex.Value <- Map.add str pos cstrIndex.Value
                pos)

    let getExtern name = 
        match Map.tryFind name extrIndex.Value with
        | Some extrId -> extrId
        | None -> 
            let extrId = Map.count extrIndex.Value |> uint16
            
            getString name 
            |> uint32
            |> getBytesLE
            |> writeBytes extrBlock
            
            extrIndex.Value <- Map.add name extrId extrIndex.Value
            extrId

    let generateScene (scene: IntermediateScene) = 
        let code = new MemoryStream ()
        let debugInfo = if genDebug then Some <| new MemoryStream () else None

        let pSceneName = 
            getString (scene.Name: string)
            |> getBytesLE
        
        writeBytes code pSceneName
        debugInfo |> Option.iter (fun d -> 
            writeBytes d pSceneName
            scene.DebugInfo.File |> getString |> getBytesLE |> writeBytes d
            uint32 scene.DebugInfo.LineNumber |> getBytesLE  |> writeBytes d)

        let writeValue block =
            function
            | Integer i -> 
                    writeBytes block <| getBytesLE 0
                    writeBytes block <| getBytesLE i
            | Real f -> 
                writeBytes block <| getBytesLE 1
                writeBytes block <| getBytesLE (float32 f)
            | String s -> 
                getString s |> int |> (+) 2 |> getBytesLE |> writeBytes block
            | Symbol s -> 
                - int (getString s) - 1 |> getBytesLE |> writeBytes block

        for call in scene.Block do
            debugInfo |> Option.iter (fun d ->
                let rec countStackFrames =
                    function
                    | { Outter = None } -> 1
                    | { Outter = Some x } -> 1 + countStackFrames x

                let stackFrameDepth = countStackFrames call.DebugInfo
                uint32 code.Position |> getBytesLE |> writeBytes d
                uint32 stackFrameDepth |> getBytesLE |> writeBytes d

                let mutable dbg = call.DebugInfo
                for _ in 1 .. stackFrameDepth do
                    getString dbg.File |> getBytesLE |> writeBytes d
                    uint32 dbg.LineNumber |> getBytesLE |> writeBytes d

                    let isSceneFlag, scopeName =
                        match dbg.Scope with
                        | Some (Choice1Of2 a) -> 0u, a.Name
                        | Some (Choice2Of2 a) -> 0x80000000u, a.Name
                        | None -> 0x80000000u, ""
                    
                    getString scopeName |> getBytesLE |> writeBytes d
                    (uint32 dbg.MacroVars.Length) ||| isSceneFlag
                    |> getBytesLE |> writeBytes d
                    
                    for (name, var) in dbg.MacroVars do
                        getString name |> getBytesLE |> writeBytes d
                        writeValue d var
                    
                    if dbg.Outter.IsSome then
                        dbg <- dbg.Outter.Value
            )

            call.Callee 
            |> getExtern 
            |> getBytesLE
            |> writeBytes code

            call.Arguments.Length
            |> uint16
            |> getBytesLE
            |> writeBytes code

            for arg in call.Arguments do
                writeValue code arg

        code, debugInfo

    let writeFourCC fourCC (target: Stream) =
        assert (String.length fourCC = 4)
        fourCC |> ASCII.GetBytes |> writeBytes target

    let scenes, debugInfos = List.map generateScene scenes |> List.unzip
    let debugInfos = List.choose id debugInfos
    while (int cstrBlock.Length) % 4 <> 0 do
        cstrBlock.WriteByte 0uy

    writeFourCC "RIFF" target

    let riffDataSize = 
        4L +                        // FourCC 'YUKI'
        8L + cstrBlock.Length +     // Block 'CSTR'
        8L + extrBlock.Length +     // Block 'EXTR'
        8L * int64 scenes.Length + List.sumBy (fun (x: MemoryStream) -> x.Length) scenes +
        8L * int64 debugInfos.Length + List.sumBy (fun (x: MemoryStream) -> x.Length) debugInfos
        |> uint32

    riffDataSize |> getBytesLE |> writeBytes target
    writeFourCC "YUKI" target

    let writeRiffBlock fourCC (block: MemoryStream) =
        writeFourCC fourCC target
        block.Flush ()
        block.Length |> uint32 |> getBytesLE |> writeBytes target
        block.Position <- 0L
        block.CopyTo(target)
    
    writeRiffBlock "CSTR" cstrBlock
    writeRiffBlock "EXTR" extrBlock

    for scene in scenes do
        use scene = scene
        writeRiffBlock "SCEN" scene

    for dbg in debugInfos do
        use dbg = dbg
        writeRiffBlock "DBGS" dbg
        
    
