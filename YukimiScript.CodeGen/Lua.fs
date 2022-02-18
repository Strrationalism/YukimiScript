﻿module YukimiScript.CodeGen.Lua

open YukimiScript.Parser
open YukimiScript.Parser.Elements


let generateLua (Intermediate scenes) =
    let luaCall (x: string) =
        let i = x.LastIndexOf '.'
        x.[..i - 1] + ":" + x.[i + 1..]

    let sb = System.Text.StringBuilder()

    sb.AppendLine("return function(api) return {")
    |> ignore

    scenes
    |> List.iter
        (fun scene ->
            sb
                .Append("  [\"")
                .Append(Constants.string2literal scene.Scene.Name)
                .Append("\"] = {")
            |> ignore

            sb.AppendLine() |> ignore

            scene.Block
            |> List.iter
                (fun c ->
                    sb
                        .Append("    function() ")
                        .Append(luaCall <| "api." + c.Callee)
                        .Append("(")
                    |> ignore

                    let args =
                        c.Arguments
                        |> List.map
                            (function
                            | Symbol "true" -> "true"
                            | Symbol "false" -> "false"
                            | Symbol "null"
                            | Symbol "nil" -> "nil"
                            | Symbol x -> "api." + x
                            | Integer x -> string x
                            | Real x -> string x
                            | String x -> "\"" + Constants.string2literal x + "\"")

                    if not <| List.isEmpty args then
                        args
                        |> List.reduce (fun a b -> a + ", " + b)
                        |> sb.Append
                        |> ignore

                    sb.Append(") end,") |> ignore

                    sb.AppendLine() |> ignore)

            sb.AppendLine("  },") |> ignore)

    sb.AppendLine("} end").ToString()