module YukimiScript.Parser.Test.Utils

open YukimiScript.Parser.Parser
open YukimiScript.Parser
open NUnit.Framework


let testParse (x: string) (case: Line) =
    try
        match parseLine x with
        | Error e -> Assert.Fail (sprintf "%A" e)
        | Ok parsed ->
            if case <> parsed.Line then
                Assert.Fail (sprintf "%A" parsed.Line)
    with
    | ex ->
        Assert.Fail (sprintf "%A" ex)