module YukimiScript.Parser.Test.Utils

open YukimiScript.Parser.Elements
open YukimiScript.Parser.Parser
open NUnit.Framework


let testParse (x: string) (case: Line) =
    try
        match parseLine x with
        | Error e -> Assert.Fail (sprintf "%A" e)
        | Ok parsed ->
            Assert.AreEqual(case, parsed.Line)
    with
    | ex ->
        Assert.Fail (sprintf "%A" ex)



let testParseScript (x: string) =
    x.Replace("\r", "").Split('\n')
    |> Array.mapi 
        (fun lineNumber line ->
            let lineNumber = lineNumber + 1
            match parseLine line with
            | Error e ->
                printfn ""
                printfn ""
                printfn "Error: Line %d" lineNumber
                printfn "%A" e
                Assert.Fail ()
                failwith ""
            | Ok parsed ->
                printfn ""
                printfn ""
                printfn "%A" parsed.Line
                if parsed.Comment.IsSome then
                    printfn "# %s" parsed.Comment.Value
                    
                parsed)
    |> YukimiScript.Parser.Dom.analyze
    |> Result.map YukimiScript.Parser.Dom.expandTextCommands
    |> Result.bind YukimiScript.Parser.Dom.expandMacros
    |> printfn "%A"