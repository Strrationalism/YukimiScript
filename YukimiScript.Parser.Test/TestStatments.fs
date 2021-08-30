module YukimiScript.Parser.Test.TestStatments

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser.Elements
open NUnit.Framework


[<Test>]
let testCommandCall () =
    testParse " @  bg.play Black \"C1\" -256 --effect a --camera -2.0" <|
        Line.CommandCall
            { Callee = "bg.play"
              UnnamedArgs = [ Symbol "Black"; String "C1"; Integer -256 ]
              NamedArgs = 
                [ "effect", Symbol "a"
                  "camera", Number -2.0 ] }

