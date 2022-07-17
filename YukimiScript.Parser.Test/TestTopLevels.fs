module YukimiScript.Parser.Test.TestTopLevels

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser
open YukimiScript.Parser.Elements
open NUnit.Framework


[<Test>]
let testComment () =
    Parser.parseLine "   # test "
    |> function
        | Ok { Line = Line.EmptyLine
               Comment = Some "test" } -> ()
        | x -> Assert.Fail <| sprintf "%A" x


[<Test>]
let testNoComment () =
    Parser.parseLine "   "
    |> function
        | Ok { Line = Line.EmptyLine
               Comment = None } -> ()
        | x -> Assert.Fail <| sprintf "%A" x


[<Test>]
let testEmptyLine () = testParse "  " Line.EmptyLine


[<Test>]
let testSceneDefination () =
    testParse "- scene \"测试场景A\""
    <| SceneDefination { Name = "测试场景A"; Inherit = None }

    testParse "- scene \"测试场景B\" inherit \"A\""
    <| SceneDefination { Name = "测试场景B"; Inherit = Some "A" }


[<Test>]
let testExternDefination () =
    testParse "- extern wait time=1"
    <| ExternDefination(
        ExternCommand(
            "wait",
            [ { Parameter = "time"
                Default = Some <| Constant (Integer 1) } ]
        )
    )


[<Test>]
let testMacroDefination () =
    testParse "  -   macro   test"
    <| MacroDefination { Name = "test"; Param = [] }

    testParse " -  macro test  param1"
    <| MacroDefination
        { Name = "test"
          Param = [ { Parameter = "param1"; Default = None } ] }

    testParse " -  macro test  param1 param2"
    <| MacroDefination
        { Name = "test"
          Param =
              [ { Parameter = "param1"; Default = None }
                { Parameter = "param2"; Default = None } ] }

    testParse " -  macro test  param1=def param2 param3=1 param4 param5=\"what\""
    <| MacroDefination
        { Name = "test"
          Param =
              [ { Parameter = "param1"
                  Default = Some <| Constant (Symbol "def") }
                { Parameter = "param2"; Default = None }
                { Parameter = "param3"
                  Default = Some <| Constant (Integer 1) }
                { Parameter = "param4"; Default = None }
                { Parameter = "param5"
                  Default = Some <| Constant (String "what") } ] }
