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
      "    -lib <libDir>         Add other library."
      "    -dgml <output>        Create the diagram."
      "    -voicedoc <outDir>    Create the voice documents."
      ""
      "Examples:"
      "    Check the scripts:"
      "        ykmc \"./scripts\" -lib \"./api\""
      "    Create the diagram from scripts:"
      "        ykmc \"./scripts\" -lib \"./api\" -dgml \"./diagram.dgml\""
      "    Create the voice documents:"
      "        ykmc \"./scripts\" -lib \"./api\" -voicedoc \"./outdir\""
      "" ]
    |> Seq.iter Console.WriteLine


type Option =
    { ScriptDir: string
      VoiceDocumentOutputDir: string option
      DiagramOutputFile: string option
      LibraryDirs: string list }


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
            | "-lib" ->
                arg
                |> bind (fun lib ->
                    { cur with LibraryDirs = lib :: cur.LibraryDirs } 
                    |> options)
            | "-dgml" ->
                if cur.DiagramOutputFile.IsSome then
                    raise OptionErrorException
                else 
                    arg
                    |> bind (fun dgml ->
                        { cur with DiagramOutputFile = Some dgml }
                        |> options)
            | "-voicedoc" ->
                if cur.VoiceDocumentOutputDir.IsSome then
                    raise OptionErrorException
                else
                    arg
                    |> bind (fun voice ->
                        { cur with VoiceDocumentOutputDir = Some voice }
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
                            |> Result.mapError (fun x -> fileName, x))
                    
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
                    // TODO: 输出配音稿
                    |> expandTextAndUserMacros
                    // TODO: 绘制分支图

                let program = 
                    loadDoms project.Program
                    |> expandTextAndUserMacros                 

                seq {
                    lib
                    yield! scenario
                    yield! program
                }
                |> Seq.fold Dom.merge Dom.empty
                |> checkRepeat
                |> Result.map Dom.expandSystemMacros
                |> Result.bind Dom.linkToExternCommands
                |> function
                    | Error x -> 
                        ErrorProcessing.printExn "" x
                        raise FailException
                    | Ok finalDom -> ()               
                    
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
