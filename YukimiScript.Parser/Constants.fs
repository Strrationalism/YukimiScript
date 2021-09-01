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
        |> name "digit"

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
        |> name "number"

    let integerParser =
        parser {
            let! sign = sign
            do!  whitespace0
            let! i = unsignedIntegerString
            return Integer <| int (sign + i)
        }
        |> name "integer" 

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
                        <| "\\" + string x
                    raise ex
                    return! fail ex
            }

        parser {
            match! predicate ((<>) '\"') anyChar with
            | '\n' -> 
                let ex = InvalidStringCharException "newline"
                raise ex
                return! fail ex
            | '\\' -> return! secondCharParser
            | x -> return x
        }

    parser {
        do!  literal "\""
        let! chars = zeroOrMore stringChar
        do!  literal "\""
        return toString chars
    }
    |> name "string"


let private stringConstant =
    map String stringParser


let constantParser =
    [ numberParser
      integerParser
      stringConstant
      map Symbol symbol ]
    |> choices
    |> name "constant"
