module YukimiScript.Parser.Test.Utils

open YukimiScript.Parser.Elements
open YukimiScript.Parser.Parser
open YukimiScript.Parser
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


let testExpr expr case =
    testParse 
        ("@ " + expr) 
        (Statment.Do case |> Statment)


let testConstant expr constant =
    testExpr expr <| Constant constant

