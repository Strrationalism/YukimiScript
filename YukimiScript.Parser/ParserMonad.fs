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


exception ExceptSymbolException of string


let name (name: string) (a: Parser<'a>) : Parser<'a> =
    { a with Name = name } 


let fail<'a> (e: exn) : Parser<'a> =
    { Run = fun _ -> Error e
      Name = string e }


let tryWith (f: exn -> Parser<'a>) (a: Parser<'a>) : Parser<'a> = 
    { a with 
        Run = 
            fun input ->
                match a.Run input with
                | Error e -> (f e).Run input
                | Ok x -> Ok x }


let explicit (a: Parser<'a>) : Parser<'a> =
    mapError raise a


type ParserBuilder () =
    member _.Bind(x, f) = bind f x
    member _.Return(x) = return' x
    member _.ReturnFrom(x) = x
    member _.Delay(x) = x ()
    member _.TryWith(x, f) = tryWith f x


let parser = ParserBuilder ()


exception EndException


let anyChar = 
    { Run = 
        function
            | x::ls -> Ok (x, ls)
            | [] -> Error EndException 
      Name = "any" }

      
exception PredicateFailedException


let predicate (f: 'a -> bool) (a: Parser<'a>) : Parser<'a> =
    parser {
        match! a with
        | a when f a -> return a
        | _ -> return! fail PredicateFailedException
    }
    |> name ("(predicate ? " + a.Name + ")")


exception ExceptNamesException of string list


let ( <||> ) (a: Parser<'a>) (b: Parser<'b>) : Parser<Choice<'a, 'b>> = 
    { Name = a.Name
      Run = 
          fun input ->
              match a.Run input with
              | Ok (x, r) -> Ok (Choice1Of2 x, r)
              | Error e1 -> 
                  match b.Run input with
                  | Ok (x, r) -> Ok (Choice2Of2 x, r)
                  | Error e2 ->
                      ExceptNamesException [ a.Name; b.Name ]
                      |> Error
    }
    |> name ("(<||> " + a.Name + b.Name)


let ( <|> ) (a: Parser<'a>) (b: Parser<'a>) =
    (a <||> b) 
    |> map
        (function
            | Choice1Of2 x -> x
            | Choice2Of2 x -> x)


let rec choices (ls: Parser<'a> list) : Parser<'a> =
    match ls with
    | [] -> invalidArg (nameof ls) "Choices must more than 1."
    | [a] -> a
    | a :: more ->
        let names = List.map (fun x -> x.Name) ls
        (a <|> choices more)
        |> mapError 
            (fun _ -> ExceptNamesException names)
        |> name 
            ("(choices " 
                + List.reduce 
                    (fun a b -> a + " " + b)
                    names
                + ")")


let rec zeroOrMore (a: Parser<'a>) : Parser<'a list> =
    parser {
        try
            let! head = a
            let! tail = zeroOrMore a
            return head :: tail
        with 
        | _ -> return []
    }
    |> name ("(zeroOrMore " + a.Name + ")")


let oneOrMore (a: Parser<'a>) : Parser<'a list> =
    parser {
        let! head = a
        let! tail = zeroOrMore a
        return head :: tail
    }
    |> name ("(oneOrMore" + a.Name + ")")


let zeroOrOne (a: Parser<'a>) : Parser<'a option> =
    parser {
        try 
            let! a = a in return Some a
        with 
        | _ -> return None
    }
    |> name ("(zeroOrOne " + a.Name + ")")


exception NotInRangeException of char seq


let rec inRange (range: char seq) : Parser<char> =
    anyChar
    |> predicate (fun x -> Seq.exists ((=) x) range)
    |> mapError (fun _ -> NotInRangeException range)
    |> name ("(inRange ?)")


exception NotLiteralException of string 


let rec literal (x: string) : Parser<unit> =
    if x = "" then return' ()
    else 
        parser {
            let! _ = predicate ((=) x.[0]) anyChar
            let! _ = literal x.[1..]
            return ()
        }
        |> mapError (fun _ -> NotLiteralException x)
    |> name ("(literal " + x + ")")


exception ParseUnfinishedException of string


let run (line: string) (parser: Parser<'a>) : Result<'a, exn> =
    try
        parser.Run (Seq.toList line)
        |> Result.bind 
            (fun (result, remainder) -> 
                if List.isEmpty remainder then
                    Ok result
                else 
                    remainder
                    |> List.toArray
                    |> System.String
                    |> ParseUnfinishedException
                    |> Error)
    with 
    | e -> Error e
    