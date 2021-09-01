open System
open YukimiScript.Parser
open YukimiScript.Parser.ParserMonad


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
        | Error _ -> help ()
        | Ok option ->
            Console.WriteLine(option.ToString())

    0