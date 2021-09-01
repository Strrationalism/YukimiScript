module YukimiScript.Parser.Dom

open YukimiScript.Parser.Parser
open YukimiScript.Parser.Elements


type Dom = 
    { HangingEmptyLine: DebugInformation list
      Externs: (ExternDefination * DebugInformation) list
      Macros: (MacroDefination * Block * DebugInformation) list
      Scenes: (SceneDefination * Block * DebugInformation) list }


let private empty = 
    { HangingEmptyLine = []
      Externs = []
      Macros = []
      Scenes = [] }


type private AnalyzeState =
    { Result: Dom
      CurrentBlock: (Line * DebugInformation * Block) option }


exception HangingOperationException of debugInfo: DebugInformation


exception UnknownException


exception SceneRepeatException of debugInfo: DebugInformation * scene: string


exception MacroRepeatException of debugInfo: DebugInformation * macro: string


exception ExternRepeatException of debugInfo: DebugInformation * name: string


let private analyzeFold 
            state 
            (line, debugInfo) 
            =

    let pushOperation x =
        match state.CurrentBlock with
        | None -> Error <| HangingOperationException debugInfo
        | Some (line, labelDbgInfo, block) -> 
            { state with
                CurrentBlock = 
                    Some (
                        line,
                        labelDbgInfo,
                        (x, debugInfo) :: block
                    ) 
            }
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

    | Line.CommandCall x -> pushOperation <| CommandCall x
    | Line.Text x -> pushOperation <| Text x
    | SceneDefination scene ->
        if List.exists (fun (x, _, _) -> x.Name = scene.Name) state.Result.Scenes then
            Error <| SceneRepeatException (debugInfo, scene.Name)
        else Ok <| setLabel state (SceneDefination scene)
    | MacroDefination macro -> 
        if List.exists (fun (x, _, _) -> x.Name = macro.Name) state.Result.Scenes then
            Error <| SceneRepeatException (debugInfo, macro.Name)
        else Ok <| setLabel state (MacroDefination macro)
    | ExternDefination (ExternCommand (name, param)) ->
        if List.exists (fun (x, _, _) -> x.Name = name) state.Result.Scenes then
            Error <| SceneRepeatException (debugInfo, name)
        else 
            let nextState = saveCurrentBlock state
            { nextState with
                Result = 
                    { nextState.Result with 
                        Externs = 
                            (ExternCommand (name, param), debugInfo) 
                            :: nextState.Result.Externs } }
            |> Ok


let analyze (x: Parsed seq) : Result<Dom, exn> = 
    let finalState =
        x
        |> Seq.indexed
        |> Seq.map (fun (lineNumber, { Line = line; Comment = comment }) ->
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
          Externs = List.rev x.Result.Externs
          HangingEmptyLine = List.rev x.Result.HangingEmptyLine })


let expandTextCommands (x: Dom) : Dom =
    let mapBlock (defination, block, debugInfo) =
        let block = 
            block
            |> List.collect (function
                | Text x, debugInfo ->
                    [ if debugInfo.Comment.IsSome then
                          EmptyLine, debugInfo

                      let blockDebugInfo: DebugInformation =
                          { debugInfo with Comment = None }
                        
                      yield! 
                          Text.toCommands x
                          |> List.map (fun x -> 
                                CommandCall x, blockDebugInfo)
                    ]
                | x -> [x]) 
                    
        defination, block, debugInfo

    { x with 
        Scenes = List.map mapBlock x.Scenes
        Macros = List.map mapBlock x.Macros }


let expandUserMacros (x: Dom) =
    let macros = List.map (fun (a, b, _) -> a, b) x.Macros
    List.foldBack 
        (fun (sceneDef, block, debugInfo) state ->
            Macro.expandBlock macros block
            |> Result.bind (fun block -> 
                state 
                |> Result.map (fun state -> 
                    (sceneDef, block, debugInfo) :: state)))
        x.Scenes
        (Ok [])
    |> Result.map (fun scenes ->
        { x with Scenes = scenes; Macros = [] })
            
 
let expandSystemMacros (x: Dom) =
    { x with 
        Scenes = 
            x.Scenes 
            |> List.map (fun (a, b, c) -> 
                a, Macro.expandSystemMacros b, c) }
                