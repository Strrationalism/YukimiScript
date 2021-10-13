module YukimiScript.Parser.EditorHelper

open YukimiScript.Parser
open YukimiScript.Parser.Elements


type Runner<'State> = 'State -> CommandCall -> Result<'State>


let rec run (state: 'State) (runner: Runner<'State>) (ops: Block) : Result<'State> =
    match Seq.tryHead ops with
    | None -> Ok state
    | Some (EmptyLine, _) -> run state runner <| List.tail ops
    | Some (CommandCall c, _) ->
        runner state c
        |> Result.bind (fun state -> run state runner <| List.tail ops)
    | Some (Text t, d) ->
        run state runner <| Text.expandTextBlock t d
        |> Result.bind (fun state -> run state runner <| List.tail ops)


let idRunner: Runner<'State> = fun a _ -> Ok a


let mapRunner (a2b: 'a -> 'b) (b2a: 'b -> 'a) (a: Runner<'a>) : Runner<'b> =
    fun state c -> a (b2a state) c |> Result.map a2b


let dispatch (dispatcher: CommandCall -> Runner<'State>) : Runner<'State> = fun state c -> (dispatcher c) state c


type RunnerWrapper<'TState>(init: 'TState, mainRunner: Runner<'TState>) =
    member _.Run(state: 'TState, ops: Block) : Result<'TState> = run state mainRunner ops

    member x.Run(ops: Block) : Result<'TState> = x.Run(init, ops)
