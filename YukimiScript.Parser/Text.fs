module internal YukimiScript.Parser.Text

open YukimiScript.Parser.Elements
open ParserMonad
open Basics


let private commandCall =
    parser {
        do!  literal "["
        do!  whitespace0
        let! commandCall = explicit Statment.commandCall
        do!  whitespace0
        do!  explicit (literal "]")
        return TextSlice.CommandCall commandCall
    }


let private bareText =
    let charPred x =
        Seq.exists ((=) x) 
            [ '\n'
              '['
              '<'
              ']'
              '>'
              '#'
              '\\'
              '\r' ]
        |> not

    let textChar = predicate charPred anyChar

    oneOrMore textChar
    |> map (toString >> TextSlice.Text)
    |> name "text"


let rec private markBlock () =
    parser {
        do! literal "<"
        let! mark = symbol
        do! whitespace1
        let! innerText = zeroOrMore <| textSlice ()
        do! literal ">"
        return Marked (mark, innerText)
    }
    |> name "text mark"
    

and private textSlice () =
    choices [
        commandCall
        bareText
        markBlock ()
    ]
    |> name "text slice"


let text = 
    parser {
        let! character =
            parser {
                do! whitespace0
                let! character = symbol
                do! literal ":"
                return character
            }
            |> zeroOrOne

        let! text = oneOrMore <| textSlice ()

        let text =
            text
            |> List.filter 
                (function
                    | TextSlice.Text x -> 
                        not <| System.String.IsNullOrWhiteSpace x
                    | _ -> true)

        let text =
            text
            |> List.tryFindIndexBack 
                (function
                    | TextSlice.Text _ -> true
                    | _ -> false)
            |> function
                | None -> text
                | Some index ->
                    let lastTextSlice =
                        match text.[index] with
                        | TextSlice.Text x -> x.Trim () |> TextSlice.Text
                        | _ -> failwith ""

                    text.[..index-1] @ [lastTextSlice] @ text.[index+1..]

        let! hasMore = 
            parser {
                do! whitespace0
                do! literal "\\"
                do! whitespace0
            }
            |> zeroOrOne

        return Line.Text
            { Character = character;
              Text = text;
              HasMore = hasMore.IsSome }
    }
    |> name "text"


let toCommands (text: TextBlock) : CommandCall list =
    [ 
        { Callee = "__text_begin"
          UnnamedArgs = []
          NamedArgs = [ 
              if text.Character.IsSome then
                  "character", Symbol text.Character.Value
          ] }
        
        let rec textSliceToCommand x =
            x
            |> List.collect
                (function
                    | TextSlice.CommandCall x -> [x]
                    | TextSlice.Text x ->
                        [ { Callee = "__text_type"
                            UnnamedArgs = []
                            NamedArgs = [ "text", String x] }]
                    | Marked (mark, inner) ->
                        [
                            { Callee = "__text_pushMark"
                              UnnamedArgs = []
                              NamedArgs = [ "mark", Symbol mark ] }

                            yield! (textSliceToCommand inner)

                            { Callee = "__text_popMark"
                              UnnamedArgs = [] 
                              NamedArgs = [ "mark", Symbol mark] }
                        ])

        yield! (textSliceToCommand text.Text)


        { Callee = "__text_end"
          UnnamedArgs = []
          NamedArgs = 
              [ "hasMore", Symbol <| text.HasMore.ToString().ToLower() ] }
    ]

