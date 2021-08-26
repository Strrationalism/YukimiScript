module internal YukimiScript.Parser.BasicParser

open YukimiScript.Parser.Elements
open ParserMonad


let whitespace0, whitespace1 =
    let whitespaceChar = inRange [ ' '; '\t' ]
    let post = map ignore >> name "<whitespace>"
    zeroOrMore whitespaceChar |> post,
    oneOrMore whitespaceChar |> post

    
let toString = 
    Seq.toArray >> fun x -> System.String(x).Trim()


let lineComment: Parser<string> =
    parser {
        do! literal "#"

        let commentChar = 
            predicate 
                (fun x -> x <> '\r' && x <> '\n')
                anyChar

        let! comment = zeroOrMore commentChar

        return toString comment
    }
    |> name "<lineComment>"


exception InvalidIdentifierException


let identifier: Parser<string> =
    let firstCharacter = 
        seq { 
            yield! seq { 'a' .. 'z' }
            yield! seq { 'A' .. 'Z' }
            yield '_' 
        }

    let character = 
        firstCharacter 
        |> Seq.append (seq { '0' .. '1' })

    parser {
        let! first = inRange firstCharacter
        let! tail = zeroOrMore (inRange character)
        return toString (first :: tail)
    }
    |> name "<identifier>"
    |> mapError (fun _ -> InvalidIdentifierException)



exception InvalidObjectNameException


let objectName =
    parser {
        let! root = identifier
        let! children = 
            zeroOrMore 
                (parser {
                    do! literal "."
                    return! identifier
                })
        
        let result =
            children
            |> List.rev
            |> List.fold 
                (fun curObjName curName ->
                    ObjectName (Some curObjName, curName))
                (ObjectName (None, root))

        return result
    }
    |> name "<object name>"
    |> mapError (fun _ -> InvalidObjectNameException)
