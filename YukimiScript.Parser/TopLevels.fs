module internal YukimiScript.Parser.TopLevels

open YukimiScript.Parser.Elements
open ParserMonad
open Basics
open Constants


let private macroDefination =
    parser {
        let! macroName = explicit symbol
        let! param =
            parser {
                do! whitespace1
                let! paramName = symbol

                let! defArg =
                    parser {
                        do! literal "="
                        return! constantParser
                    }
                    |> zeroOrOne
                 
                return paramName, defArg
            }
            |> zeroOrMore

        return MacroDefination (macroName, param)
    }


let private sceneDefaintion =
    parser {
        let! sceneName = explicit stringParser
        
        let! inheritScene =
            parser {
                do! whitespace0
                do! literal "inherit"
                do! whitespace0
                return! explicit stringParser
            }
            |> zeroOrOne

        return SceneDefination (sceneName, inheritScene)
    }
    |> name "<sceneDefination>"


exception InvalidTopLevelException of string


let topLevels =
    parser {
        do!  literal "-"
        do!  whitespace0
        let! symbol = explicit symbol
        do!  whitespace0

        match symbol  with
        | "import" -> 
            let! importName = 
                explicit stringParser
                |> mapError (fun _ -> ExceptNamesException ["<import>"])
            return Import importName

        | "macro" -> return! macroDefination
        | "scene" -> return! sceneDefaintion
        | x -> return! fail (InvalidTopLevelException x)
    }
    |> name "<topLevel>"