module YukimiScript.CodeGen.Lua

open YukimiScript.Parser
open YukimiScript.Parser.Elements


let generateLua genDebug (Intermediate scenes) =
    let sb = System.Text.StringBuilder()
    let sbDebug = if genDebug then Some (System.Text.StringBuilder ()) else None

    let strPool = ref Map.empty
    let getStr str = 
        match Map.tryFind str strPool.Value with
        | Some x -> "s" + string x
        | None ->
            let curId = Map.count strPool.Value
            strPool.Value <- Map.add str curId strPool.Value
            "s" + string curId

    sb.AppendLine("return function(api) return {")
    |> ignore

    let genConst =
        function
        | Symbol "true" -> "true"
        | Symbol "false" -> "false"
        | Symbol "null"
        | Symbol "nil" -> "nil"
        | Symbol x -> getStr x
        | Integer x -> string x
        | Real x -> string x
        | String x -> getStr x

    scenes
    |> List.iter
        (fun scene ->
            let scenName = Constants.string2literal scene.Name

            sb
                .Append("  [")
                .Append(getStr scenName)
                .Append("] = {")
            |> ignore

            sb.AppendLine() |> ignore

            sbDebug |> Option.iter (fun sbd -> 
                sbd
                    .Append("    [")
                    .Append(getStr scenName)
                    .AppendLine("] = {")
                    .Append("      F = ")
                    .Append(getStr scene.DebugInformation.File)
                    .AppendLine(",")
                    .Append("      L = ")
                    .Append(string scene.DebugInformation.LineNumber)
                |> ignore)

            scene.Block
            |> List.iter
                (fun c ->
                    let rec genDebugInfo (w: System.Text.StringBuilder) dbg =
                        let vars = 
                            match dbg.MacroVars with
                            | [] -> ""
                            | vars ->
                                vars
                                |> List.map (fun (name, var) ->
                                    "[" + getStr name + "] = " + genConst var)
                                |> List.reduce (fun a b -> a + ", " + b)
                        w
                            .Append("{ F = ")
                            .Append(getStr dbg.File)
                            .Append(", L = ")
                            .Append(string dbg.LineNumber)
                            .Append(", V = {")
                            .Append(vars)
                            .Append("}")
                        |> ignore

                        match dbg.Scope with
                        | None -> w.Append(", ScopeType = nil")
                        | Some (Choice1Of2 a) -> 
                            w
                                .Append(", ScopeType = ")
                                .Append(getStr "macro")
                                .Append(", MacroName = ")
                                .Append(getStr a.Name)
                        | Some (Choice2Of2 a) ->
                            w
                                .Append(", ScopeType = ")
                                .Append(getStr "scene")
                                .Append(", SceneName = ")
                                .Append(getStr a.Name)
                        |> ignore

                        if Option.isSome dbg.Outter then
                            w.Append(", O = ") |> ignore
                            genDebugInfo w dbg.Outter.Value

                        w.Append (" }") |> ignore
                        
                    sbDebug |> Option.iter (fun sbd ->
                        sbd
                            .AppendLine(",")
                            .Append("      ")
                        |> ignore
                        genDebugInfo sbd c.DebugInfo)
                    sb
                        .Append("    function() ")
                        .Append("api[")
                        .Append(getStr c.Callee)
                        .Append("]")
                        .Append("(api")
                    |> ignore

                    if not <| List.isEmpty c.Arguments then
                        sb.Append(", ") |> ignore

                    let args = List.map genConst c.Arguments
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

    let strPool =
        Map.toSeq strPool.Value
        |> Seq.map (fun (str, strId) ->
            "local s" + string strId + " = \"" 
            + Constants.string2literal str + "\"")
        |> Seq.fold (fun acc x -> acc + x + "\n") ""

    strPool + sb.AppendLine("} end").ToString()

