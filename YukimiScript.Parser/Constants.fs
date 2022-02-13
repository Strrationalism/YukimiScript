module YukimiScript.Parser.Constants

open Basics
open YukimiScript.Parser.Elements
open ParserMonad


let private numberParser, integerParser =
    let numberChar = inRange <| seq { '0' .. '9' }

    let unsignedIntegerString =
        parser {
            let! n = oneOrMore numberChar
            return toStringTrim n
        }
        |> name "digit"

    let sign =
        parser {
            let! sign = zeroOrOne (literal "-")

            if sign.IsSome then
                return "-"
            else
                return ""
        }

    let numberParser =
        parser {
            let! sign = sign
            do! whitespace0
            let! a = unsignedIntegerString
            do! literal "."
            let! b = unsignedIntegerString

            return Real <| float (sign + a + "." + b)
        }
        |> name "number"

    let integerParserDec =
        parser {
            let! sign = sign
            do! whitespace0
            let! i = unsignedIntegerString
            return Integer <| int (sign + i)
        }
        |> name "integer"

    let integerParserHex =
        parser {
            let! sign = sign
            do! whitespace0
            do! literal "0x"
            let aToF = inRange <| Seq.append (seq { 'a' .. 'f' }) (seq { 'A' .. 'F' })
            let! hexStrLs = (numberChar <|> aToF) |> oneOrMore
            let hexStr = sign + toStringTrim hexStrLs
            return Integer <| System.Convert.ToInt32(hexStr, 16)
        }

    let integerParser = integerParserHex <|> integerParserDec

    numberParser, integerParser



exception InvalidStringCharException of string


let string2literal =
    String.collect
        (function
        | '\n' -> "\\n"
        | '\t' -> "\\t"
        | '\r' -> "\\r"
        | '\'' -> "\\'"
        | '\"' -> "\\\""
        | '\\' -> "\\\\"
        | x -> x.ToString())

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
                        InvalidStringCharException <| "\\" + string x

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
        do! literal "\""
        let! chars = zeroOrMore stringChar
        do! literal "\""
        return toString chars
    }
    |> name "string"


let private stringConstant = map String stringParser


let constantParser =
    [ numberParser
      integerParser
      stringConstant
      map Symbol symbol ]
    |> choices
    |> name "constant"
