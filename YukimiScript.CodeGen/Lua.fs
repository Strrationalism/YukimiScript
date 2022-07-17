module YukimiScript.CodeGen.Lua

open YukimiScript.Parser
open YukimiScript.Parser.Elements


let generateLua genDebug (Intermediate scenes) =
    let sb = System.Text.StringBuilder()

    let sbDebug = if genDebug then Some (System.Text.StringBuilder ()) else None

    sb.AppendLine("return function(api) return {")
    |> ignore

    scenes
    |> List.iter
        (fun scene ->
            let scenName = Constants.string2literal scene.Name

            sb
                .Append("  [\"")
                .Append(scenName)
                .Append("\"] = {")
            |> ignore

            sb.AppendLine() |> ignore

            sbDebug |> Option.iter (fun sbd -> 
                sbd
                    .Append("    [\"")
                    .Append(scenName)
                    .AppendLine("\"] = {")
                    .Append("      F = \"")
                    .Append(scene.DebugInformation.File.Replace("\\", "\\\\"))
                    .AppendLine("\",")
                    .Append("      L = ")
                    .Append(string scene.DebugInformation.LineNumber)
                |> ignore)

            scene.Block
            |> List.iter
                (fun c ->
                    sbDebug |> Option.iter (fun sbd ->
                        sbd
                            .AppendLine(",")
                            .Append("      { F = \"")
                            .Append(c.DebugInformation.File.Replace("\\", "\\\\"))
                            .Append("\", L = ")
                            .Append(string c.DebugInformation.LineNumber)
                            .Append(" }") |> ignore)
                    sb
                        .Append("    function() ")
                        .Append("api[\"")
                        .Append(c.Callee)
                        .Append("\"]")
                        .Append("(api")
                    |> ignore

                    if not <| List.isEmpty c.Arguments then
                        sb.Append(", ") |> ignore

                    let args =
                        c.Arguments
                        |> List.map
                            (function
                            | Symbol "true" -> "true"
                            | Symbol "false" -> "false"
                            | Symbol "null"
                            | Symbol "nil" -> "nil"
                            | Symbol x -> "api[\"" + x + "\"]"
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

            sbDebug |> Option.iter (fun sbd ->
                sbd.AppendLine().AppendLine ("    },") |> ignore)

            sb.AppendLine().AppendLine("  },") |> ignore)

    sbDebug |> Option.iter (fun sbDbg ->
        sb.AppendLine ("  [0] = {") |> ignore
        sb.Append (sbDbg.ToString ()) |> ignore
        sb.AppendLine ("  }") |> ignore)

    sb.AppendLine("} end").ToString()

