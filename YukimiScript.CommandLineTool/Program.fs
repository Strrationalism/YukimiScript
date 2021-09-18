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
      ""
      "Examples:"
      "    Check the scripts:"
      "        ykmc \"./Example\" --lib \"./api\""
      "    Create the diagram from scripts:"
      "        ykmc \"./Example\" --lib \"./api\" --dgml \"./diagram.dgml\""
      "    Compiles to Lua source code:"
      "        ykmc \"./Example\" -lib \"./api\" --target-lua \"script.lua\""
      "" ]
    |> Seq.iter Console.WriteLine


type Option =
    { ScriptDir: string
      VoiceDocumentOutputDir: string option
      DiagramOutputFile: string option
      LibraryDirs: string list
      TargetLua: string option }


exception private OptionErrorException


let private optionParser =
    let arg = 
        parser {
            do! Basics.whitespace1
            return! Constants.stringParser
        }
        |> name "command line arg"

    let rec options cur = 
        arg
        |> bind (function
            | "--target-lua" ->
                arg
                |> bind (fun lua ->
                    { cur with TargetLua = Some lua }
                    |> options)
            | "--lib" ->
                arg
                |> bind (fun lib ->
                    { cur with LibraryDirs = lib :: cur.LibraryDirs } 
                    |> options)
            | "--dgml" ->
                if cur.DiagramOutputFile.IsSome then
                    raise OptionErrorException
                else 
                    arg
                    |> bind (fun dgml ->
                        { cur with DiagramOutputFile = Some dgml }
                        |> options)
            | _ -> raise OptionErrorException)
        |> zeroOrOne
        |> map (Option.defaultValue cur)

    parser {
        let! scriptDir = arg
        return! options  { 
            ScriptDir = scriptDir
            VoiceDocumentOutputDir = None
            DiagramOutputFile = None
            LibraryDirs = [] 
            TargetLua = None
        }
    }
    

[<EntryPoint>]
let main argv =
    argv
    |> Array.map (fun x -> 
        match x.Trim() with
        | x when x.StartsWith "\"" && x.EndsWith "\"" ->
            x
        | x when x.StartsWith "\"" -> x + "\""
        | x when x.EndsWith "\"" -> "\"" + x
        | x -> "\"" + x + "\"")
    |> Array.fold (fun a b -> a + " " + b) ""
    |> fun x -> " " + x.Trim ()
    |> fun argv -> run argv optionParser
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
