module YukimiScript.CodeGen.PyMO

open YukimiScript.Parser
open YukimiScript.Parser.Elements
open System.Text
open System


let private genArg = function
    | String x -> Some x
    | Real x -> Some <| string x
    | Integer x -> Some <| string x
    | Symbol "true" -> Some "1"
    | Symbol "false" -> Some "0"
    | Symbol "cm0" -> Some "0"
    | Symbol "cm1" -> Some "1"
    | Symbol "cm2" -> Some "2"
    | Symbol "cm3" -> Some "3"
    | Symbol "cm4" -> Some "4"
    | Symbol "cm5" -> Some "5"
    | Symbol "cm6" -> Some "6"
    | Symbol "white" -> Some "#FFFFFF"
    | Symbol "black" -> Some "#000000"
    | Symbol "red" -> Some "#FF0000"
    | Symbol "green" -> Some "#00FF00"
    | Symbol "blue" -> Some "#0000FF"
    | Symbol "BG_ALPHA" -> Some "BG_ALPHA"
    | Symbol "BG_FADE" -> Some "BG_FADE"
    | Symbol "BG_NOFADE" -> Some "BG_NOFADE"
    | Symbol "null" -> None
    | Symbol x -> failwithf "Unsupported PyMO keyword %A." x


let private genArgUntyped = function
    | Symbol x -> x
    | String x -> x
    | Integer x -> string x
    | Real x -> string x


let private genArgs' genArg = 
    genArg
    >> function
        | [] -> ""
        | ls -> List.reduce (fun a b -> a + "," + b) ls


let private genArgs = genArgs' <| List.choose genArg
let private genArgsUntyped = genArgs' <| List.map genArgUntyped


type private ComplexCommand = ComplexCommand of string * string * (Constant list -> Constant list -> string)


let private gen'Untyped c = genArgsUntyped >> (+) ("#" + c + " ")


let private sayCommand = 
    ComplexCommand 
        ("__text_type", "__text_end", fun m _ -> 
            let character = match m[0] with | Symbol "null" -> None | x -> Some <| genArgUntyped x
            let text = m[1..] |> List.map genArgUntyped |> List.fold (+) ""
            "#say " + (match character with | None -> "" | Some x -> x + ",") + text)


let private complexCommands =
    let gen command a b = a @ b |> genArgs |> (+) ("#" + command + " ")
    let gen' c = genArgs >> (+) ("#" + c + " ")
    
    let genSel c argGroupSize = fun m d -> Integer (List.length m / argGroupSize) :: m @ d |> gen'Untyped c
    [ "chara_multi", "chara_multi_do", gen "chara"
      "chara_quake_multi", "chara_quake_multi_do", gen "chara_quake"
      "chara_down_multi", "chara_down_multi_do", gen "chara_down"
      "chara_up_multi", "chara_up_multi_do", gen "chara_up"
      "chara_y_multi", "chara_y_multi_do", fun multi d -> d[0] :: multi @ [d[1]] |> gen' "chara_y"
      "chara_anime", "chara_anime_do", fun multi d -> d @ multi |> gen' "chara_anime"
      "sel", "sel_do", fun multi d -> 
        let firstLine = "#sel " + genArgs (Integer (List.length multi) :: d)
        let allLines = (firstLine :: List.choose genArg multi)
        allLines |> List.reduce (fun a b -> a + "\n" + b)
      "select_text", "select_text_do", genSel "select_text" 1
      "select_var", "select_var_do", genSel "select_var" 2
      "select_img", "select_img_do", fun m d -> genSel "select_img" 3 (d[0] :: m) d.[1..]
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


type private CodeGenContext = 
    { Characters: Map<string, string>
      CurrentComplexCommand: (ComplexCommand * Constant list) option }


let private (|ComplexCommand'|_|) x = 
    Map.tryFind x complexCommands


let private simpleCommands =
    [ "text"
      "text_off"
      "waitkey"
      "title"
      "title_dsp"
      "chara_cls"
      "chara_pos"
      "bg"
      "flash"
      "quake"
      "fade_out"
      "fade_in"
      "movie"
      "textbox"
      "scroll"
      "chara_scroll"
      "anime_on"
      "anime_off"
      "goto"
      "change"
      "call"
      "ret"
      "wait"
      "wait_se"
      "bgm"
      "bgm_stop"
      "se"
      "se_stop"
      "vo"
      "load"
      "album"
      "music"
      "date"
      "config" ]
    |> Set.ofSeq


let private genCommand 
    (sb: StringBuilder) 
    context
    (call: IntermediateCommandCall) 
    : Result<CodeGenContext, string * DebugInformation> = 
    match context.CurrentComplexCommand with
    | Some (ComplexCommand (openCmd, closeCmd, gen) as cc, args) -> 
        match call.Callee with
        | "__text_begin" when openCmd = "__text_type" && call.Arguments[0] = Symbol "null" -> Ok context
        | "__text_end" when openCmd = "__text_type" && call.Arguments[0] = Symbol "true" -> 
            Ok { context with CurrentComplexCommand = Some (cc, args @ [Constant.String "\\n"]) }
        | c when c = openCmd -> 
            Ok { context with CurrentComplexCommand = Some (cc, args @ call.Arguments)}
        | c when c = closeCmd ->
            sb.AppendLine (gen args call.Arguments) |> ignore
            Ok { context with CurrentComplexCommand = None }
        | _ -> Error ("当你使用PyMO变参命令时，不应该在中间夹杂其他命令。", call.DebugInformation)
    | None -> 
        match call.Callee with
        | "__define_character" -> 
            Ok { context with 
                    Characters = 
                        Map.add 
                            (genArgUntyped call.Arguments[0]) 
                            (genArgUntyped call.Arguments[1]) 
                            context.Characters }
        | "if_goto" -> 
            let left, op, right, label = 
                genArgUntyped call.Arguments[0], 
                genArgUntyped call.Arguments[1],
                genArgUntyped call.Arguments[2],
                genArgUntyped call.Arguments[3]
            
            let op = 
                match op with
                | "eq" -> "="
                | "ne" -> "!="
                | "gt" -> ">"
                | "ge" -> ">="
                | "lt" -> "<"
                | "le" -> "<="
                | _ -> failwith ""

            sb.Append("#if ").Append(left).Append(op).Append(right).Append(",goto ").AppendLine(label)
            |> ignore

            Ok context

        | "__text_begin" -> 
            match call.Arguments[0] with
            | Symbol "null" as n -> Ok { context with CurrentComplexCommand = Some (sayCommand, [n])}
            | Symbol x -> 
                match Map.tryFind x context.Characters with
                | None -> Error ("未能找到角色定义 " + x + " 。", call.DebugInformation)
                | Some x -> Ok { context with CurrentComplexCommand = Some (sayCommand, [Constant.String x])}
            | _ -> failwith ""
        | "__text_type" -> Error ("错误的__text_type用法。", call.DebugInformation)
        | "__text_pushMark" | "__text_popMark" -> Error ("PyMO不支持高级文本语法。", call.DebugInformation)
        | "__text_end" -> Error ("错误的__text_end用法。", call.DebugInformation)

        | c when Set.contains c commandsWithUntypedSymbol -> 
            sb.Append('#').Append(c).Append(' ').AppendLine(genArgsUntyped call.Arguments) |> ignore
            Ok context
        | ComplexCommand' complex -> 
            Ok { context with CurrentComplexCommand = Some (complex, call.Arguments) }
        | simple when Set.contains simple simpleCommands -> 
            sb.Append('#').Append(simple).Append(' ').AppendLine(genArgs call.Arguments) |> ignore
            Ok context
        | x -> Error ("不能在这里使用的命令" + x + "。", call.DebugInformation)


let private generateScene scene context (sb: StringBuilder) =
    sb
        .Append("#label ")
        .AppendLine(scene.Scene.Name)
    |> ignore

    scene.Block 
    |> List.fold 
        (fun (context, success) -> 
            genCommand sb context
            >> function
                | Ok context -> context, success
                | Error (msg, dbg) -> 
                    ErrorStringing.header dbg + msg
                    |> Console.WriteLine
                    context, false)
        (context, true)
    |> function
        | { CurrentComplexCommand = Some (ComplexCommand (o, e, _), _) } as context, success -> 
            ErrorStringing.header scene.DebugInformation
             + "在此场景的末尾，应当使用"
             + e 
             + "命令来结束"
             + o
             + "变参命令组。"
            |> Console.WriteLine
            context, success
        | context -> context

    
let generateScript (Intermediate scenes) scriptName =
    let sb = new StringBuilder ()
    let scenes, (context, success) = 
        let initContext = { Characters = Map.empty; CurrentComplexCommand = None }
        match List.tryFind (fun x -> x.Scene.Name = "$init") scenes with
        | None -> scenes, (initContext, true)
        | Some init -> 
            List.except [init] scenes,
            generateScene init initContext sb
    
    match success, List.tryFind (fun x -> x.Scene.Name = scriptName) scenes with
    | false, _ -> Error ()
    | true, None -> Console.WriteLine "未能找到入口点场景。"; Error ()
    | true, Some entryPoint ->  
        (entryPoint :: List.except [entryPoint] scenes)
        |> List.fold (fun success scene -> 
            let succ' = generateScene scene context sb |> snd
            success && succ') true
        |> function
            | true -> Ok <| sb.ToString ()
            | false -> Error ()
            
        