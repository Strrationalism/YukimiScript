module YukimiScript.Parser.ParserMonad


type Parser<'a> =
    { Run: char list -> Result<'a * char list, exn> }


let bind (f: 'a -> Parser<'b>) (p: Parser<'a>) : Parser<'b> =
    { Run = p.Run >> Result.bind (fun (a, ls) -> (f a).Run ls) }


let return' (a: 'a) : Parser<'a> = 
    { Run = fun x -> Ok (a, x) }


let map (f: 'a -> 'b) (p: Parser<'a>) : Parser<'b> =
    bind (f >> return') p


let mapError (f: exn -> exn) (p: Parser<'a>) : Parser<'a> =
    { Run = p.Run >> Result.mapError f }


exception ExceptSymbolException of string


let name (name: string) (a: Parser<'a>) : Parser<'a> =
    mapError (fun _ -> ExceptSymbolException name) a


let fail<'a> (e: exn) : Parser<'a> =
    { Run = fun _ -> Error e }


let tryWith (f: exn -> Parser<'a>) (a: Parser<'a>) : Parser<'a> = 
    { Run = 
        fun input ->
            match a.Run input with
            | Error e -> (f e).Run input
            | Ok x -> Ok x }


let explicit (a: Parser<'a>) : Parser<'a> =
    mapError raise a


let switchResultList (x: Result<'a, 'b> list) : Result<'a list, 'b> =
    (x, Ok [])
    ||> List.foldBack (fun x state -> 
        state |> Result.bind (fun state ->
            x |> Result.map (fun x -> x :: state)))


type ParserBuilder() =
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
        | [] -> Error EndException }

      
exception PredicateFailedException


let predicate (f: 'a -> bool) (a: Parser<'a>) : Parser<'a> =
    parser {
        match! a with
        | a when f a -> return a
        | _ -> return! fail PredicateFailedException
    }


exception MultiException of exn list


let ( <||> ) (a: Parser<'a>) (b: Parser<'b>) : Parser<Choice<'a, 'b>> = 
    { Run = 
          fun input ->
              match a.Run input with
              | Ok (x, r) -> Ok (Choice1Of2 x, r)
              | Error e1 -> 
                  match b.Run input with
                  | Ok (x, r) -> Ok (Choice2Of2 x, r)
                  | Error e2 -> Error (MultiException [ e1; e2 ])
                              
    }


let ( <|> ) (a: Parser<'a>) (b: Parser<'a>) =
    (a <||> b) 
    |> map (function
        | Choice1Of2 x -> x
        | Choice2Of2 x -> x)


let rec choices : Parser<'a> list -> Parser<'a> =
    function
    | [] -> invalidArg "_arg0" "Choices must more than 1."
    | [a] -> a
    | a :: more -> a <|> choices more


let rec zeroOrMore (a: Parser<'a>) : Parser<'a list> =
    parser {
        try
            let! head = a
            let! tail = zeroOrMore a
            return head :: tail
        with _ -> return []
    }


let oneOrMore (a: Parser<'a>) : Parser<'a list> =
    parser {
        let! head = a
        let! tail = zeroOrMore a
        return head :: tail
    }


let zeroOrOne (a: Parser<'a>) : Parser<'a option> =
    parser {
        try 
            let! a = a 
            return Some a
        with _ -> return None
    }


exception NotInRangeException of char seq


let rec inRange (range: char seq) : Parser<char> =
    anyChar
    |> predicate (fun x -> Seq.exists ((=) x) range)
    |> mapError (fun _ -> NotInRangeException range)


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


exception ParseUnfinishedException of string


let run (line: string) (parser: Parser<'a>) : Result<'a, exn> =
    try
        parser.Run (Seq.toList line)
        |> Result.bind (fun (result, remainder) -> 
            if List.isEmpty remainder then
                Ok result
            else 
                remainder
                |> List.toArray
                |> System.String
                |> ParseUnfinishedException
                |> Error)
    with e -> Error e
    