module YukimiScript.Parser.Parser

open YukimiScript.Parser.Elements
open ParserMonad
open Basics


let private lineComment: Parser<string> =
    parser {
        do! literal "#"

        let commentChar = 
            predicate 
                (fun x -> x <> '\r' && x <> '\n')
                anyChar

        let! comment = zeroOrMore commentChar

        return toStringTrim comment
    }
    |> name "comment"


type Parsed =
    { Line: Elements.Line
      Comment: string option }


let parseLine (line: string) =
    parser {
        do!  whitespace0
        let! parsed =
            choices [
                TopLevels.topLevels
                Statment.statment
                Text.text
                return' Line.EmptyLine
            ]
            
        do! whitespace0
        let! comment = zeroOrOne lineComment
        return { Line = parsed; Comment = comment }
    }
    |> run line
