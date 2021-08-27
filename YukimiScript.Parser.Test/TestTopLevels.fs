module YukimiScript.Parser.Test.TestTopLevels

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser
open YukimiScript.Parser.Elements
open NUnit.Framework


[<Test>]
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
let testGlobalDefination () = 
    testParse "   -  global   # ? " GlobalDefination


[<Test>]
let testSceneDefination () =
    testParse "- scene \"这是一个 测试 用\\n的 场景A\"" 
        <| SceneDefination "这是一个 测试 用\n的 场景A"


[<Test>]
let testMethodDefination () =
    testParse "  -   method    system.id    x  y  z w# id: a -> a"
        (MethodDefination 
            (ObjectName (Some (ObjectName (None, "system")), "id"),
                [ Parameter ("x", None)
                  Parameter ("y", None)
                  Parameter ("z", None)
                  Parameter ("w", None) ]))

