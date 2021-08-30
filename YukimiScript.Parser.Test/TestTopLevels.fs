module YukimiScript.Parser.Test.TestTopLevels

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser
open YukimiScript.Parser.Elements
open NUnit.Framework


[<Test>]
let testComment () =
    Parser.parseLine "   # test "
    |> function
        | Ok { Line = Line.EmptyLine; Comment = Some "test" } -> ()
        | x -> Assert.Fail <| sprintf "%A" x


[<Test>]
let testNoComment () =
    Parser.parseLine "   "
    |> function
        | Ok { Line = Line.EmptyLine; Comment = None } -> ()
        | x -> Assert.Fail <| sprintf "%A" x


[<Test>]
let testEmptyLine () = 
    testParse "  " Line.EmptyLine
    

[<Test>]
let testSceneDefination () =
    testParse "- scene \"测试场景A\"" <|
        SceneDefination { Name = "测试场景A"; Inherit = None }

    testParse "- scene \"测试场景B\" inherit \"A\"" <|
        SceneDefination { Name = "测试场景B"; Inherit = Some "A" }


[<Test>]
let testMacroDefination () =
    testParse "  -   macro   test" <| 
        MacroDefination { Name = "test"; Param = [] }

    testParse " -  macro test  param1" <|
        MacroDefination { Name = "test"; Param = ["param1", None] }

    testParse " -  macro test  param1 param2" <| 
        MacroDefination 
            { Name = "test"
              Param = ["param1", None; "param2", None] }

    testParse " -  macro test  param1=def param2 param3=1 param4 param5=\"what\"" <| 
        MacroDefination 
            { Name = "test"
              Param =
                [ "param1", Some (Symbol "def")
                  "param2", None
                  "param3", Some (Integer 1)
                  "param4", None
                  "param5", Some (String "what") ] }