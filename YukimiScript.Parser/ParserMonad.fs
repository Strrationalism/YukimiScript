module YukimiScript.Parser.ParserMonad


type Parser<'a> =
    { Run: char list -> Result<'a * char list, exn>
      Name: string }


let bind (f: 'a -> Parser<'b>) (p: Parser<'a>) : Parser<'b> =
    { Run = p.Run >> Result.bind (fun (a, ls) -> (f a).Run ls)
      Name = p.Name }


let return' (a: 'a) : Parser<'a> = 
    { Run = fun x -> Ok (a, x)
      Name = string a }


let map (f: 'a -> 'b) (p: Parser<'a>) : Parser<'b> =
    bind (f >> return') p


let mapError (f: exn -> exn) (p: Parser<'a>) : Parser<'a> =
    { p with Run = p.Run >> Result.mapError f }


let name (name: string) (a: Parser<'a>) : Parser<'a> =
    { a with Name = name }


let fail<'a> (e: exn) : Parser<'a> =
    { Run = fun _ -> Error e
      Name = string e }


exception PredicateFailedException


let predicate (f: 'a -> bool) (a: Parser<'a>) : Parser<'a> =
    { a with 
        Run = 
            a.Run 
            >> Result.bind 
                (fun (x, ls) -> 
                    if f x then Ok (x, ls) 
                    else Error PredicateFailedException) }


let tryWith (f: exn -> Parser<'a>) (a: Parser<'a>) : Parser<'a> = 
    { a with 
        Run = 
            fun input ->
                match a.Run input with
                | Error e -> (f e).Run input
                | x -> x }


type ParserBuilder () =
    member _.Bind(x, f) = bind f x
    member _.Return(x) = return' x
    member _.ReturnFrom(x) = x
    member _.Delay(x) = x ()
    member _.TryWith(x, f) = tryWith f x


let parser = ParserBuilder ()


exception EndOfUnitException


let anyChar = 
    { Run = 
        function
        | x::ls -> Ok (x, ls)
        | [] -> Error EndOfUnitException 
      Name = "Any Character" }


let ( <+> ) (a: Parser<'a>) (b: Parser<'b>) = 
    parser {
        let! a = a
        let! b = b
        return (a, b)
    }
    |> name ("(" + a.Name + ", " + b.Name + ")")


let ( <@+> ) (a: Parser<'a>) (b: Parser<_>) : Parser<'a> =
    (a <+> b) |> map fst |> name ("(" + b.Name + ", ..)")


let ( <+@> ) (a: Parser<_>) (b: Parser<'a>) : Parser<'a> =
    (a <+> b) |> map snd |> name ("(.., " + b.Name + ")")


exception BinaryException of exn * exn


let ( <||> ) (a: Parser<'a>) (b: Parser<'b>) = 
    parser {
        return! (
            try 
                map Choice1Of2 a
            with 
            | e1 -> 
                map Choice2Of2 b 
                |> mapError 
                    (fun e2 -> 
                        BinaryException (e1, e2))
        )
    }
    |> name ("(" + a.Name + " | " + b.Name + ")")


let ( <|> ) (a: Parser<'a>) (b: Parser<'a>) =
    (a <||> b)
    |> map
        (function
        | Choice1Of2 x -> x
        | Choice2Of2 x -> x)


let rec zeroOrMore (a: Parser<'a>) : Parser<'a list> =
    parser {
        try
            let! head = a
            let! tail = zeroOrMore a
            return head :: tail
        with 
        | _ -> return []
    }
    |> name ("ZeroOrMore (" + a.Name + ")")


let oneOrMore (a: Parser<'a>) : Parser<'a list> =
    (a <+> zeroOrMore a)
    |> map (fun (head, tail) -> head::tail)
    |> name ("OneOrMore (" + a.Name + ")")


let zeroOrOne (a: Parser<'a>) : Parser<'a option> =
    parser {
        try 
            let! a = a in return Some a
        with 
        | _ -> return None
    }
    |> name ("ZeroOrOne (" + a.Name + ")")
