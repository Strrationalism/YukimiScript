module YukimiScript.Parser.ParserMonad

type Parser<'a> =
    { Run: char list -> Result<'a * char list, exn>
      Name: string }

let bind (f: 'a -> Parser<'b>) (p: Parser<'a>) : Parser<'b> =
    failwith "No Impl"

let return' (a: 'a) : Parser<'a> = 
    failwith "No Impl"

let name (name: string) (a: Parser<'a>) : Parser<'a> =
    { a with Name = name }

type ParserBuilder () =
    member _.Bind(x, f) = bind f x
    member _.Return(x) = return' x

let parser = ParserBuilder ()

exception EndOfUnitException

let anyChar = 
    { Run = 
        function
            | x::ls -> Ok (x, ls)
            | [] -> Error EndOfUnitException 
      Name = "Any Character" }

