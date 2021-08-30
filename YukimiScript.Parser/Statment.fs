module internal YukimiScript.Parser.Statment

open YukimiScript.Parser.Elements
open ParserMonad
open Basics
open Constants


let commandCall =
    parser {
        do!  whitespace0
        let! command = symbol
        
        let! unnamedArgs = 
            parser {
                do! whitespace1
                return! constantParser
            }
            |> zeroOrMore

        let! namedArgs =
            parser {
                do! whitespace1
                do! literal "--"
                let! param = explicit symbol

                let arg = 
                    parser {
                        do! whitespace1
                        return! constantParser
                    }

                let! arg = arg <|> return' (Symbol "true")

                return param, arg
            }
            |> zeroOrMore

        return 
            { Callee = command
              UnnamedArgs = unnamedArgs
              NamedArgs = namedArgs }
    }
    |> name "command call"


let statment =
    parser {
        do! literal "@"
        return! 
            commandCall 
            |> map Line.CommandCall
    }
