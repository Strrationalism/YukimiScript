module YukimiScript.CodeGen.WebGAL

open YukimiScript.Parser
open YukimiScript.Parser.Elements
open System.Text


let private genArg = function
    | Symbol x -> x
    | String x -> x
    | Integer x -> string x
    | Real x -> string x


let private genArgs = function
    | [] -> ""
    | ls -> ":" + (ls |> List.map genArg |> List.reduce (fun a b -> a + "," + b))


let private genCmd (c: IntermediateCommandCall) =
    c.Callee + genArgs c.Arguments + ";"


let private genScene (s: StringBuilder) c = 
    s.AppendLine ("label:" + c.Scene.Name + ";") |> ignore

    c.Block
    |> List.iter (genCmd >> s.AppendLine >> ignore)


let genWebGal (Intermediate scenes) = 
    let s = StringBuilder ()
    scenes |> List.iter (genScene s)

    s.ToString ()

