module YukimiScript.Parser.Test.TestProject

open YukimiScript.Parser.Project
open NUnit.Framework


[<Test>]
let testLoadExampleProject () =
    openProject "../../../../Example"
    |> printfn "%A"