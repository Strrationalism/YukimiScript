module YukimiScript.CodeGen.PyMO

open YukimiScript.Parser
open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.Elements
open System.Text
open System


let private genArg = function
    | String x -> Some x
    | Real x -> Some <| string x
    | Integer x -> Some <| string x
    | Symbol "true" -> Some "1"
    | Symbol "false" -> Some "0"
    | Symbol "a" -> Some "a"
    | Symbol "BG_VERYFAST" -> Some "BG_VERYFAST"
    | Symbol "BG_FAST" -> Some "BG_FAST"
    | Symbol "BG_NORMAL" -> Some "BG_NORMAL"
    | Symbol "BG_SLOW" -> Some "BG_SLOW"
    | Symbol "BG_VERYSLOW" -> Some "BG_VERYSLOW"
    | Symbol "cm0" -> Some "0"
    | Symbol "cm1" -> Some "1"
    | Symbol "cm2" -> Some "2"
    | Symbol "cm3" -> Some "3"
    | Symbol "cm4" -> Some "4"
    | Symbol "cm5" -> Some "5"
    | Symbol "cm6" -> Some "6"
    | Symbol "BG_ALPHA" -> Some "BG_ALPHA"
    | Symbol "BG_FADE" -> Some "BG_FADE"
    | Symbol "BG_NOFADE" -> Some "BG_NOFADE"
    | Symbol "null" -> None
    | Symbol x -> failwith <| "Unsupported PyMO keyword " +  x + "."


let private genArgUntyped = function
    | Symbol x -> x
    | String x -> x
    | Integer x -> string x
    | Real x -> string x


let private checkSceneName (name: string) =
    match name with
    | "$init" -> true
    | name ->
        if name.Contains '.'
        then false
        else
            let p = seq { yield! ['0'..'9']; yield! ['a' .. 'z']; yield! ['A' .. 'Z']; yield! [ '_'; '-' ] }
            match run name <| oneOrMore (inRange p) with
            | Ok _ -> true
            | _ -> false


let private genArgs' genArg =
    genArg
    >> function
        | [] -> ""
        | ls -> List.reduce (fun a b -> a + "," + b) ls


let private colorArg = function
    | Symbol "white" -> Constant.String "#FFFFFF"
    | Symbol "black" -> Constant.String "#000000"
    | Symbol "red" -> Constant.String "#FF0000"
    | Symbol "green" -> Constant.String "#00FF00"
    | Symbol "blue" -> Constant.String "#0000FF"
    | Integer i ->
        let mutable hex = Math.Clamp(i, 0, 0xFFFFFF).ToString("X")
        while hex.Length < 6 do hex <- "0" + hex
        Constant.String <| "#" + hex
    | _ -> failwith ""


let private genArgs = genArgs' <| List.choose genArg
let private genArgsUntyped = genArgs' <| List.map genArgUntyped


type private ComplexCommand = ComplexCommand of string * string * (Constant list -> Constant list -> string)


let private gen'Untyped c = genArgsUntyped >> (+) ("#" + c + " ")


let private sayCommand =
    ComplexCommand
        ("__text_type", "__text_end", fun m _ ->
            let character = match m.[0] with | Symbol "null" -> None | x -> Some <| genArgUntyped x
            let text = m.[1..] |> List.map genArgUntyped |> List.fold (+) ""
            "#say " + (match character with | None -> "" | Some x -> x + ",") + text)


let private complexCommands =
    let gen command a b = a @ b |> genArgs |> (+) ("#" + command + " ")
    let gen' c = genArgs >> (+) ("#" + c + " ")

    let genSel c argGroupSize = fun m d ->
        let d =
            if List.length d >= 4
            then
                let arr = List.toArray d
                arr.[4] <- colorArg d.[4]
                List.ofArray arr
            else d
        Integer (List.length m / argGroupSize) :: m @ d |> gen'Untyped c
    [ "chara_multi", "chara_multi_do", gen "chara"
      "chara_quake_multi", "chara_quake_multi_do", gen "chara_quake"
      "chara_down_multi", "chara_down_multi_do", gen "chara_down"
      "chara_up_multi", "chara_up_multi_do", gen "chara_up"
      "chara_y_multi", "chara_y_multi_do", fun multi d -> d.[0] :: multi @ [d.[1]] |> gen' "chara_y"
      "chara_anime", "chara_anime_do", fun multi d -> d @ multi |> gen' "chara_anime"
      "sel", "sel_do", fun multi d ->
        let firstLine = "#sel " + genArgs (Integer (List.length multi) :: d)
        let allLines = (firstLine :: List.choose genArg multi)
        allLines |> List.reduce (fun a b -> a + "\n" + b)
      "select_text", "select_text_do", genSel "select_text" 1
      "select_var", "select_var_do", genSel "select_var" 2
      "select_img", "select_img_do", fun m d -> genSel "select_img" 3 (d.[0] :: m) d.[1..]
      "select_imgs", "select_imgs_do", genSel "select_imgs" 4
      (let (ComplexCommand (x, y, z)) = sayCommand in x, y, z) ]
    |> Seq.map (fun x -> (let (k, _, _) = x in k), ComplexCommand x)
    |> Map.ofSeq


let private commandsWithUntypedSymbol =
    [ "set"
      "add"
      "sub"
      "rand" ]
    |> Set.ofSeq


type Scope =
    | IfScope of ifs: (string * StringBuilder) list * def: StringBuilder option


type private CodeGenContext =
    { Characters: Map<string, string>
      CurrentComplexCommand: (ComplexCommand * Constant list) option
      ScopeStack: Scope list
      Inc: int
      GenExitLabel: bool ref }


let private (|ComplexCommand'|_|) x =
    Map.tryFind x complexCommands


let private colorCommands =
    [ "text", 5
      "flash", 0
      "fade_out", 0
      "date", 3 ]
    |> Map.ofList


let private getDebugString (table: ref<Map<string, int>>) str =
    match Map.tryFind str table.Value with
    | Some x -> x
    | None ->
        let curId = Map.count table.Value
        table.Value <- Map.add str curId table.Value
        curId


let private genCommand
    dbgStrs
    genDbg
    (sbRoot: StringBuilder)
    context
    (call: IntermediateCommandCall)
    : Result<CodeGenContext, string * DebugInfo> =

    let getScopeSb =
        function
        | [] -> sbRoot
        | IfScope ((_, sb) :: _, None) :: _ -> sb
        | IfScope (_, Some sb) :: _ -> sb
        | IfScope _ :: _ -> failwith "Invalid if scope!"

    let sb = getScopeSb context.ScopeStack

    if genDbg && call.Callee <> "__text_begin" && call.Callee <> "__text_end" then
        sb.Append (";YKMDBG") |> ignore

        sb.Append (";C") |> ignore
        sb.Append (getDebugString dbgStrs call.Callee) |> ignore

        for arg in call.Arguments do
                match arg with
                | Constant.String x -> ";AS" + string (getDebugString dbgStrs x)
                | Symbol x -> ";As" + string (getDebugString dbgStrs x)
                | Integer i -> ";Ai" + string i
                | Real r -> ";Ar" + string r
                |> sb.Append
                |> ignore

        sb.Append (";E") |> ignore

        let mutable d = Some call.DebugInfo
        while Option.isSome d do
            let dbgInfo = d.Value
            sb.Append (";L") |> ignore
            sb.Append (dbgInfo.LineNumber) |> ignore
            sb.Append (";F") |> ignore
            sb.Append (getDebugString dbgStrs dbgInfo.File) |> ignore

            for i in dbgInfo.MacroVars do
                let var = fst i
                match snd i with
                | Constant.String x ->
                    ";VS" + var + "=" + string (getDebugString dbgStrs x)
                | Symbol x ->
                    ";Vs" + var + "=" + string (getDebugString dbgStrs x)
                | Integer x -> ";Vi" + var + "=" + string x
                | Real x -> ";Vr" + var + "=" + string x
                |> sb.Append
                |> ignore

            match dbgInfo.Scope with
            | None -> ()
            | Some (Choice2Of2 a) ->
                sb.Append(";S").Append(getDebugString dbgStrs a.Name) |> ignore
            | Some (Choice1Of2 a) ->
                sb.Append(";M").Append(getDebugString dbgStrs a.Name) |> ignore

            sb.Append (";E") |> ignore

            d <- dbgInfo.Outter
        sb.AppendLine () |> ignore

    let genComplexCommandError () =
        Error ("当你使用PyMO变参命令时，不应该在中间夹杂其他命令。", call.DebugInfo)

    let condStr left op right =
        let left, op, right =
            genArgUntyped left,
            genArgUntyped op,
            genArgUntyped right

        let op =
            match op with
            | "eq" -> "="
            | "ne" -> "!="
            | "gt" -> ">"
            | "ge" -> ">="
            | "lt" -> "<"
            | "le" -> "<="
            | _ -> failwith ""

        left + op + right

    match context.CurrentComplexCommand with
    | Some (ComplexCommand (openCmd, closeCmd, gen) as cc, args) ->
        match call.Callee with
        | "__text_begin" when openCmd = "__text_type" && call.Arguments.[0] = Symbol "null" -> Ok context
        | "__text_end" when openCmd = "__text_type" && call.Arguments.[0] = Symbol "true" ->
            Ok { context with CurrentComplexCommand = Some (cc, args @ [Constant.String "\\n"]) }
        | c when c = openCmd ->
            Ok { context with CurrentComplexCommand = Some (cc, args @ call.Arguments)}
        | c when c = closeCmd ->
            sb.AppendLine (gen args call.Arguments) |> ignore
            Ok { context with CurrentComplexCommand = None }
        | _ -> genComplexCommandError ()
    | None ->
        match call.Callee with
        | "__define_character" ->
            Ok { context with
                    Characters =
                        Map.add
                            (genArgUntyped call.Arguments.[0])
                            (genArgUntyped call.Arguments.[1])
                            context.Characters }
        | "exit" ->
            sb.AppendLine ("#goto EXIT") |> ignore
            context.GenExitLabel.Value <- true
            Ok context
        | "if" ->
            let condStr = condStr call.Arguments[0] call.Arguments[1] call.Arguments[2]
            if context.CurrentComplexCommand.IsSome
            then genComplexCommandError ()
            else
                { context with
                    ScopeStack =
                        IfScope ([condStr, StringBuilder ()], None)::context.ScopeStack }
                |> Ok
        | "elif" ->
            let condStr = condStr call.Arguments[0] call.Arguments[1] call.Arguments[2]
            if context.CurrentComplexCommand.IsSome
            then genComplexCommandError ()
            else
                match context.ScopeStack with
                | IfScope (x, None)::ls ->
                    { context with
                        ScopeStack = IfScope ((condStr, StringBuilder ())::x, None)::ls }
                    |> Ok
                | _ -> Error ("这里不应该使用elif命令。", call.DebugInfo)
        | "else" ->
            if context.CurrentComplexCommand.IsSome
            then genComplexCommandError ()
            else
                match context.ScopeStack with
                | IfScope (ifs, None)::ls ->
                    { context with ScopeStack = IfScope (ifs, Some <| StringBuilder ()) :: ls }
                    |> Ok
                | _ -> Error ("这里不应该使用else命令。", call.DebugInfo)
        | "endif" ->
            if context.CurrentComplexCommand.IsSome
            then genComplexCommandError ()
            else
                match context.ScopeStack with
                | (IfScope (ifs, def)) :: outter ->
                    let sb = getScopeSb outter
                    let ifs = List.rev ifs
                    ifs
                    |> List.iteri (fun i (cond, _) ->
                        sb
                            .Append("#if ")
                            .Append(cond)
                            .Append(",goto ")
                            .Append("IF_")
                            .AppendLine(string <| i + context.Inc)
                        |> ignore)

                    let endOfIfLabel = "IF_END_" + string context.Inc

                    if def.IsSome then
                        sb
                            .AppendLine(def.Value.ToString ())
                        |> ignore

                    sb.Append("#goto ").AppendLine(endOfIfLabel) |> ignore

                    ifs
                    |> List.iteri (fun i (_, body) ->
                        sb
                            .Append("#label IF_")
                            .AppendLine(string <| i + context.Inc)
                            .AppendLine(body.ToString ())
                            .Append("#goto ")
                            .AppendLine(endOfIfLabel)
                        |> ignore)

                    sb.Append("#label ").AppendLine(endOfIfLabel) |> ignore
                    Ok { context with ScopeStack = outter; Inc = context.Inc + List.length ifs }

                | _ -> Error ("这里不应该使用endif命令。", call.DebugInfo)

        | "if_goto" ->
            let condStr, label =
                condStr
                    call.Arguments[0]
                    call.Arguments[1]
                    call.Arguments[2],
                genArgUntyped call.Arguments.[3]


            sb.Append("#if ").Append(condStr).Append(",goto SCN_").AppendLine(label)
            |> ignore

            Ok context

        | "goto" ->
            sb.Append("#goto SCN_").AppendLine(genArgUntyped call.Arguments[0]) |> ignore
            Ok context

        | "__text_begin" ->
            match call.Arguments.[0] with
            | Symbol "null" as n -> Ok { context with CurrentComplexCommand = Some (sayCommand, [n])}
            | String x ->
                match Map.tryFind x context.Characters with
                | None -> Ok { context with CurrentComplexCommand = Some (sayCommand, [Constant.String x])}
                | Some x -> Ok { context with CurrentComplexCommand = Some (sayCommand, [Constant.String x])}
            | _ -> failwith ""
        | "__text_type" -> Error ("错误的__text_type用法。", call.DebugInfo)
        | "__text_pushMark" | "__text_popMark" -> Error ("PyMO不支持高级文本语法。", call.DebugInfo)
        | "__text_end" -> Error ("错误的__text_end用法。", call.DebugInfo)
        | c when Set.contains c commandsWithUntypedSymbol ->
            sb.Append('#').Append(c).Append(' ').AppendLine(genArgsUntyped call.Arguments) |> ignore
            Ok context
        | ComplexCommand' complex ->
            Ok { context with CurrentComplexCommand = Some (complex, call.Arguments) }
        | simple ->
            let simple =
                if simple = "chara_scroll_complex"
                then "chara_scroll"
                else simple

            let args =
                match Map.tryFind simple colorCommands with
                | None -> call.Arguments
                | Some x ->
                    let arg = Array.ofList call.Arguments
                    arg.[x] <- colorArg arg.[x]
                    List.ofArray arg
            sb.Append('#').Append(simple).Append(' ').AppendLine(genArgs args) |> ignore
            Ok context


let private generateScene strings genDbg (scene: IntermediateScene) context (sb: StringBuilder) =

    if genDbg then
        sb.AppendLine(
            ";YKMDBG" +
            ";L" +
            string scene.DebugInfo.LineNumber +
            ";F" + string (getDebugString strings scene.DebugInfo.File) +
            ";S" + string (getDebugString strings scene.Name))
        |> ignore


    sb
        .Append("#label SCN_")
        .AppendLine(scene.Name)
    |> ignore

    match checkSceneName scene.Name with
    | false ->
        ErrorStringing.header scene.DebugInfo
        + "场景名称 " + scene.Name
        + " 非法，在PyMO中只可以使用由字母、数字和下划线组成的场景名。"
        |> Console.WriteLine
        context, false
    | true ->
        scene.Block
        |> List.fold
            (fun (context, success) ->
                genCommand strings genDbg sb context
                >> function
                    | Ok context -> context, success
                    | Error (msg, dbg) ->
                        ErrorStringing.header dbg + msg
                        |> Console.WriteLine
                        context, false)
            (context, true)
        |> function
            | { CurrentComplexCommand = Some (ComplexCommand (o, e, _), _) } as context, _ ->
                ErrorStringing.header scene.DebugInfo
                 + "在此场景的末尾，应当使用"
                 + e
                 + "命令来结束"
                 + o
                 + "变参命令组。"
                |> Console.WriteLine
                context, false
            | { ScopeStack = _::_ } as context, _ ->
                ErrorStringing.header scene.DebugInfo
                 + "在场景的末尾应当结束当前的if区域。"
                |> Console.WriteLine
                context, false
            | c -> c


let generateScript genDbg (Intermediate scenes) scriptName =
    let sb = new StringBuilder ()
    let strings: ref<Map<string, int>> = ref Map.empty

    let scenes, (context, success) =
        let initContext =
          { Characters = Map.empty
            CurrentComplexCommand = None
            ScopeStack = []
            Inc = 0
            GenExitLabel = ref false }
        match List.tryFind (fun (x: IntermediateScene) -> x.Name = "$init") scenes with
        | None -> scenes, (initContext, true)
        | Some init ->
            List.except [init] scenes,
            generateScene strings genDbg init initContext sb

    match success, List.tryFind (fun (x: IntermediateScene) -> x.Name = scriptName) scenes with
    | false, _ -> Error ()
    | true, None -> Console.WriteLine "未能找到入口点场景。"; Error ()
    | true, Some entryPoint ->
        (entryPoint :: List.except [entryPoint] scenes)
        |> List.fold (fun success scene ->
            let succ' = generateScene strings genDbg scene context sb |> snd
            success && succ') true
        |> function
            | false -> Error ()
            | true ->
                let debugSymbolTable =
                    seq { 0 .. Map.count strings.Value - 1 }
                    |> Seq.map (fun x ->
                        strings.Value
                        |> Map.pick (fun s x' ->
                            if x = x'
                            then Some s
                            else None))
                    |> Seq.fold
                        (fun acc x ->
                            acc + ";YKMDBG;P" + Constants.string2literal x + "\n")
                        ""

                if context.GenExitLabel.Value then
                    sb  .AppendLine()
                        .AppendLine("#label EXIT")
                    |> ignore

                Ok <| debugSymbolTable + sb.ToString ()

