module YukimiScript.Parser.Dom

open YukimiScript.Parser.Parser
open YukimiScript.Parser.Elements


type DebugInformation =
    { LineNumber: int
      Comment: string option }


type Operation =
    | Text of TextBlock
    | CommandCall of CommandCall
    | EmptyLine


type Block = (Operation * DebugInformation) list


type Dom = 
    { HangingEmptyLine: DebugInformation list
      Macros: (MacroDefination * Block * DebugInformation) list
      Scenes: (SceneDefination * Block * DebugInformation) list }


let private empty = 
    { HangingEmptyLine = []
      Macros = []
      Scenes = [] }


type private AnalyzeState =
    { Result: Dom
      CurrentBlock: (Line * DebugInformation * Block) option }


exception HangingOperationException of debugInfo: DebugInformation


exception UnknownException


let private analyzeFold 
            state 
            (line, debugInfo) =

    let pushOperation x =
        match state.CurrentBlock with
        | None -> Error <| HangingOperationException debugInfo
        | Some (line, labelDbgInfo, block) -> 
            { state with
                CurrentBlock = 
                    Some 
                        (line,
                         labelDbgInfo,
                         (x, debugInfo) :: block) }
            |> Ok

    let saveCurrentBlock state =
        match state.CurrentBlock with
        | None -> state
        | Some (label, debugInfo, block) ->
            { CurrentBlock = None
              Result = 
                  { state.Result with
                      Macros = 
                          match label with
                          | MacroDefination x ->
                              (x, List.rev block, debugInfo) :: state.Result.Macros 
                          | SceneDefination _ -> state.Result.Macros
                          | _ -> raise UnknownException 
                      Scenes =
                          match label with
                          | MacroDefination _ -> state.Result.Scenes
                          | SceneDefination x -> 
                              (x, List.rev block, debugInfo) :: state.Result.Scenes 
                          | _ -> raise UnknownException } }

    let setLabel state line =
        { saveCurrentBlock state with
            CurrentBlock = Some (line, debugInfo, []) }

    match line with
    | Line.EmptyLine -> 
        match state.CurrentBlock with
        | Some _ -> pushOperation EmptyLine
        | None -> 
            { state with 
                Result = 
                    { state.Result with 
                        HangingEmptyLine = 
                            debugInfo :: state.Result.HangingEmptyLine } }
            |> Ok

    | Line.CommandCall x -> pushOperation (CommandCall x)
    | Line.Text x -> pushOperation (Text x)
    | (SceneDefination _ | MacroDefination _) as x -> 
        setLabel state x |> Ok


let analyze (x: Parsed seq) : Result<Dom, exn> = 
    let finalState =
        x
        |> Seq.indexed
        |> Seq.map 
            (fun (lineNumber, { Line = line; Comment = comment }) ->
                line, { LineNumber = lineNumber + 1; Comment = comment })
        |> Seq.fold 
            (fun state x -> 
                Result.bind 
                    (fun state -> analyzeFold state x) 
                    state)
            (Ok { Result = empty
                  CurrentBlock = None })
    
    finalState 
    |> Result.map (fun x -> 
        { Scenes = List.rev x.Result.Scenes
          Macros = List.rev x.Result.Macros
          HangingEmptyLine = List.rev x.Result.HangingEmptyLine })