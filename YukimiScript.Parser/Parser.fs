module YukimiScript.Parser.Parser

open YukimiScript.Parser.BasicParser
open YukimiScript.Parser.Elements
open YukimiScript.Parser.ParserMonad


let private emptyLine = return' EmptyLine


let private line =
    [
        TopLevelParser.topLevelScopeDefination
        StatmentParser.statment |> map Statment
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
    

let parseLine (line: string) : Result<Parsed, exn> =
    run line codeLine
    |> Result.map (fun (parsed, comment) ->
        { Line = parsed; Comment = comment })
    