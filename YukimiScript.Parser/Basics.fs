module YukimiScript.Parser.Basics

open ParserMonad


let whitespace0, whitespace1 =
    let whitespaceChar = inRange [ ' '; '\t' ]
    let post = map ignore >> name "whitespace"
    zeroOrMore whitespaceChar |> post,
    oneOrMore whitespaceChar |> post


let toString: _ -> string = 
    Seq.toArray >> fun x -> System.String(x)


let toStringTrim x = 
    (toString x).Trim()


exception InvalidSymbolException


let symbol: Parser<string> =
    let firstCharacter = 
        seq { 
            yield! seq { 'a' .. 'z' }
            yield! seq { 'A' .. 'Z' }
            '_'
            '.'
        }

    let character = 
        firstCharacter 
        |> Seq.append (seq { '0' .. '9' })

    parser {
        let! first = inRange firstCharacter
        let! tail = zeroOrMore (inRange character)
        return toStringTrim (first :: tail)
    }
    |> mapError (fun _ -> InvalidSymbolException)


