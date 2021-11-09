namespace YukimiScript.Parser

open YukimiScript.Parser.Parser
open YukimiScript.Parser.Elements
open YukimiScript.Parser.TypeChecker


type Dom =
    { HangingEmptyLine: DebugInformation list
      Externs: (ExternDefination * BlockParamTypes * DebugInformation) list
      Macros: (MacroDefination * BlockParamTypes * Block * DebugInformation) list
      Scenes: (SceneDefination * Block * DebugInformation) list }


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
          CurrentBlock: (Line * DebugInformation * Block) option }


    exception HangingOperationException of debugInfo: DebugInformation


    exception UnknownException


    exception SceneRepeatException of scene: string * debugInfo: DebugInformation seq


    exception MacroRepeatException of macro: string * debugInfo: DebugInformation seq


    exception ExternRepeatException of name: string * debugInfo: DebugInformation seq


    exception ExternCannotHasContentException of name: string * DebugInformation


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
                                match parametersTypeFromBlock x.Param block with
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
                                        | CommandCall c when c.Callee = "__type" -> true
                                        | _ -> false) block
                                then
                                    match parametersTypeFromBlock p block with
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
                      CurrentBlock = Some(line, labelDbgInfo, (x, debugInfo) :: block) }
                |> Ok

        let setLabel state line =
            { saveCurrentBlock state with
                  CurrentBlock = Some(line, debugInfo, []) }

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
        try
            let finalState =
                x
                |> Seq.indexed
                |> Seq.map
                    (fun (lineNumber, { Line = line; Comment = comment }) ->
                        line,
                        { LineNumber = lineNumber + 1
                          Comment = comment
                          File = fileName })
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
                    [ if debugInfo.Comment.IsSome then
                            EmptyLine, debugInfo

                      yield! Text.expandTextBlock x debugInfo ]
                | x -> [ x ])

        { x with
              Scenes = List.map (fun (def, block, d) -> def, mapBlock block, d) x.Scenes
              Macros = List.map (fun (def, t, b, d) -> def, t, mapBlock b, d) x.Macros }


    let expandUserMacros (x: Dom) =
        let macros =
            List.map (fun (a, _, b, _) -> a, b) x.Macros

        x.Scenes
        |> List.map
            (fun (sceneDef, block, debugInfo) ->
                Macro.expandBlock macros block
                |> Result.map (fun x -> sceneDef, x, debugInfo))
        |> ParserMonad.sequenceRL
        |> Result.map (fun scenes -> { x with Scenes = scenes })


    let expandSystemMacros (x: Dom) =
        { x with
              Scenes =
                  x.Scenes
                  |> List.map (fun (a, b, c) -> a, Macro.expandSystemMacros b, c) }


    exception MustExpandTextBeforeLinkException


    exception ExternCommandDefinationNotFoundException of string * DebugInformation


    let private systemCommands =
        let parse str =
            TopLevels.topLevels
            |> ParserMonad.run str
            |> function
                | Ok (ExternDefination x) -> x
                | _ -> failwith "Bug here!"

        [ parse "- extern __text_begin character=null"
          parse "- extern __text_type text"
          parse "- extern __text_pushMark mark"
          parse "- extern __text_popMark mark"
          parse "- extern __text_end hasMore" ]


    let linkToExternCommands (x: Dom) : Result<Dom, exn> =
        let externs = systemCommands @ List.map (fun (x, _, _) -> x) x.Externs

        let linkSingleCommand (op, debugInfo) =
            match op with
            | Text _ -> Error MustExpandTextBeforeLinkException
            | CommandCall c ->
                match List.tryFind (fun (ExternCommand (name, _)) -> name = c.Callee) externs with
                | None ->
                    Error
                    <| ExternCommandDefinationNotFoundException(c.Callee, debugInfo)
                | Some (ExternCommand (_, param)) ->
                    Macro.matchArguments debugInfo param c
                    |> Result.map
                        (fun args ->
                            let args =
                                List.map (fun { Parameter = param } -> List.find (fst >> (=) param) args |> snd) param

                            CommandCall
                                { c with
                                      UnnamedArgs = args
                                      NamedArgs = [] })
            | x -> Ok x
            |> Result.map (fun x -> x, debugInfo)

        let linkToExternCommands (sceneDef, block, debugInfo) =
            List.map linkSingleCommand block
            |> ParserMonad.sequenceRL
            |> Result.map (fun block -> sceneDef, (block: Block), debugInfo)

        List.map linkToExternCommands x.Scenes
        |> ParserMonad.sequenceRL
        |> Result.map (fun scenes -> { x with Scenes = scenes })
