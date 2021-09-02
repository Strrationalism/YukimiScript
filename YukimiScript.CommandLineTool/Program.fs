open System
open System.IO
open YukimiScript.Parser
open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.Dom


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
    

let printError (e: string) =
    lock stdout (fun _ ->
        Console.WriteLine("Error:" + e))


let private e2str = ErrorStringing.schinese

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
                let project = Project.openProject option.ScriptDir
                let libs =
                    option.LibraryDirs
                    |> Seq.collect (fun libDir ->
                        Directory.EnumerateFiles(
                            libDir, 
                            "*.ykm", 
                            SearchOption.AllDirectories))

                let getDom x =
                    x
                    |> Array.ofSeq
                    |> Array.Parallel.map (fun path ->
                        let lines = 
                            File.ReadAllLines path
                            |> Array.Parallel.map Parser.parseLine
                            |> Array.toList

                        let errors =
                            lines 
                            |> List.indexed
                            |> List.choose (fun (lineNumber, line) ->
                                let lineNumber = lineNumber + 1
                                match line with
                                | Ok _ -> None
                                | Error e ->
                                    path + "(" + string lineNumber + "):" + e2str e
                                    |> printError
                                    
                                    Some e)
                                    
                        if List.isEmpty errors |> not then
                            Error errors.Head
                        else
                            lines
                            |> switchResultList
                            |> Result.bind (analyze path)
                            |> function
                                | Error e ->
                                    path + ":" + e2str e
                                    |> printError
                                    Error e
                                | Ok dom -> Ok dom
                        )
                        |> Array.toList
                        |> switchResultList
                        |> Result.map 
                            (List.fold merge empty)
                        |> function
                            | Error _ -> raise FailException
                            | Ok x -> x

                let libDom = getDom <| Seq.append project.Library libs

                if libDom.Scenes |> List.isEmpty |> not then
                    printError <| e2str CannotDefineSceneInLibException
                    raise FailException

                let scenarioDom = getDom <| project.Scenario
                let programDom = getDom <| project.Program

                // TODO:检查最终的dom中是否存在重复的macro、scenes和externs
                
                    
                0
            with 
            | :? FailException -> 
                Console.WriteLine()
                -1
            | e ->
                Console.WriteLine(e.Message)
                -1
