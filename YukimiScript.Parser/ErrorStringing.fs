module YukimiScript.Parser.ErrorStringing

open YukimiScript.Parser.Basics
open YukimiScript.Parser.Constants
open YukimiScript.Parser.Dom
open YukimiScript.Parser.Macro
open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.TopLevels


type ErrorStringing = exn -> string


let schinese : ErrorStringing =
    function
    | InvalidSymbolException -> "非法符号。"
    | InvalidStringCharException x -> "字符串中存在非法字符\"" + x + "\"。"
    | HangingOperationException debug -> 
        "在第" + string debug.LineNumber + "行存在悬浮操作。"
    | SceneRepeatException (debug, scene) ->
        "在第" + string debug.LineNumber + "行重复定义了场景" + scene + "。"
    | MacroRepeatException (debug, macro) ->
        "在第" + string debug.LineNumber + "行重复定义了宏" + macro + "。"
    | ExternRepeatException (debug, ex) ->
        "在第" + string debug.LineNumber + "行重复定义了外部元素" + ex + "。"
    | MustExpandTextBeforeLinkException -> "必须先展开文本元素再连接外部元素。"
    | ExternCommandDefinationNotFoundException (ex, debug) ->
        "在第" + string debug.LineNumber + "行发现了对外部元素" + ex + "的引用，但未找到此引用。"
    | ParamRepeatedException (parent, param) ->
        "在" + parent + "中发现重复定义的参数" + param + "。"
    | NoMacroMatchedException -> "没有匹配的宏。"
    | ArgumentsTooMuchException (debug, macro, _) ->
        "在第" + string debug.LineNumber + "行发现对" + macro.Name + "传入了过多参数。"
    | ArgumentRepeatException (debug, cmd, param) ->
        "在第" + string debug.LineNumber + "行发现对" + cmd.Callee + "传入了重复的参数" + param + "。"
    | ArgumentUnmatchedException (debug, cmd, param) ->
        "在第" + string debug.LineNumber + "行发现不能为" + cmd.Callee + "匹配参数" + param + "。"
    | ParseUnfinishedException str ->
        "解析未能完成，剩余内容为" + str + "。"
    | NotLiteralException str ->
        str + "不符合Literal。"
    | NotInRangeException _ -> "不在范围内。"
    | PredicateFailedException -> "不符合条件。"
    | EndException -> "遇到结尾。"
    | ExceptSymbolException x -> "需要一个" + x + "，但并未传入。"
    | InvalidTopLevelException topLevel -> "未知的顶级定义" + topLevel + "。"
    | CannotDefineSceneInLibException -> "不能在库中定义scene。"
        
    | e -> "未知错误" + e.Message