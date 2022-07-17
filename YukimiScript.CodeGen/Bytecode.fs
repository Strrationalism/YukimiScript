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

    let getString str = 
        match Map.tryFind str cstrIndex.Value with
        | Some offset -> offset
        | None -> 
            let pos = uint32 cstrBlock.Position
            let strBytes = UTF8.GetBytes(str: string)
            writeBytes cstrBlock strBytes
            cstrBlock.WriteByte(0uy)
            cstrIndex.Value <- Map.add str pos cstrIndex.Value
            pos

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
            scene.DebugInformation.File |> getString |> getBytesLE |> writeBytes d
            uint32 scene.DebugInformation.LineNumber |> getBytesLE  |> writeBytes d)

        for call in scene.Block do
            debugInfo |> Option.iter (fun d ->
                uint32 code.Position |> getBytesLE |> writeBytes d
                call.DebugInformation.File |> getString |> getBytesLE |> writeBytes d
                uint32 call.DebugInformation.LineNumber |> getBytesLE |> writeBytes d)

            call.Callee 
            |> getExtern 
            |> getBytesLE
            |> writeBytes code

            call.Arguments.Length
            |> uint16
            |> getBytesLE
            |> writeBytes code

            for arg in call.Arguments do
                match arg with
                | Integer i -> 
                    writeBytes code <| getBytesLE 0
                    writeBytes code <| getBytesLE i
                | Real f -> 
                    writeBytes code <| getBytesLE 1
                    writeBytes code <| getBytesLE (float32 f)
                | String s -> 
                    getString s |> int |> (+) 2 |> getBytesLE |> writeBytes code
                | Symbol s -> 
                    - int (getString s) - 1 |> getBytesLE |> writeBytes code

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
        
    
    
