module YukimiScript.Parser.Parser

open YukimiScript.Parser.BasicParser
open YukimiScript.Parser.Elements
open YukimiScript.Parser.ParserMonad


let private emptyLine = return' EmptyLine


let private line =
    [
        TopLevelParser.topLevelScopeDefination
        emptyLine
    ]
    |> choices
    |> name "<line>"
    

let private codeLine = 
    parser {
        do!  whitespace0
        let! line = line
        do!  whitespace0
        let! comment = zeroOrOne lineComment

        return line, comment
    }
    |> name "<codeLine>"


type Parsed = 
    { Line: Line
      Comment: string option }
    

exception ParseUnfinishedException of string


let parseLine (line: string) : Result<Parsed, exn> =
    try
        codeLine.Run (Seq.toList line)
        |> Result.bind 
            (fun ((line, comment), remainder) -> 
                if List.isEmpty remainder then
                    Ok { Line = line; Comment = comment }
                else 
                    remainder
                    |> List.toArray
                    |> System.String
                    |> ParseUnfinishedException
                    |> Error)
    with 
    | e -> Error e
    