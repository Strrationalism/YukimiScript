module YukimiScript.CodeGen.Lua

open YukimiScript.Parser
open YukimiScript.Parser.Dom
open YukimiScript.Parser.Elements


let generateLua (x: Dom) : string =
    let luaCall (x: string) =
        let i = x.LastIndexOf '.'
        x.[..i-1] + ":" + x.[i+1..]

    let sb = System.Text.StringBuilder ()
    sb.Append("return function(api) return {") |> ignore

    x.Scenes
    |> List.iter (fun (defination, block, debugInfo) ->
        sb  .Append("  [\"")
            .Append(Constants.string2literal defination.Name)
            .Append("\"] = {") |> ignore

        if debugInfo.Comment.IsSome then
            sb  .Append("    -- ")
                .Append(debugInfo.Comment.Value) |> ignore

        sb.AppendLine() |> ignore

        block
        |> List.iter (fun (op, debugInfo) ->
            match op with
            | EmptyLine -> ()
            | CommandCall c ->
                sb  .Append("    function() ")
                    .Append(luaCall <| "api." + c.Callee)
                    .Append("(") |> ignore

                c.UnnamedArgs
                |> List.map (function
                    | Symbol "true" -> "true"
                    | Symbol "false" -> "false"
                    | Symbol "null" | Symbol "nil" -> "nil"
                    | Symbol x -> luaCall <| "api." + x
                    | Integer x -> string x
                    | Number x -> string x
                    | String x -> "\"" + Constants.string2literal x + "\"")
                |> List.reduce (fun a b -> a + ", " + b)
                |> sb.Append
                |> ignore

                sb.Append(") end,") |> ignore

            | _ -> failwith "This construction is not supported."

            if debugInfo.Comment.IsSome then
                sb.Append("    -- ").Append(debugInfo.Comment.Value) |> ignore

            sb.AppendLine() |> ignore
        )
        
        sb.AppendLine("  },") |> ignore
    )

    sb  .AppendLine("} end")
        .ToString()
    
