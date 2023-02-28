module YukimiScript.CodeGen.WebGAL

open YukimiScript.Parser
open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.Elements
open System.Text
open System


let private generateCommand externs (sb: StringBuilder) cmd =
    let param = 
        List.zip
            (Map.find (cmd: IntermediateCommandCall).Callee externs 
                |> List.map (fun x -> x.Parameter))
            cmd.Arguments
        |> List.filter (fun (_, x) -> x <> Symbol "null")
    sb.Append(cmd.Callee).Append(":") |> ignore

    let writeArg = function
        | String a -> sb.Append(a) |> ignore      
        | Real a -> sb.Append(a) |> ignore
        | Integer a -> sb.Append(a) |> ignore
        | Symbol a -> sb.Append(a) |> ignore

    let writeParams =
        List.iter (fun (p: string, a) -> 
            sb.Append(" -").Append(p).Append("=") |> ignore
            writeArg a)
    
    match param with
    | [] -> ()
    | ("content", contentArg) :: nextParams -> 
        writeArg contentArg
        writeParams nextParams
    | nextParams ->
        writeParams nextParams

    sb.AppendLine(";") |> ignore


let private generateScene externs sb (scene: IntermediateScene) =
    (sb: StringBuilder).Append("label:").Append(scene.Name).AppendLine(";") |> ignore
    scene.Block |> List.iter (generateCommand externs sb)
    sb.AppendLine() |> ignore


let generateWebGAL dom (Intermediate scenes) =
    let externs = 
        dom.Externs 
        |> Seq.map (fun (ExternCommand (a, p), _, _) -> a, p)
        |> Map.ofSeq

    let sb = StringBuilder ()
    scenes |> Seq.iter (generateScene externs sb)
    sb


