open System
open System.IO
open YukimiScript.Parser
open YukimiScript.Parser.Utils
open YukimiScript.Parser.Elements


let private help () =
    [ "YukimiScript Command Line Tool"
      "by Strrationalism Studio 2022"
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
      "    -L<LIB_SEARCH_DIR>  Add library searching dir for -l,"
      "                        you can even pass this argument by env variable "
      "                        \"YKM_LIB_PATH\" and split it by \':\'."
      "    -l<LIBNAME>         Import library from -L dirs,"
      "                        -lpymo means search \"libpymo.ykm\","
      "                        -l\"libpymo.ykm\" means search \"libpymo.ykm\"."
      ""
      "Diagram Types:"
      "    dgml               Visual Studio Directed Graph Markup Language."
      "    mermaid            Flowchart in Mermaid."
      ""
      "Targets:"
      "    bin                YukimiScript bytecode."
      "    lua                Lua 5.1 for Lua Runtime 5.1 or LuaJIT (UTF-8)"
      "    pymo               PyMO 1.2 script, you must compile with libpymo.ykm."
      "    json               Json."
      ""
      "Example:"
      "    ykmc ./Example/main.ykm --target-pymo ./main.lua -L../lib -lpymo"
      "    ykmc diagram dgml ./Example/scenario ./Example.dgml --lib ./Example/lib"
      "    ykmc charset ./Example/ ./ExampleCharset.txt --lib ./Example/lib"
      "" ]
    |> List.iter Console.WriteLine


exception FailException


let private unwrapResultExn (errorStringing: ErrorStringing.ErrorStringing) =
    function
    | Ok x -> x
    | Error err ->
        errorStringing err |> stderr.WriteLine
        raise FailException


type private Options = 
  { LibExactly: string list
    LibsToSearch: string list
    LibSearchDir: string list
    Debugging: bool }


let defaultLibSearchDirs =
    let e = System.Environment.GetEnvironmentVariable ("YKM_LIB_PATH")
    if String.IsNullOrWhiteSpace e then [] else
        e.Split ';' |> List.ofArray


let private defaultOptions = 
  { LibExactly = []
    LibsToSearch = []
    Debugging = false
    LibSearchDir = "." :: defaultLibSearchDirs }


type private TargetOption = 
    | Lua of outputFile: string
    | PyMO of outputFile: string * scriptName: string
    | Bytecode of outputFile: string
    | Json of outputFile: string


type private DiagramType =
    | Dgml
    | Mermaid


type private CmdArg =
    | Diagram of DiagramType * inputDir: string * output: string * Options
    | Charset of inputDir: string * outputCharsetFile: string * Options
    | Compile of inputFile: string * TargetOption list * Options


let rec private parseOptions prev =
    function
    | [] -> Ok prev
    | "--lib" :: libPath :: next -> 
        parseOptions { prev with LibExactly = libPath :: prev.LibExactly } next
    | x :: next when x = "-g" || x = "--debug" -> 
        parseOptions { prev with Debugging = true } next
    | x :: next when x.StartsWith "-L" -> 
        parseOptions
            { prev with LibSearchDir = x.[2..] :: prev.LibSearchDir } 
            next
    | x :: next when x.StartsWith "-l" ->
        parseOptions 
            { prev with LibsToSearch = x.[2..] :: prev.LibsToSearch } 
            next
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
    | "--target-json" :: json :: next ->
        parseTargetsAndOptions inputSrc next
        |> Result.map (fun (nextTargets, options) -> Json json :: nextTargets, options)
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

    let loadLibs options =
        CompilePipe.findLibs options.LibSearchDir options.LibsToSearch
        |> Result.map (List.append options.LibExactly)
        |> Result.bind CompilePipe.loadLibs
        |> unwrapResultExn ErrorStringing.schinese

    function
    | Compile (inputFile, targets, options) ->
        let libs = loadLibs options
        let intermediate = CompilePipe.compile libs inputFile |> unwrapResultExn ErrorStringing.schinese
        
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

                    File.WriteAllText(output, lua, Text.Encoding.UTF8)
                | Json out ->
                    YukimiScript.CodeGen.Json.genJson options.Debugging intermediate out)

    | Diagram (diagramType, inputDir, out, options) ->
        let diagramExporter =
            match diagramType with
            | Dgml -> Diagram.exportDgml
            | _ -> Diagram.exportMermaid

        let lib = loadLibs options

        CompilePipe.getYkmFiles inputDir
        |> Array.map
            (fun path ->
                //loadSrc errStringing lib path
                CompilePipe.loadDom path
                |> Result.map (Dom.merge lib)
                |> Result.bind CompilePipe.checkRepeat
                |> Result.map Dom.expandTextCommands
                |> Result.bind Dom.expandUserMacros
                |> Result.map (fun x -> 
                    Path.GetRelativePath(inputDir, path), x))
        |> List.ofArray
        |> Result.transposeList
        |> Result.bind Diagram.analyze
        |> unwrapResultExn errStringing
        |> diagramExporter
        |> fun diagram -> File.WriteAllText(out, diagram, Text.Encoding.UTF8)

    | Charset (inputDir, outCharset, options) ->
        let lib = loadLibs options

        CompilePipe.getYkmFiles inputDir
        |> Array.map (CompilePipe.compile lib)
        |> Array.toList
        |> Result.transposeList
        |> unwrapResultExn ErrorStringing.schinese
        |> Seq.collect (fun (Intermediate s) -> s)
        |> Seq.collect (fun x -> x.Block)
        |> Seq.collect (fun x -> x.Arguments)
        |> Seq.choose (function
            | String x -> Some x
            | _ -> None)
        |> Seq.concat
        |> Set.ofSeq
        |> Set.remove ' '
        |> Set.remove '\n'
        |> Set.remove '\r'
        |> Array.ofSeq
        |> fun x -> new String (x)
        |> fun x -> IO.File.WriteAllText(outCharset, x, Text.Encoding.UTF8)


[<EntryPoint>]
let main argv =
    let mutable ret = 0
    (*
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
    ret*)
    
    argv
    |> Array.toList
    |> parseArgs
    |> function
        | Error () ->
            help ()
        | Ok x ->
            try doAction ErrorStringing.schinese x
            with FailException -> ret <- -1
    ret
