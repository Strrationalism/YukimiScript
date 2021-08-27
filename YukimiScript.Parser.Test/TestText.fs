module YukimiScript.Parser.Test.TestText

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser.Elements
open NUnit.Framework


[<Test>]
let testTextOnly () =
    testParse "你好？   \\ " (Line.Text (None, [TextSlice.Text "你好？"], true))
    testParse "  A:你好？  " (Line.Text (Some "A", [TextSlice.Text "你好？"], false))


[<Test>]
let testCommand () =
    testParse "[wait --time 5]\\" 
        (Line.Text (
            None,
            [TextSlice.CommandCall
                (CommandCall.CommandCall (
                    "wait",
                    [],
                    [ "time", Integer 5]
                ))
            ],
            true
        ))

    testParse "A:测试  [wait --time 5]测试  " 
        (Line.Text 
            (Some "A",
            [
                TextSlice.Text "测试  "
                TextSlice.CommandCall
                    (CommandCall.CommandCall (
                        "wait",
                        [],
                        [ "time", Integer 5]
                    ))

                TextSlice.Text "测试"
            ],
            false)
        )