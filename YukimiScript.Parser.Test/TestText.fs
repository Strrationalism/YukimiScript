module YukimiScript.Parser.Test.TestText

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser.Elements
open NUnit.Framework


[<Test>]
let testTextOnly () =
    testParse "你好？   \\ "
    <| Line.Text
        { Character = None
          Text = [ TextSlice.Text "你好？" ]
          HasMore = true }

    testParse "  A:你好？  "
    <| Line.Text
        { Character = Some "A"
          Text = [ TextSlice.Text "你好？" ]
          HasMore = false }


[<Test>]
let testCommand () =
    testParse "[wait --time 5]\\"
    <| Line.Text
        { Character = None
          Text =
              [ TextSlice.CommandCall
                    { Callee = "wait"
                      UnnamedArgs = []
                      NamedArgs = [ "time", Constant <| Integer 5 ] } ]
          HasMore = true }



    testParse "A:测试  [wait  5]测试  "
    <| Line.Text
        { Character = Some "A"
          Text =
              [ TextSlice.Text "测试  "
                TextSlice.CommandCall
                    { Callee = "wait"
                      UnnamedArgs = [ Constant <| Integer 5 ]
                      NamedArgs = [] }

                TextSlice.Text "测试" ]
          HasMore = false }
