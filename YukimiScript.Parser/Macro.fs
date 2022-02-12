module internal YukimiScript.Parser.Macro

open YukimiScript.Parser.Elements
open TypeChecker
open ParserMonad
open Basics
open Constants


exception ParamRepeatedException of parent: string * param: string


let private parameter: Parser<Parameter> =
    parser {
        do! whitespace1
        let! paramName = symbol

        let! defArg =
            parser {
                do! literal "="
                return! constantParser
            }
            |> zeroOrOne

        return
            { Parameter = paramName
              Default = defArg }
    }
    |> name "parameter"


let parameterList parentName : Parser<Parameter list> =
    zeroOrMore parameter
    |> map
        (fun x ->
            match Seq.countBy (fun x -> x.Parameter) x
                  |> Seq.tryFind (fun (_, x) -> x > 1) with
            | Some (p, _) -> raise <| ParamRepeatedException(parentName, p)
            | None -> x)


let macroDefinationParser =
    parser {
        let! macroName = explicit symbol
        let! param = parameterList macroName
        return { Name = macroName; Param = param }
    }


exception NoMacroMatchedException


exception ArgumentsTooMuchException of DebugInformation * MacroDefination * CommandCall


exception ArgumentRepeatException of DebugInformation * CommandCall * string


exception ArgumentUnmatchedException of DebugInformation * CommandCall * parameter: string


let matchArguments debugInfo (x: Parameter list) (c: CommandCall) : Result<(string * Constant) list, exn> =
    let defaultArgs =
        x
        |> List.choose (fun { Parameter = name; Default = x } -> Option.map (fun x -> name, x) x)

    let inputArgs =
        c.UnnamedArgs
        |> List.zip (List.map (fun x -> x.Parameter) x.[..c.UnnamedArgs.Length - 1])
        |> List.append c.NamedArgs

    // 检查是否有重复传入的参数
    let inputArgRepeat =
        Seq.countBy fst inputArgs
        |> Seq.tryFind (fun (_, l) -> l > 1)
        |> function
            | None -> Ok()
            | Some (p, _) -> Error <| ArgumentRepeatException(debugInfo, c, p)

    let matchArg paramName : Result<string * Constant, exn> =
        let find = List.tryFind (fst >> (=) paramName)

        match find inputArgs with
        | Some x -> Ok x
        | None ->
            match find defaultArgs with
            | Some x -> Ok x
            | None ->
                Error
                <| ArgumentUnmatchedException(debugInfo, c, paramName)

    inputArgRepeat
    |> Result.bind
        (fun () ->
            List.map (fun x -> matchArg x.Parameter) x
            |> sequenceRL)


let private matchMacro debug x macro =
    let pred (macro: MacroDefination, _, _) = macro.Name = x.Callee

    match List.tryFind pred macro with
    | None -> Error NoMacroMatchedException
    | Some (macro, _, _) when macro.Param.Length < x.UnnamedArgs.Length ->
        Error
        <| ArgumentsTooMuchException(debug, macro, x)
    | Some (macro, t, other) ->
        matchArguments debug macro.Param x
        |> Result.bind (checkApplyTypeCorrect debug t)
        |> Result.map (fun args -> macro, other, args)
        


let private replaceParamToArgs args macroBody =
    let replaceArg =
        function
        | Symbol x ->
            match List.tryFind (fst >> (=) x) args with
            | None -> Symbol x
            | Some (_, x) -> x
        | x -> x

    { macroBody with
          UnnamedArgs = List.map replaceArg macroBody.UnnamedArgs
          NamedArgs = List.map (fun (name, arg) -> name, replaceArg arg) macroBody.NamedArgs }


exception MacroInnerException of DebugInformation * exn


let rec private expandSingleOperation macros operation : Result<Block, exn> =
    match operation with
    | CommandCall command, debug ->
        match matchMacro debug command macros with
        | Error NoMacroMatchedException -> Ok <| [ CommandCall command, debug ]
        | Error e -> Error e
        | Ok (macro, macroBody: Block, args) ->
            let macros =
                macros
                |> List.filter (fun (x, _, _) -> x.Name <> macro.Name)

            macroBody
            |> List.map (
                function
                | CommandCall call, debugInfo -> CommandCall <| replaceParamToArgs args call, debugInfo
                | x -> x
                >> expandSingleOperation macros
            )
            |> sequenceRL
            |> Result.map List.concat
    | x -> Ok [ x ]
    |> Result.mapError (fun err -> MacroInnerException (snd operation, err))


let expandBlock macros (block: Block) =
    List.map (expandSingleOperation macros) block
    |> sequenceRL
    |> Result.map List.concat


let expandSystemMacros (block: Block) =
    let systemMacros = [ "__diagram_link_to"; "__type"; "__type_symbol" ]

    block
    |> List.map
        (function
        | CommandCall cmdCall, dbg when List.exists ((=) cmdCall.Callee) systemMacros -> EmptyLine, dbg
        | x -> x)


let parametersTypeFromBlock (par: Parameter list) (b: Block) : Result<BlockParamTypes, exn> =
    let typeMacroParams = 
        [ { Parameter = "param"; Default = None }
          { Parameter = "type"; Default = None } ]

    let typeMacroParamsTypes =
        [ "param", Types.symbol
          "type", Types.symbol ]
    
    List.choose (function
        | CommandCall c, d when c.Callee = "__type" || c.Callee = "__type_symbol" -> Some (c, d)
        | _ -> None) b
    |> List.map (fun (c, d) -> 
        matchArguments d typeMacroParams c
        |> Result.bind (checkApplyTypeCorrect d typeMacroParamsTypes)
        |> Result.map (fun x -> c.Callee, x, d))
    |> sequenceRL
    |> Result.bind (fun x ->
        let paramTypePairs =
            List.map (fun (macroName, x, d) ->
                x
                |> readOnlyDict 
                |> fun x ->
                match x.["param"], x.["type"] with
                | Symbol par, Symbol t -> macroName, par, t, d
                | _ -> failwith "parametersTypeFromBlock: failed!") x

        let dummy =
            paramTypePairs
            |> List.filter (not << fun (_, n, _, _) -> 
                List.exists (fun p -> p.Parameter = n) par)
            |> List.map (fun (_, n, _, d) -> n, d)

        if List.length dummy = 0
        then
            par
            |> List.map (fun { Parameter = name; Default = _ } -> 
                paramTypePairs
                |> List.filter ((=) name << fun (_, n, _, _) -> n)
                |> List.map (fun (macroName, _, t, d) -> macroName, t, d)
                |> function
                    | [] -> Ok (name, Types.any)
                    | types -> 
                        types
                        |> List.map (fun (macroName, typeName, d) -> 
                            match macroName with
                            | "__type" -> 
                                Types.all
                                |> List.tryFind (fun (ParameterType (n, _)) -> n = typeName)
                                |> function
                                    | Some x -> Ok x
                                    | None -> Error <| IsNotAType (typeName, d)
                            | "__type_symbol" ->
                                Ok <| ParameterType ($"{typeName}", set [ExplicitSymbol' typeName])
                            | _ -> failwith "?")
                        |> sequenceRL
                        |> Result.map (fun t -> 
                            name, List.reduce sumParameterType t))
            |> sequenceRL
        else Error <| CannotGetParameterException dummy)
