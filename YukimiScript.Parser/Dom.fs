namespace YukimiScript.Parser

open YukimiScript.Parser.Parser
open YukimiScript.Parser.Elements
open YukimiScript.Parser.TypeChecker
open YukimiScript.Parser.Utils


type Dom =
    { HangingEmptyLine: DebugInfo list
      Externs: (ExternDefination * BlockParamTypes * DebugInfo) list
      Macros: (MacroDefination * BlockParamTypes * Block * DebugInfo) list
      Scenes: (SceneDefination * Block * DebugInfo) list }


module Dom =
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
          CurrentBlock: (Line * DebugInfo * Block) option }


    exception HangingOperationException of debugInfo: DebugInfo


    exception UnknownException


    exception SceneRepeatException of scene: string * debugInfo: DebugInfo seq


    exception MacroRepeatException of macro: string * debugInfo: DebugInfo seq


    exception ExternRepeatException of name: string * debugInfo: DebugInfo seq


    exception ExternCannotHasContentException of name: string * DebugInfo


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
                                let block = List.rev block
                                match Macro.parametersTypeFromBlock x.Param block with
                                | Ok t -> (x, t, block, debugInfo) :: state.Result.Macros
                                | Error e -> raise e
                            | _ -> state.Result.Macros
                        Scenes =
                            match label with
                            | SceneDefination x ->
                                (x, List.rev block, debugInfo)
                                :: state.Result.Scenes
                            | _ -> state.Result.Scenes
                        Externs =
                            match label with
                            | ExternDefination (ExternCommand (n, p)) ->
                                if
                                    List.forall (fst >> function
                                        | CommandCall c when c.Callee = "__type" || c.Callee = "__type_symbol" -> true
                                        | EmptyLine -> true
                                        | _ -> false) block
                                then
                                    match Macro.parametersTypeFromBlock p block with
                                    | Ok t -> 
                                        (ExternCommand (n, p), t, debugInfo) 
                                        :: state.Result.Externs
                                    | Error e -> raise e
                                else raise <| ExternCannotHasContentException (n, debugInfo)
                            | _ -> state.Result.Externs} }


    let private analyzeFold state (line, debugInfo) =

        let pushOperation x =
            match state.CurrentBlock with
            | None -> Error <| HangingOperationException debugInfo
            | Some (line, labelDbgInfo, block) ->
                { state with
                      CurrentBlock = 
                        let debugInfo = 
                            { debugInfo with Scope = labelDbgInfo.Scope }
                        Some(line, labelDbgInfo, (x, debugInfo) :: block) }
                |> Ok

        let setLabel state line =
            { saveCurrentBlock state with
                  CurrentBlock = 
                    let debugInfoScope = 
                        match line with
                        | SceneDefination scene -> Some (Choice2Of2 scene)
                        | MacroDefination macro -> Some (Choice1Of2 macro)
                        | _ -> None
                    Some (line, { debugInfo with Scope = debugInfoScope }, []) }

        match line with
        | Line.EmptyLine ->
            match state.CurrentBlock with
            | Some _ -> pushOperation EmptyLine
            | None ->
                { state with
                      Result =
                          { state.Result with
                                HangingEmptyLine = debugInfo :: state.Result.HangingEmptyLine } }
                |> Ok

        | Line.CommandCall x -> pushOperation <| CommandCall x
        | Line.Text x -> pushOperation <| Text x
        | SceneDefination scene -> Ok <| setLabel state (SceneDefination scene)
        | MacroDefination macro -> Ok <| setLabel state (MacroDefination macro)
        | ExternDefination extern' ->  Ok <| setLabel state (ExternDefination extern')


    exception CannotDefineSceneInLibException of string


    let analyze (fileName: string) (x: Parsed seq) : Result<Dom, exn> =
        let filePath = System.IO.Path.GetFullPath fileName
        try
            let finalState =
                x
                |> Seq.indexed
                |> Seq.map
                    (fun (lineNumber, { Line = line }) ->
                        line,
                        { LineNumber = lineNumber + 1
                          File = filePath
                          Scope = None
                          Outter = None
                          MacroVars = [] })
                |> Seq.fold
                    (fun state x -> Result.bind (fun state -> analyzeFold state x) state)
                    (Ok { Result = empty; CurrentBlock = None })
                |> Result.map saveCurrentBlock

            finalState
            |> Result.map
                (fun x ->
                    { Scenes = List.rev x.Result.Scenes
                      Macros = List.rev x.Result.Macros
                      Externs = List.rev x.Result.Externs
                      HangingEmptyLine = List.rev x.Result.HangingEmptyLine })
        with e -> Error e

    let expandTextCommands (x: Dom) : Dom =
        let mapBlock =
            List.collect (function
                | Text x, debugInfo ->
                    Text.expandTextBlock x debugInfo
                | x -> [ x ])

        { x with
              Scenes = List.map (fun (def, block, d) -> def, mapBlock block, d) x.Scenes
              Macros = List.map (fun (def, t, b, d) -> def, t, mapBlock b, d) x.Macros }


    let expandUserMacros (x: Dom) =
        let macros =
            List.map (fun (a, t, b, _) -> a, t, b) x.Macros

        let callSystemCallback name args sceneDebugInfo =
            if 
                x.Macros |> List.exists (fun (a, _, _, _) -> a.Name = name) ||
                x.Externs |> List.exists (fun ((ExternCommand (x, _)), _, _) -> x = name)
                then
                    Some begin
                        CommandCall { Callee = name
                                      UnnamedArgs = args
                                      NamedArgs = [] },
                        sceneDebugInfo
                    end
                else None
                    
        x.Scenes
        |> List.map
            (fun (sceneDef, block, debugInfo) ->
                let lastDebugInfo =
                    List.tryLast block
                    |> Option.map snd
                    |> Option.defaultValue debugInfo

                let beforeSceneCall, afterSceneCall =
                    match sceneDef.Inherit with
                    | None -> 
                        callSystemCallback 
                            "__callback_scene_before"
                            [ Constant <| String sceneDef.Name ]
                            debugInfo,
                        callSystemCallback
                            "__callback_scene_after"
                            [ Constant <| String sceneDef.Name ]
                            lastDebugInfo
                    | Some inheritScene ->
                        callSystemCallback
                            "__callback_scene_inherit_before"
                            [ Constant <| String sceneDef.Name;
                              Constant <| String inheritScene ]
                            debugInfo,
                        callSystemCallback
                            "__callback_scene_inherit_after"
                            [ Constant <| String sceneDef.Name;
                              Constant <| String inheritScene ]
                            lastDebugInfo

                let block = 
                    Option.toList beforeSceneCall
                    @ block @ 
                    Option.toList afterSceneCall
                
                Macro.expandBlock macros block
                |> Result.map (fun x -> sceneDef, x, debugInfo))
        |> Result.transposeList
        |> Result.map (fun scenes -> { x with Scenes = scenes })


    let expandSystemMacros (x: Dom) =
        { x with
              Scenes =
                  x.Scenes
                  |> List.map (fun (a, b, c) -> a, Macro.expandSystemMacros b, c) }


    exception MustExpandTextBeforeLinkException


    exception ExternCommandDefinationNotFoundException of string * DebugInfo


    let private systemCommands : (ExternDefination * BlockParamTypes) list =
        let parse str =
            TopLevels.topLevels
            |> ParserMonad.run str
            |> function
                | Ok (ExternDefination x) -> x
                | _ -> failwith "Bug here!"

        [ parse "- extern __text_begin character=null", [ 
                "character", ParameterType ("string | null", set [ String'; ExplicitSymbol' "null" ]) ]
          parse "- extern __text_type text", [ "text", Types.string ]
          parse "- extern __text_pushMark mark", [ "mark", Types.symbol ]
          parse "- extern __text_popMark mark", [ "mark", Types.symbol ]
          parse "- extern __text_end hasMore", [ "hasMore", Types.bool ] ]


    let linkToExternCommands (x: Dom) : Result<Dom, exn> =
        let externs = systemCommands @ List.map (fun (x, t, _) -> x, t) x.Externs
            
        let linkSingleCommand (op, debugInfo) =
            match op with
            | Text _ -> Error MustExpandTextBeforeLinkException
            | CommandCall c ->
                match List.tryFind (fun (ExternCommand (name, _), _) -> name = c.Callee) externs with
                | None ->
                    Error
                    <| ExternCommandDefinationNotFoundException(c.Callee, debugInfo)
                | Some (ExternCommand (_, param), t) ->
                    Macro.matchArguments debugInfo param c
                    |> Result.bind (checkApplyTypeCorrect debugInfo t)
                    |> Result.bind
                        (fun args ->
                            let args = Macro.sortArgs param args
                            let args = 
                                List.map (fun (n, a) -> 
                                    Macro.commandArgToConstant 
                                        args (Some n) a debugInfo) args
                                |> Result.transposeList

                            args
                            |> Result.map (fun args -> 
                                CommandCall
                                    { c with
                                        UnnamedArgs = List.map Constant args
                                        NamedArgs = [] } ))
            | x -> Ok x
            |> Result.map (fun x -> x, debugInfo)

        let linkToExternCommands (sceneDef, block, debugInfo) =
            List.map linkSingleCommand block
            |> Result.transposeList
            |> Result.map (fun block -> sceneDef, (block: Block), debugInfo)

        List.map linkToExternCommands x.Scenes
        |> Result.transposeList
        |> Result.map (fun scenes -> { x with Scenes = scenes })
