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


let generateBytecode (Intermediate scenes) (target: FileStream) =
    let cstrBlock = new MemoryStream ()
    let extrBlock = new MemoryStream ()

    let cstrIndex = ref Map.empty
    let extrIndex = ref Map.empty

    let getString str = 
        match Map.tryFind str cstrIndex.Value with
        | Some offset -> offset
        | None -> 
            let pos = uint32 cstrBlock.Position
            let strBytes = UTF8.GetBytes(str: string)
            cstrBlock.Write(strBytes)
            cstrBlock.WriteByte(0uy)
            cstrIndex.Value <- Map.add str pos cstrIndex.Value
            pos

    let getExtern name = 
        match Map.tryFind name extrIndex.Value with
        | Some extrId -> extrId
        | None -> 
            let extrId = Map.count extrIndex.Value |> uint16
            
            getString name 
            |> uint16
            |> getBytesLE
            |> extrBlock.Write
            
            extrIndex.Value <- Map.add name extrId extrIndex.Value
            extrId

    let generateScene scene = 
        let code = new MemoryStream ()
        getString scene.Scene.Name
        |> getBytesLE
        |> code.Write

        for call in scene.Block do
            call.Callee 
            |> getExtern 
            |> getBytesLE
            |> code.Write

            call.Arguments.Length
            |> uint16
            |> getBytesLE
            |> code.Write

            for arg in call.Arguments do
                let typeid, data =
                    match arg with
                    | Integer i -> 0u, getBytesLE i
                    | Real f -> 1u, getBytesLE (float32 f)
                    | String s -> 2u, getString s |> getBytesLE
                    | Symbol s -> 3u, getString s |> getBytesLE

                code.Write (getBytesLE typeid)
                code.Write data

        code

    let writeFourCC fourCC (target: Stream) =
        assert (String.length fourCC = 4)
        fourCC |> ASCII.GetBytes |> target.Write

    let scenes = List.map generateScene scenes
    while cstrBlock.Length % 4L <> 0 do
        cstrBlock.WriteByte 0uy

    writeFourCC "RIFF" target

    let riffDataSize = 
        4L +                        // FourCC 'YUKI'
        8L + cstrBlock.Length +     // Block 'CSTR'
        8L + extrBlock.Length +     // Block 'EXTR'
        8L * int64 scenes.Length + List.sumBy (fun (x: MemoryStream) -> x.Length) scenes
        |> uint32

    riffDataSize |> getBytesLE |> target.Write
    writeFourCC "YUKI" target

    let writeRiffBlock fourCC (block: MemoryStream) =
        writeFourCC fourCC target
        block.Flush ()
        block.Length |> uint32 |> getBytesLE |> target.Write
        block.Position <- 0
        block.CopyTo(target)
    
    writeRiffBlock "CSTR" cstrBlock
    writeRiffBlock "EXTR" extrBlock
    for scene in scenes do
        writeRiffBlock "SCEN" scene
    