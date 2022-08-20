module YukimiScript.Parser.Parser

open YukimiScript.Parser.Elements
open YukimiScript.Parser.Utils
open ParserMonad
open Basics


let private lineComment: Parser<string> =
    parser {
        do! literal "#"

        let commentChar =
            predicate (fun x -> x <> '\r' && x <> '\n') anyChar

        let! comment = zeroOrMore commentChar

        return toStringTrim comment
    }
    |> name "comment"


type Parsed =
    { Line: Elements.Line
      Comment: string option }


let parseLine (line: string) =
    parser {
        do! whitespace0

        let! parsed =
            choices [ TopLevels.topLevels
                      Statment.statment
                      Text.text
                      return' Line.EmptyLine ]

        do! whitespace0
        let! comment = zeroOrOne lineComment
        return { Line = parsed; Comment = comment }
    }
    |> run line


let parseLines (line: string []) : Result<Parsed list, (int * exn) list> =
    let parsed =
        line
        |> Array.Parallel.map parseLine
        |> Array.toList

    let errors =
        parsed
        |> List.indexed
        |> List.choose
            (function
            | lineNumber, Error e -> Some(lineNumber, e)
            | _ -> None)

    if List.isEmpty errors then
        match Result.transposeList parsed with
        | Ok x -> Ok x
        | _ -> failwith "Internal Error"
    else
        Error errors
