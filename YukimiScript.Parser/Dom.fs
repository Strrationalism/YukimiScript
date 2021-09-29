module YukimiScript.Parser.Dom

open YukimiScript.Parser.Parser
open YukimiScript.Parser.Elements


type Dom = 
    { HangingEmptyLine: DebugInformation list
      Externs: (ExternDefination * DebugInformation) list
      Macros: (MacroDefination * Block * DebugInformation) list
      Scenes: (SceneDefination * Block * DebugInformation) list }


let merge dom1 dom2 =
    { HangingEmptyLine = dom1.HangingEmptyLine @ dom2.HangingEmptyLine
      Externs = dom1.Externs @ dom2.Externs
      Macros = dom1.Macros @ dom2.Macros
      Scenes = dom1.Scenes @ dom2.Scenes }


let empty = 
    { HangingEmptyLine = []
      Externs = []
      Macros = []
      Scenes = [] }


type private AnalyzeState =
    { Result: Dom
      CurrentBlock: (Line * DebugInformation * Block) option }


exception HangingOperationException of debugInfo: DebugInformation


exception UnknownException


exception SceneRepeatException of scene: string * debugInfo: DebugInformation seq


exception MacroRepeatException of macro: string * debugInfo: DebugInformation seq


exception ExternRepeatException of name: string * debugInfo: DebugInformation seq


let private saveCurrentBlock state =
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
        Ok <| setLabel state (SceneDefination scene)
    | MacroDefination macro -> 
        Ok <| setLabel state (MacroDefination macro)
    | ExternDefination (ExternCommand (name, param)) ->
        let nextState = saveCurrentBlock state
        { nextState with
            Result = 
                { nextState.Result with 
                    Externs = 
                        (ExternCommand (name, param), debugInfo) 
                        :: nextState.Result.Externs } }
        |> Ok


exception CannotDefineSceneInLibException of string


let analyze (fileName: string) (x: Parsed seq) : Result<Dom> = 
    let finalState =
        x
        |> Seq.indexed
        |> Seq.map (fun (lineNumber, { Line = line; Comment = comment }) ->
                line, 
                { LineNumber = lineNumber + 1
                  Comment = comment
                  File = fileName })
        |> Seq.fold 
            (fun state x -> 
                Result.bind 
                    (fun state -> analyzeFold state x) 
                    state)
            (Ok { Result = empty
                  CurrentBlock = None })
        |> Result.map saveCurrentBlock
    
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
                        
                      yield! Text.expandTextBlock x debugInfo ]
                | x -> [x]) 
                    
        defination, block, debugInfo

    { x with 
        Scenes = List.map mapBlock x.Scenes
        Macros = List.map mapBlock x.Macros }


let expandUserMacros (x: Dom) =
    let macros = List.map (fun (a, b, _) -> a, b) x.Macros
    x.Scenes
    |> List.map (fun (sceneDef, block, debugInfo) -> 
        Macro.expandBlock macros block
        |> Result.map (fun x -> sceneDef, x, debugInfo))
    |> ParserMonad.switchResultList
    |> Result.map (fun scenes ->
        { x with Scenes = scenes })
            
 
let expandSystemMacros (x: Dom) =
    { x with 
        Scenes = 
            x.Scenes 
            |> List.map (fun (a, b, c) -> 
                a, Macro.expandSystemMacros b, c) }


exception MustExpandTextBeforeLinkException


exception ExternCommandDefinationNotFoundException of string * DebugInformation


let private systemCommands =  
    let parse str =
        TopLevels.topLevels |> ParserMonad.run str
        |> function
            | Ok (ExternDefination x) -> x
            | _ -> failwith "Bug here!"
    
    [ parse "- extern __text_begin character=null"
      parse "- extern __text_type text"
      parse "- extern __text_pushMark mark"
      parse "- extern __text_popMark mark"
      parse "- extern __text_end hasMore" ]


let linkToExternCommands (x: Dom) : Result<Dom> =
    let externs = systemCommands @ List.map fst x.Externs
    let linkSingleCommand (op, debugInfo) =
        match op with
        | Text _ -> Error MustExpandTextBeforeLinkException
        | CommandCall c -> 
            match 
                List.tryFind 
                    (fun (ExternCommand (name, _)) -> 
                        name = c.Callee) 
                    externs with
            | None -> 
                Error 
                <| ExternCommandDefinationNotFoundException 
                    (c.Callee, debugInfo)
            | Some (ExternCommand (_, param)) -> 
                Macro.matchArguments debugInfo param c
                |> Result.map (fun args -> 
                    let args = 
                        List.map 
                            (fun { Parameter = param } -> 
                                List.find (fst >> (=) param) args
                                |> snd) 
                            param

                    CommandCall { c with UnnamedArgs = args; NamedArgs = [] })
        | x -> Ok x
        |> Result.map (fun x -> x, debugInfo)

    let linkToExternCommands (sceneDef, block, debugInfo) =
        List.map linkSingleCommand block |> ParserMonad.switchResultList
        |> Result.map (fun block -> sceneDef, (block: Block), debugInfo)
        
    List.map linkToExternCommands x.Scenes 
    |> ParserMonad.switchResultList
    |> Result.map (fun scenes ->
        { x with Scenes = scenes })
