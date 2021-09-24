open System
open System.IO
open YukimiScript.Parser
open YukimiScript.Parser.ParserMonad
open YukimiScript.CommandLineTool


let private help () =
    [ "Yukimi Script Command Tool"
      "by Strrationalism Studio 2021"
      ""
      "Usage: ykmc <path-to-scripts> [options]"
      ""
      "Options:"
      "    --lib <libDir>         Add other library."
      "    --dgml <output>        Create the diagram."
      "    --target-lua <output>  Compile to lua source code."
      "    --charset <charset>    Generate charset file in UTF-8 text."
      ""
      "Examples:"
      "    Check the scripts:"
      "        ykmc \"./Example\" --lib \"./api\""
      "    Create the diagram from scripts:"
      "        ykmc \"./Example\" --lib \"./api\" --dgml \"./diagram.dgml\""
      "    Compiles to Lua source code:"
      "        ykmc \"./Example\" --lib \"./api\" --target-lua \"script.lua\""
      "    Create charset file:"
      "        ykmc \"./Example\" --charset \"./charset.txt\""
      "" ]
    |> Seq.iter Console.WriteLine


type Option =
    { ScriptDir: string
      VoiceDocumentOutputDir: string option
      DiagramOutputFile: string option
      LibraryDirs: string list
      TargetLua: string option
      Charset: string option }


exception private OptionErrorException


let defaultOption scriptDir =
    { ScriptDir = scriptDir
      VoiceDocumentOutputDir = None
      DiagramOutputFile = None
      LibraryDirs = [] 
      TargetLua = None
      Charset = None }
    

let rec parseOption prev =
    function
    | [] -> Ok prev
    | "--lib" :: lib :: other ->
        parseOption
            { prev with LibraryDirs = lib :: prev.LibraryDirs}
            other

    | "--dgml" :: dgml :: other ->
        if prev.DiagramOutputFile.IsSome then
            failwith "--dgml is already given."
        parseOption { prev with DiagramOutputFile = Some dgml } other

    | "--target-lua" :: lua :: other ->
        if prev.TargetLua.IsSome then
            failwith "--target-lua is already given."
        parseOption { prev with  TargetLua = Some lua } other

    | "--charset" :: charset :: other ->
        if prev.Charset.IsSome then
            failwith "--charset is already given."
        parseOption { prev with Charset = Some charset } other

    | _ -> Error ()


[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> function 
        | scriptDir :: other ->
            parseOption 
                (defaultOption scriptDir)
                other
        | _ -> Error ()
    |> function
        | Error _ -> 
            help ()
            0
        | Ok option ->
            try
                if Directory.Exists option.ScriptDir |> not then
                    raise <| DirectoryNotFoundException(option.ScriptDir + " not found!")

                let project = Project.openProject option.ScriptDir
                let libs =
                    option.LibraryDirs
                    |> Seq.collect (fun libDir ->
                        Directory.EnumerateFiles(
                            libDir, 
                            "*.ykm", 
                            SearchOption.AllDirectories))

                let loadDoms (files: string seq) : (string * Dom.Dom) [] =
                    files 
                    |> Array.ofSeq
                    |> Array.Parallel.map (fun path ->
                        File.ReadAllLines(path)
                        |> Parser.parseLines
                        |> function
                            | Ok x -> x
                            | Error es ->
                                es
                                |> List.iter (fun (i, x) ->
                                    ErrorProcessing.printLExn path i x)
                                raise FailException
                        |> Dom.analyze path
                        |> function
                            | Ok x -> path, x
                            | Error e ->
                                ErrorProcessing.printExn path e
                                raise FailException)

                let checkRepeat (dom: Dom.Dom) =
                    dom.Scenes 
                    |> Seq.countBy (fun (x, _, _) -> x.Name)
                    |> Seq.tryFind (snd >> (<>) 1)
                    |> function
                        | Some (x, _) -> Error <| Dom.SceneRepeatException x
                        | None ->
                            dom.Externs
                            |> Seq.countBy (fun (Elements.ExternCommand (x, _), _) -> x)
                            |> Seq.tryFind (snd >> (<>) 1)
                            |> function
                                | Some (x, _) -> Error <| Dom.ExternRepeatException x
                                | None ->
                                    dom.Macros
                                    |> Seq.countBy (fun (x, _, _) -> x.Name)
                                    |> Seq.tryFind (snd >> (<>) 1)
                                    |> function
                                        | Some (x, _) -> Error <| Dom.MacroRepeatException x
                                        | None -> Ok dom

                let lib = 
                    Seq.append project.Library libs
                    |> loadDoms
                    |> Array.map snd
                    |> Array.fold Dom.merge Dom.empty
                    |> checkRepeat
                    |> function
                        | Error x -> 
                            ErrorProcessing.printExn "" x
                            raise FailException
                        | Ok x -> x

                if List.isEmpty lib.Scenes |> not then
                    lib.Scenes
                    |> List.iter (fun (_, _, debug) ->
                        ErrorProcessing.printLExn 
                            debug.File
                            debug.LineNumber
                            Dom.CannotDefineSceneInLibException)
                    raise FailException

                let expandTextAndUserMacros x =
                    let result = 
                        x
                        |> Array.Parallel.map (fun (fileName, dom) ->
                            dom
                            |> checkRepeat
                            |> Result.map Dom.expandTextCommands 
                            |> Result.bind (Dom.expandUserMacros lib)
                            |> Result.mapError (fun x -> fileName, x)
                            |> Result.map (fun x -> fileName, x))
                    
                    let errors =
                        result
                        |> Array.choose (function
                            | Error (fileName, e) -> Some (fileName, e)
                            | _ -> None)
                    
                    if Array.isEmpty errors |> not then
                        errors
                        |> Array.iter (fun (fileName, e) -> 
                            ErrorProcessing.printExn fileName e)
                        raise FailException

                    result 
                    |> Array.map (function 
                        | Ok x -> x 
                        | _ -> failwith "Internal Error")

                let scenario = 
                    loadDoms project.Scenario
                    |> expandTextAndUserMacros

                match option.DiagramOutputFile with
                | None -> ()
                | Some output ->
                    scenario
                    |> List.ofArray
                    |> List.map (fun (fileName, x) ->
                        let dir = Path.Combine(option.ScriptDir, "scenario")
                        Path.GetRelativePath(dir, fileName), x)
                    |> Diagram.analyze
                    |> function
                        | Error e -> 
                            ErrorProcessing.printExn "" e
                            raise FailException
                        | Ok diagram ->
                            let dgml = Diagram.exportDgml diagram
                            File.WriteAllText(output, dgml)

                let program = 
                    loadDoms project.Program
                    |> expandTextAndUserMacros                 

                seq {
                    lib
                    yield! Seq.map snd scenario
                    yield! Seq.map snd program
                }
                |> Seq.fold Dom.merge Dom.empty
                |> checkRepeat
                |> Result.map Dom.expandSystemMacros
                |> Result.bind Dom.linkToExternCommands
                |> function
                    | Error x -> 
                        ErrorProcessing.printExn "" x
                        raise FailException
                    | Ok finalDom ->
                        if option.TargetLua.IsSome then
                            let target = option.TargetLua.Value
                            let funcName = Path.GetFileNameWithoutExtension target
                            let lua = YukimiScript.CodeGen.Lua.generateLua funcName finalDom
                            IO.File.WriteAllText(target, lua)

                        if option.Charset.IsSome then
                            seq { 
                                for (_, block, _) in finalDom.Scenes -> 
                                    seq {
                                        for (command, _) in block ->
                                            match command with
                                            | Elements.Operation.EmptyLine _ -> None
                                            |  Elements.Operation.CommandCall c ->
                                                if c.NamedArgs.Length <> 0 then
                                                    failwith "This construction is not supported."
                                                c.UnnamedArgs
                                                |> List.choose (function
                                                    | Elements.Constant.String x -> Some x
                                                    | _ -> None)
                                                |> Some
                                            | _ -> failwith "This construction is not supported."
                                    }
                            }
                            |> Seq.concat
                            |> Seq.choose id
                            |> Seq.concat
                            |> Seq.concat
                            |> Set.ofSeq
                            |> Seq.except [ ' '; '\t' ]
                            |> Seq.map string
                            |> fun charset ->
                                File.WriteAllLines (option.Charset.Value, charset)
                                
                0
                
            with 
            | :? FailException -> -1
            | :? AggregateException as e ->
                match e.InnerException with
                | :? FailException -> ()
                | e -> 
                    Console.WriteLine("Error:" + e.Message)
                -1
            | e ->
                Console.WriteLine("Error:" + e.Message)
                -1
