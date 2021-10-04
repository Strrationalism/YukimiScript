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
      "        ykmc dgml <INPUT_DIR> <OUTPUT_DGML_FILE> [OPTIONS...]"
      "    Create charset file:"
      "        ykmc charset <INPUT_DIR> <OUTPUT_CHARSET_FILE> [OPTIONS...]"
      ""
      "Options:"
      "    --lib <LIB_DIR>    Include external libraries."
      ""
      "Targets:"
      "    lua                Lua 5.1 for Lua Runtime 5.1 or LuaJIT (UTF-8)"
      ""
      "Example:"
      "    ykmc ./Example/main.ykm --target-lua ./main.lua --lib ./Example/lib/"
      "    ykmc dgml ./Example/scenario ./Example.dgml --lib ./Example/lib"
      "    ykmc charset ./Example/ ./ExampleCharset.txt --lib ./Example/lib"
      "" ]
    |> List.iter Console.WriteLine
   

type Options = 
    { Lib: string list }


let defaultOptions = 
    { Lib = [] }


type TargetOption =
    | Lua of outputFile: string


type CmdArg =
    | Dgml of inputDir: string * outputDgml: string * Options
    | Charset of inputDir: string * outputCharsetFile: string * Options
    | Compile of inputFile: string * TargetOption list * Options


let rec parseOptions prev =
    function
    | [] -> Ok prev
    | "--lib" :: libDir :: next ->
        parseOptions { prev with Lib = libDir :: prev.Lib } next
    | _ -> Error ()


let rec parseTargetsAndOptions =
    function
    | "--target-lua" :: luaOut :: next ->
        parseTargetsAndOptions next
        |> Result.map (fun (nextTargets, options) ->
            Lua luaOut :: nextTargets, options)
    | options -> 
        parseOptions defaultOptions options 
        |> Result.map (fun options -> [], options)


let parseArgs =
    function
    | "dgml" :: inputDir :: outputDgml :: options -> 
        parseOptions defaultOptions options
        |> Result.map (fun options -> 
            Dgml (inputDir, outputDgml, options))

    | "charset" :: inputDir :: charsetFile :: options ->
        parseOptions defaultOptions options
        |> Result.map (fun options -> 
            Charset (inputDir, charsetFile, options))

    | inputSrc :: targetsAndOptions ->
        parseTargetsAndOptions targetsAndOptions
        |> Result.map (fun (targets, options) ->
            Compile (inputSrc, targets, options))

    | _ -> Error ()


let doAction errStringing =
    function
    | Compile (inputFile, targets, options) -> 
        let dom =
            loadSrc 
                errStringing 
                (loadLibs errStringing options.Lib) 
                inputFile
            |> checkRepeat errStringing
            |> prepareCodegen errStringing

        targets
        |> List.iter (function
            | Lua output -> 
                let lua = YukimiScript.CodeGen.Lua.generateLua <| Intermediate.ofDom dom
                File.WriteAllText(output, lua, Text.Encoding.UTF8))
        
    | Dgml (inputDir, outDgml, options) -> 
        let lib = loadLibs errStringing options.Lib
        getYkmFiles inputDir
        |> Array.map (fun path -> 
            Path.GetRelativePath(inputDir, path), 
            loadSrc errStringing lib path |> checkRepeat errStringing)
        |> List.ofArray
        |> Diagram.analyze
        |> unwrapDomException errStringing
        |> Diagram.exportDgml
        |> fun dgml -> File.WriteAllText(outDgml, dgml, Text.Encoding.UTF8)

    | Charset (inputDir, outCharset, options) ->
        let lib = loadLibs errStringing options.Lib
        getYkmFiles inputDir
        |> Array.map (fun filePath ->
            loadSrc errStringing lib filePath
            |> checkRepeat errStringing
            |> prepareCodegen errStringing)
        |> Array.fold Dom.merge Dom.empty
        |> (fun x -> Seq.map (fun (_, block, _) -> block) x.Scenes)
        |> Seq.collect (Seq.map fst)
        |> Seq.collect (function
            | Elements.Operation.CommandCall c -> 
                c.UnnamedArgs
                |> Seq.collect (function
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
    argv
    |> Array.toList
    |> parseArgs
    |> function
        | Error () -> 
            help ()
            0
        | Ok x ->
            try
                doAction ErrorStringing.schinese x
                0
            with
            | FailException -> -1

