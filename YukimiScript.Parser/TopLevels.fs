module internal YukimiScript.Parser.TopLevels

open YukimiScript.Parser.Elements
open ParserMonad
open Basics
open Constants


let private sceneDefaintion =
    parser {
        let! sceneName = 
            explicit 
            <| name "scene name" stringParser

        let! inheritScene =
            parser {
                do! whitespace0
                do! literal "inherit"
                do! whitespace0
                return! explicit <| 
                    name "inherit scene name" stringParser
            }
            |> zeroOrOne

        return { Name = sceneName; Inherit = inheritScene }
    }
    |> name "scene defination"


exception InvalidTopLevelException of string


let private externDefination =
    parser {
        let! externName = explicit symbol
        let! param = Macro.parameterList externName
        return ExternCommand (externName, param)
    }

let topLevels =
    parser {
        do!  literal "-"
        do!  whitespace0
        let! symbol = explicit symbol
        do!  whitespace0
        
        match symbol  with
        | "macro" -> return! map MacroDefination Macro.macroDefinationParser
        | "scene" -> return! map SceneDefination sceneDefaintion
        | "extern" -> return! map ExternDefination externDefination
        | x -> return! explicit <| fail (InvalidTopLevelException x)
    }
    |> name "top level"