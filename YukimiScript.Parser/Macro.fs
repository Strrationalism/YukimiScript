module internal YukimiScript.Parser.Macro

open YukimiScript.Parser.Elements
open ParserMonad
open Basics
open Constants


exception ParamRepeatedException of 
    macro: MacroDefination * param: string


let macroDefinationParser =
    parser {
        let! macroName = explicit symbol
        let! param =
            parser {
                do!  whitespace1
                let! paramName = symbol
                let! defArg =
                    parser {
                        do! literal "="
                        return! constantParser
                    }
                    |> zeroOrOne
                 
                return paramName, defArg
            }
            |> zeroOrMore

        return { Name = macroName; Param = param }
    }
    |> map (fun x ->
        match 
            Seq.countBy fst x.Param
            |> Seq.tryFind (fun (_, x) -> x > 1) with
        | Some (p, _) -> raise <| ParamRepeatedException (x, p)
        | None -> x)


exception NoMacroMatchedException


exception ParamNotMatchedException of 
    MacroDefination * CommandCall


exception ArgumentRepeatException of 
    MacroDefination * CommandCall * string


let private matchMacro x macro =
    let pred (macro: MacroDefination, _) = 
        macro.Name = x.Callee

    match List.tryFind pred macro with
    | None -> Error NoMacroMatchedException
    | Some (macro, other) -> 
        if macro.Param.Length < x.UnnamedArgs.Length then
            Error (ParamNotMatchedException (macro, x))
        else 
            let defaultArgs =
                macro.Param
                |> List.choose (fun (name, x) ->
                    Option.map (fun x -> name, x) x)

            let inputArgs =
                x.UnnamedArgs
                |> List.zip 
                    (List.map fst macro.Param.[..x.UnnamedArgs.Length - 1])
                |> List.append x.NamedArgs

            // 检查是否有重复传入的参数
            let inputArgRepeat =
                Seq.countBy fst inputArgs
                |> Seq.tryFind (fun (_, l) -> l > 1)
                |> function
                    | None -> Ok ()
                    | Some (p, _) -> 
                        Error <| ArgumentRepeatException (macro, x, p)
            
            
            let matchArg paramName =
                let find = List.tryFind (fst >> (=) paramName)
                find inputArgs
                |> Option.defaultWith 
                    (fun () -> 
                        find defaultArgs
                        |> Option.defaultWith 
                            (fun () -> paramName, Symbol "false"))

            inputArgRepeat 
            |> Result.map (fun () -> 
                let args = List.map (fst >> matchArg) macro.Param
                macro, other, args)

        
let private replaceParamToArgs args macroBody =
    let replaceArg = 
        function
        | Symbol x -> 
            match List.tryFind (fst >> (=) x) args with
            | None -> Symbol x
            | Some (_, x) -> x
        | x -> x

    { macroBody with
        UnnamedArgs = 
            List.map replaceArg macroBody.UnnamedArgs
        NamedArgs = 
            List.map 
                (fun (name, arg) -> name, replaceArg arg) 
                macroBody.NamedArgs }
    


let rec expandSingleOperation macros operation : Result<Block, exn> =
    match operation with
    | (CommandCall command, debug) ->
        match matchMacro command macros with
        | Error NoMacroMatchedException -> Ok <| [CommandCall command, debug]
        | Error e -> Error e
        | Ok (_, macroBody: Block, args) ->
            macroBody
            |> List.map 
                (function
                    | (CommandCall call, debugInfo) -> 
                        CommandCall <| replaceParamToArgs args call, debugInfo
                    | x -> x)
            |> List.fold 
                (fun state x -> 
                    state
                    |> Result.bind (fun state ->
                        expandSingleOperation macros x 
                        |> Result.map 
                            (fun x -> state @ [x])))
                (Ok [])
            |> Result.map List.concat
    | x -> Ok [x]
    
