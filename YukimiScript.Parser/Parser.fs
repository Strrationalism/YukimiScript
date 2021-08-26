module YukimiScript.Parser.Parser

open YukimiScript.Parser.Elements
open ParserMonad


let private whitespace0, private whitespace1 =
    let whitespaceChar = inRange [ ' '; '\t' ]
    let post = map ignore >> name "whitespace"
    zeroOrMore whitespaceChar |> post,
    oneOrMore whitespaceChar |> post


let private lineComment: Parser<string> =
    (inRange ['#'] 
        <+@> zeroOrMore 
            (predicate 
                (fun x -> x <> '\r' && x <> '\n')
                anyChar))
    |> map (Seq.toArray >> fun x -> System.String(x).Trim())
    |> name "comment"
    

exception InvalidIdentifierException


let private identifier: Parser<string> =
    let firstCharacter = 
        seq { 
            yield! seq { 'a' .. 'z' }
            yield! seq { 'A' .. 'Z' }
            yield '_' 
        }

    let character = 
        firstCharacter 
        |> Seq.append (seq { '0' .. '1' })

    (inRange firstCharacter 
        <+> zeroOrMore (inRange character))
    |> map (fun (a, ls) -> 
        a :: ls
        |> List.toArray
        |> System.String)
    |> name "identifier"
    |> mapError (fun _ -> InvalidIdentifierException)


let private objectName =
    (identifier 
        <+> zeroOrMore 
            (literal "." <+@> identifier))
    |> name "object name"
    |> map (fun (x, ls) ->
        ls
        |> List.rev
        |> List.fold 
            (fun curObjName curName ->
                ObjectName (Some curObjName, curName))
            (ObjectName (None, x)))


let private emptyLine = return' EmptyLine


let private globalDefination =
    literal "global"
    |> map (fun _ -> GlobalDefination)
    |> name "global defination"


exception InvalidParameterException
exception TopLevelScopeFailedException


let private methodDefination =
    let header = literal "method"
    let methodName = objectName
    let parameter = 
        identifier
        |> map (fun x -> x, None)

    (header 
        <+> whitespace1
        <+@> methodName 
        <+> zeroOrMore (whitespace1 <+@> parameter))    // TODO: 这里尚未解析默认值
    |> map 
        (fun (methodName, parameters) ->
            MethodDefination (
                methodName,
                parameters
                |> List.map (fun (x, _) -> Parameter (x, None))
            ))
    

let private sceneDefination =
    let header = literal "scene" 
    
    (header <+@> (whitespace1 <+@> identifier |> mapError raise))
    |> map SceneDefination
    


let private topLevelScopeDefination : Parser<Line> =
    let topLevels = 
        [ 
            globalDefination
            sceneDefination
            methodDefination 
        ]
        |> List.reduce (<|>)

    literal "-" <+@> whitespace0 <+@> topLevels


let private line =
    [ topLevelScopeDefination
      emptyLine ]
    |> List.reduce (<|>)
    

let private codeLine = 
    whitespace0 <+@> line <@+> whitespace0 <+> zeroOrOne lineComment


type Parsed = 
    { Line: Line
      Comment: string option }
    

exception ParseUnfinishedException of string


let parseLine (line: string) : Result<Parsed, exn> =
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
    