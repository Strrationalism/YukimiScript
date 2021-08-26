module YukimiScript.Parser.Test.TestParsers

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser
open YukimiScript.Parser.Elements
open NUnit.Framework


[<SetUp>]
let setup () = ()


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
    testParse "- scene a" 
        <| SceneDefination "a"


[<Test>]
let testMethodDefination () =
    testParse "  -   method    system.id    x  y  z w# id: a -> a"
        (MethodDefination 
            (ObjectName (Some (ObjectName (None, "system")), "id"),
                [ Parameter ("x", None)
                  Parameter ("y", None)
                  Parameter ("z", None)
                  Parameter ("w", None) ]))

