module YukimiScript.Parser.Test.TestTopLevels

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser
open YukimiScript.Parser.Elements
open NUnit.Framework


let testComment () =
    Parser.parseLine "   # test "
    |> function
        | Ok { Line = EmptyLine; Comment = Some "test" } -> ()
        | x -> Assert.Fail <| sprintf "%A" x


[<Test>]
let testNoComment () =
    Parser.parseLine "   "
    |> function
        | Ok { Line = EmptyLine; Comment = None } -> ()
        | x -> Assert.Fail <| sprintf "%A" x


[<Test>]
let testEmptyLine () = 
    testParse "  " EmptyLine


[<Test>]
let testImportDefination () = 
    testParse "   -  import \"abc\"   # ? " <| Import "abc"


[<Test>]
let testSceneDefination () =
    testParse "- scene \"这是一个 测试 用\\n的 场景A\"" 
        <| SceneDefination ("这是一个 测试 用\n的 场景A", None)

    testParse "- scene \"这是一个 测试 用\\n的 场景B\" inherit \"A\"" 
    <| SceneDefination ("这是一个 测试 用\n的 场景B", Some "A")


[<Test>]
let testMacroDefination () =
    testParse "  -   macro   test" <| MacroDefination ("test", [])
    testParse " -  macro test  param1" <| MacroDefination ("test", ["param1", None])
    testParse " -  macro test  param1 param2" <| 
        MacroDefination ("test", ["param1", None; "param2", None])

    testParse " -  macro test  param1=def param2 param3=1 param4 param5=\"what\"" <| 
        MacroDefination ("test", 
            [
                "param1", Some (Symbol "def")
                "param2", None
                "param3", Some (Integer 1)
                "param4", None
                "param5", Some (String "what")
            ])