module YukimiScript.Parser.Constants

open Basics
open YukimiScript.Parser.Elements
open ParserMonad


let private numberParser, integerParser =
    let numberChar = 
        inRange <| seq { '0' .. '9' }

    let unsignedIntegerString =
        parser {
            let! n = oneOrMore numberChar
            return toStringTrim n
        }
        |> name "<integer>"

    let sign = 
        parser {
            let! sign = zeroOrOne (literal "-")
            if sign.IsSome then return "-"
            else return ""
        }

    let numberParser =
        parser {
            let! sign = sign
            do!  whitespace0
            let! a = unsignedIntegerString
            do!  literal "."
            let! b = unsignedIntegerString

            return Number <| float (sign + a + "." + b)
        }
        |> name "<number constant>"

    let integerParser =
        parser {
            let! sign = sign
            do!  whitespace0
            let! i = unsignedIntegerString
            return Integer <| int (sign + i)
        }
        |> name "<integer constant>" 

    numberParser,
    integerParser 
        


exception InvalidStringCharException of string


let stringParser =
    let stringChar =
        let secondCharParser =
            parser {
                match! anyChar with
                | 'n' -> return '\n'
                | 't' -> return '\t'
                | '\\' -> return '\\'
                | 'r' -> return '\r'
                | '\'' -> return '\''
                | '\"' -> return '\"'
                | x -> 
                    let ex = 
                        InvalidStringCharException 
                            ("\\" + string x)
                    raise ex
                    return! fail ex
            }

        parser {
            let! char = predicate ((<>) '\"') anyChar
            match char with
            | '\n' -> 
                let ex = InvalidStringCharException "<newline>"
                raise ex
                return! fail ex
            | '\\' -> return! secondCharParser
            | x -> return x
        }

    parser {
        do! literal "\""
        let! chars = zeroOrMore stringChar
        do! literal "\""
        return toString chars
    }
    |> name "<string constant>"


let private stringConstant =
    map String stringParser


let constantParser =
    [
        numberParser
        integerParser
        stringConstant
        symbol |> map Symbol
    ]
    |> choices
    |> name "<constant>"
