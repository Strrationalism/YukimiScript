open System
open System.IO
open YukimiScript.CommandLineTool.Compile
open YukimiScript.Parser


let private help () =
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
      "    --lib <LIBPATH>     Import external library(ies) from file or dir."
      "    --debug, -g         Enable debugging information."
      "    -L<LIB_SEARCH_DIR>  Add library searching dir for -l."
      "    -l<LIBNAME>         Import library from -L dirs,"
      "                        -lpymo means search \'libpymo.ykm\',"
      "                        -l\"libpymo.ykm\" means search \'libpymo.ykm\'."
      ""
      "Diagram Types:"
      "    dgml               Visual Studio Directed Graph Markup Language."
      "    mermaid            Flowchart in Mermaid."
      ""
      "Targets:"
      "    bin                YukimiScript bytecode."
      "    lua                Lua 5.1 for Lua Runtime 5.1 or LuaJIT (UTF-8)"
      "    pymo               PyMO 1.2 script, you must compile with libpymo.ykm."
      ""
      "Example:"
      "    ykmc ./Example/main.ykm --target-pymo ./main.lua -lpymo --lib ./Example/lib/"
      "    ykmc diagram dgml ./Example/scenario ./Example.dgml --lib ./Example/lib"
      "    ykmc charset ./Example/ ./ExampleCharset.txt --lib ./Example/lib"
      "" ]
    |> List.iter Console.WriteLine


type private Options = 
  { Lib: string list
    LibSearchDir: string list
    Debugging: bool }


let private defaultOptions = 
  { Lib = []
    Debugging = false
    LibSearchDir = ["."] }


type private TargetOption = 
    | Lua of outputFile: string
    | PyMO of outputFile: string * scriptName: string
    | Bytecode of outputFile: string


type private DiagramType =
    | Dgml
    | Mermaid


type private CmdArg =
    | Diagram of DiagramType * inputDir: string * output: string * Options
    | Charset of inputDir: string * outputCharsetFile: string * Options
    | Compile of inputFile: string * TargetOption list * Options


let private findLib opt (libName: string) =
    let libName =
        if libName.ToLower().EndsWith ".ykm"
        then libName
        else "lib" + libName + ".ykm"

    opt.LibSearchDir
    |> Seq.rev
    |> Seq.tryPick (fun dir ->
        let path = IO.Path.Combine (dir, libName)
        if File.Exists path || Directory.Exists path
        then Some path
        else None)


let rec private parseOptions prev =
    function
    | [] -> Ok prev
    | "--lib" :: libDir :: next -> 
        parseOptions { prev with Lib = libDir :: prev.Lib } next
    | x :: next when x = "-g" || x = "--debug" -> 
        parseOptions { prev with Debugging = true } next
    | x :: next when x.StartsWith "-L" -> 
        parseOptions { prev with LibSearchDir = x.[2..] :: next } next
    | x :: next when x.StartsWith "-l" ->
        match findLib prev x.[2..] with
        | None -> 
            Console.WriteLine("Can not find lib \"" + x + "\"."); exit -1
        | Some libPath -> 
            parseOptions { prev with Lib = libPath :: prev.Lib } next
    | _ -> Error()


let rec private parseTargetsAndOptions (inputSrc: string) =
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


let private parseDiagramType =
    function
    | "dgml" -> Ok Dgml
    | "mermaid" -> Ok Mermaid
    | _ -> Error()


let private parseArgs =
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


let private doAction errStringing =
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
                    YukimiScript.CodeGen.Bytecode.generateBytecode options.Debugging intermediate file
                    file.Close ()
                | PyMO (output, scriptName) -> 
                    YukimiScript.CodeGen.PyMO.generateScript options.Debugging intermediate scriptName
                    |> function
                        | Ok out -> File.WriteAllText(output, out, Text.Encoding.UTF8)
                        | Error () -> Console.WriteLine "Code generation failed."; exit (-1)

                | Lua output ->
                    let lua =
                        YukimiScript.CodeGen.Lua.generateLua options.Debugging intermediate

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
                    | Elements.Constant (Elements.String x) -> x
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
