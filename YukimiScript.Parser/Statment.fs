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
                let! param = symbol
                do! whitespace1
                let! arg = constantParser
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
            |> map CommandCall
    }
