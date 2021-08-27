module YukimiScript.Parser.Test.TestConstant

open YukimiScript.Parser.Test.Utils
open YukimiScript.Parser
open YukimiScript.Parser.Elements
open NUnit.Framework


[<Test>]
let testConstantFlagAndNull () =
    testConstant "null" Null
    testConstant "flag" Flag


[<Test>]
let testIntegers () =
    let rnd = System.Random ()
    for _ in 0..16 do
        let i = rnd.Next()
        testConstant (string i) <| Integer i
        let j = -rnd.Next()
        testConstant (string i) <| Integer i

    testConstant "-  1674 " <| Integer -1674


[<Test>]
let testNumbers () =
    let rnd = System.Random ()
    for _ in 0..16 do
        let i = float (rnd.Next()) + rnd.NextDouble()
        testConstant (string i) <| Number i
        let j = -(float (rnd.Next()) + rnd.NextDouble())
        testConstant (string j) <| Number j

    testConstant "- 176.00" <| Number -176.0


[<Test>]
let testStrings () =
    let t (str: string) =
        let parserInput =
            str
                .Replace("\\", @"\\")
                .Replace("\n", @"\n")
                .Replace("\t", @"\t")
                .Replace("\"", "\\\"")
                .Replace("\'", @"\'")
                .Replace("\r", @"\r")
                
        testConstant ("\"" + parserInput + "\"") <| String str

    t "中文测试"
    t "english test  "
    t " This is a long text, \n this is a long text."
    t "\n\t\r\"\'\\    "
