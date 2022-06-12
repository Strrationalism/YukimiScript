open System
open System.IO
open YukimiScript.CommandLineTool.Compile
open YukimiScript.Parser


let help () =
    [ "YukimiScript Command Line Tool"
      "by Strrationalism Studio 2021"
      ""
      "Usage:"
      "    Compile YukimiScript to Lua:"
      "        ykmc <INPUT_FILE> [--target-<TARGET> <OUTPUT_FILE>] [OPTIONS...]"
      "    Create diagram:"
      "        ykmc diagram <DIAGRAM_TYPE> <INPUT_DIR> <OUTPUT_FILE> [OPTIONS...]"
      "    Create charset file:"
      "        ykmc charset <INPUT_DIR> <OUTPUT_CHARSET_FILE> [OPTIONS...]"
      ""
      "Options:"
      "    --lib <LIB_DIR>    Include external libraries."
      ""
      "Diagram Types:"
      "    dgml               Visual Studio Directed Graph Markup Language."
      "    mermaid            Flowchart in Mermaid."
      ""
      "Targets:"
      "    bin                YukimiScript bytecode."
      "    lua                Lua 5.1 for Lua Runtime 5.1 or LuaJIT (UTF-8)"
      "    pymo               PyMO 1.2 script, you must compile source code with libpymo.ykm."
      ""
      "Example:"
      "    ykmc ./Example/main.ykm --target-lua ./main.lua --lib ./Example/lib/"
      "    ykmc diagram dgml ./Example/scenario ./Example.dgml --lib ./Example/lib"
      "    ykmc charset ./Example/ ./ExampleCharset.txt --lib ./Example/lib"
      "" ]
    |> List.iter Console.WriteLine


type Options = { Lib: string list }


let defaultOptions = { Lib = [] }


type TargetOption = 
    | Lua of outputFile: string
    | PyMO of outputFile: string * scriptName: string
    | Bytecode of outputFile: string


type DiagramType =
    | Dgml
    | Mermaid


type CmdArg =
    | Diagram of DiagramType * inputDir: string * output: string * Options
    | Charset of inputDir: string * outputCharsetFile: string * Options
    | Compile of inputFile: string * TargetOption list * Options


let rec parseOptions prev =
    function
    | [] -> Ok prev
    | "--lib" :: libDir :: next -> parseOptions { prev with Lib = libDir :: prev.Lib } next
    | _ -> Error()


let rec parseTargetsAndOptions (inputSrc: string) =
    function
    | "--target-bin" :: binOut :: next ->
        parseTargetsAndOptions inputSrc next
        |> Result.map (fun (nextTargets, options) ->
            Bytecode binOut :: nextTargets, options)
    | "--target-pymo" :: pymoOut :: next ->
        parseTargetsAndOptions inputSrc next
        |> Result.map (fun (nextTargets, options) -> 
            let scriptName = Path.GetFileNameWithoutExtension inputSrc
            PyMO (pymoOut, scriptName) :: nextTargets, options)
    | "--target-lua" :: luaOut :: next ->
        parseTargetsAndOptions inputSrc next
        |> Result.map (fun (nextTargets, options) -> Lua luaOut :: nextTargets, options)
    | options ->
        parseOptions defaultOptions options
        |> Result.map (fun options -> [], options)


let parseDiagramType =
    function
    | "dgml" -> Ok Dgml
    | "mermaid" -> Ok Mermaid
    | _ -> Error()


let parseArgs =
    function
    | "diagram" :: diagramType :: inputDir :: output :: options ->
        parseDiagramType diagramType
        |> Result.bind
            (fun diagramType ->
                parseOptions defaultOptions options
                |> Result.map (fun options -> Diagram(diagramType, inputDir, output, options)))

    | "charset" :: inputDir :: charsetFile :: options ->
        parseOptions defaultOptions options
        |> Result.map (fun options -> Charset(inputDir, charsetFile, options))

    | inputSrc :: targetsAndOptions ->
        parseTargetsAndOptions inputSrc targetsAndOptions
        |> Result.map (fun (targets, options) -> Compile(inputSrc, targets, options))

    | _ -> Error()


let doAction errStringing =
    function
    | Compile (inputFile, targets, options) ->
        let dom =
            loadSrc errStringing (loadLibs errStringing options.Lib) inputFile
            |> checkRepeat errStringing
            |> prepareCodegen errStringing

        let intermediate = Intermediate.ofDom dom

        targets
        |> List.iter
            (function
                | Bytecode output ->    
                    use file = File.Open (output, FileMode.Create)
                    YukimiScript.CodeGen.Bytecode.generateBytecode intermediate file
                    file.Close ()
                | PyMO (output, scriptName) -> 
                    YukimiScript.CodeGen.PyMO.generateScript false intermediate scriptName
                    |> function
                        | Ok out -> File.WriteAllText(output, out, Text.Encoding.UTF8)
                        | Error () -> Console.WriteLine "Code generation failed."; exit (-1)

                | Lua output ->
                    let lua =
                        YukimiScript.CodeGen.Lua.generateLua intermediate

                    File.WriteAllText(output, lua, Text.Encoding.UTF8))

    | Diagram (diagramType, inputDir, out, options) ->
        let diagramExporter =
            match diagramType with
            | Dgml -> Diagram.exportDgml
            | _ -> Diagram.exportMermaid

        let lib = loadLibs errStringing options.Lib

        getYkmFiles inputDir
        |> Array.map
            (fun path ->
                Path.GetRelativePath(inputDir, path),
                loadSrc errStringing lib path
                |> checkRepeat errStringing)
        |> List.ofArray
        |> Diagram.analyze
        |> unwrapDomException errStringing
        |> diagramExporter
        |> fun diagram -> File.WriteAllText(out, diagram, Text.Encoding.UTF8)

    | Charset (inputDir, outCharset, options) ->
        let lib = loadLibs errStringing options.Lib

        getYkmFiles inputDir
        |> Array.map
            (fun filePath ->
                loadSrc errStringing lib filePath
                |> checkRepeat errStringing
                |> prepareCodegen errStringing)
        |> Array.fold Dom.merge Dom.empty
        |> (fun x -> Seq.map (fun (_, block, _) -> block) x.Scenes)
        |> Seq.collect (Seq.map fst)
        |> Seq.collect
            (function
            | Elements.Operation.CommandCall c ->
                c.UnnamedArgs
                |> Seq.collect
                    (function
                    | Elements.Constant.String x -> x
                    | _ -> "")
            | _ -> Seq.empty)
        |> Set.ofSeq
        |> Set.remove ' '
        |> Array.ofSeq
        |> Array.map string
        |> fun x -> IO.File.WriteAllLines(outCharset, x, Text.Encoding.UTF8)


[<EntryPoint>]
let main argv =
    let mutable ret = 0
    let threadStart =
        Threading.ThreadStart (fun () -> 
            argv
            |> Array.toList
            |> parseArgs
            |> function
                | Error () ->
                    help ()
                | Ok x ->
                    try doAction ErrorStringing.schinese x
                    with FailException -> ret <- -1)
    let thread = Threading.Thread (threadStart, 1024 * 1024 * 16)
    thread.Start ()
    thread.Join ()
    ret
