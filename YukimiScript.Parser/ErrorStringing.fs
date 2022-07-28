module YukimiScript.Parser.ErrorStringing

open YukimiScript.Parser.Basics
open YukimiScript.Parser.Constants
open YukimiScript.Parser.Dom
open YukimiScript.Parser.Macro
open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.TopLevels
open YukimiScript.Parser.Diagram
open YukimiScript.Parser.TypeChecker
open System.IO


type ErrorStringing = exn -> string


let header (debug: Elements.DebugInfo) =
    Path.GetFileName(debug.File)
    + "("
    + string debug.LineNumber
    + "):"

let rec schinese: ErrorStringing =
    function
    | TypeCheckFailedException (d, i, ParameterType (name, _), a) ->
        header d 
        + "第" + string (i + 1) + "个参数的类型应当为" + name + "，但传入了" +
        match a with
        | Int' -> "int"
        | Real' -> "real"
        | String' -> "string"
        | ExplicitSymbol' _ | Symbol' -> "symbol"
        + "。"
    | IsNotAType (x, d) ->
        header d + x + "不是一个类型。"
    | CannotGetParameterException ls ->
        ls
        |> List.map (fun (x, d) -> header d + "不能获得" + x + "的类型。")
        |> List.reduce (fun a b -> a + System.Environment.NewLine + b)
    | InvalidSymbolException -> "非法符号。"
    | InvalidStringCharException x -> "字符串中存在非法字符\"" + x + "\"。"
    | HangingOperationException debug -> header debug + "存在悬浮操作。"
    | SceneRepeatException (scene, dbgs) ->
        "重复定义了场景"
        + scene
        + "，分别在以下位置："
        + System.Environment.NewLine
        + (dbgs
           |> Seq.map (fun x -> "    " + x.File + "(" + string x.LineNumber + ")")
           |> Seq.reduce (fun a b -> a + System.Environment.NewLine + b))

    | MacroRepeatException (macro, dbgs) ->
        "重复定义了宏"
        + macro
        + "，分别在以下位置："
        + System.Environment.NewLine
        + (dbgs
           |> Seq.map (fun x -> "    " + x.File + "(" + string x.LineNumber + ")")
           |> Seq.reduce (fun a b -> a + System.Environment.NewLine + b))

    | ExternRepeatException (ex, dbgs) ->
        "重复定义了外部元素"
        + ex
        + "，分别在以下位置："
        + System.Environment.NewLine
        + (dbgs
           |> Seq.map (fun x -> "    " + x.File + "(" + string x.LineNumber + ")")
           |> Seq.reduce (fun a b -> a + System.Environment.NewLine + b))

    | MustExpandTextBeforeLinkException -> "必须先展开文本元素再连接外部元素。"
    | ExternCannotHasContentException (x, d) -> header d + "外部定义中不能包含内容：" + x + "。"
    | ExternCommandDefinationNotFoundException (ex, debug) -> header debug + "发现了对外部元素" + ex + "的引用，但未找到此引用。"
    | ParamRepeatedException (parent, param) -> "在" + parent + "中发现重复定义的参数" + param + "。"
    | NoMacroMatchedException -> "没有匹配的宏。"
    | ArgumentsTooMuchException (debug, c) -> header debug + "对" + c.Callee + "传入了过多参数。"
    | ArgumentRepeatException (debug, cmd, param) ->
        header debug
        + "对"
        + cmd.Callee
        + "传入了重复的参数"
        + param
        + "。"
    | ArgumentUnmatchedException (debug, cmd, param) ->
        header debug
        + "不能为"
        + cmd.Callee
        + "匹配参数"
        + param
        + "。"
    | ParseUnfinishedException str -> "解析未能完成，剩余内容为" + str + "。"
    | NotLiteralException str -> str + "不符合Literal。"
    | NotInRangeException _ -> "不在范围内。"
    | PredicateFailedException -> "不符合条件。"
    | EndException -> "遇到结尾。"
    | ExpectSymbolException x -> "需要一个" + x + "，但并未传入。"
    | InvalidTopLevelException topLevel -> "未知的顶级定义" + topLevel + "。"
    | CannotDefineSceneInLibException debug -> debug + ":不能在lib中定义scene。"
    | DiagramMacroErrorException d -> header d + "__diagram_link_to宏使用方式错误。"
    | CannotFindSceneException x -> "不能找到场景\"" + x + "\"的定义。"
    | MacroInnerException (debugInfo, x) ->
        header debugInfo + "在展开宏时遇到以下错误：" + System.Environment.NewLine +
        begin 
            (schinese x).Split '\n' 
            |> Array.map (fun x -> "    " + x.Trim('\r')) 
            |> Array.reduce (fun a b -> a + System.Environment.NewLine + b)
        end
    | e -> "未知错误" + e.Message
