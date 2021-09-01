module internal YukimiScript.Parser.Macro

open YukimiScript.Parser.Elements
open ParserMonad
open Basics
open Constants


exception ParamRepeatedException of 
    parent: string * param: string


let private parameter: Parser<Parameter> =
    parser {
        do!  whitespace1
        let! paramName = symbol
        let! defArg =
            parser {
                do! literal "="
                return! constantParser
            }
            |> zeroOrOne
         
        return { Parameter = paramName
                 Default = defArg }
    }
    |> name "parameter"


let parameterList parentName : Parser<Parameter list> =
    zeroOrMore parameter
    |> map (fun x ->
        match 
            Seq.countBy (fun x -> x.Parameter) x
            |> Seq.tryFind (fun (_, x) -> x > 1) 
            with
        | Some (p, _) -> raise <| ParamRepeatedException (parentName, p)
        | None -> x)


let macroDefinationParser =
    parser {
        let! macroName = explicit symbol
        let! param = parameterList macroName

        return { Name = macroName; Param = param }
    }


exception NoMacroMatchedException


exception ArgumentsTooMuchException of 
    MacroDefination * CommandCall


exception ArgumentRepeatException of 
    MacroDefination * CommandCall * string


let private matchMacro x macro =
    let pred (macro: MacroDefination, _) = 
        macro.Name = x.Callee

    match List.tryFind pred macro with
    | None -> Error NoMacroMatchedException
    | Some (macro, _) when macro.Param.Length < x.UnnamedArgs.Length ->
        Error <| ArgumentsTooMuchException (macro, x)
    | Some (macro, other) -> 
        let defaultArgs =
            macro.Param
            |> List.choose 
                (fun { Parameter = name; Default = x } ->
                    Option.map (fun x -> name, x) x)

        let inputArgs =
            x.UnnamedArgs
            |> List.zip 
                (List.map 
                    (fun x -> x.Parameter) 
                    macro.Param.[..x.UnnamedArgs.Length - 1])
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
            |> Option.defaultWith (fun () -> 
                find defaultArgs
                |> Option.defaultWith (fun () -> 
                    paramName, Symbol "false"))

        inputArgRepeat 
        |> Result.map (fun () -> 
            let args = List.map (fun x -> matchArg x.Parameter) macro.Param
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
    


let rec private expandSingleOperation 
                macros 
                operation 
                : Result<Block, exn> =
    match operation with
    | CommandCall command, debug ->
        match matchMacro command macros with
        | Error NoMacroMatchedException -> 
            Ok <| [CommandCall command, debug]
        | Error e -> Error e
        | Ok (macro, macroBody: Block, args) ->
            let macros =
                macros 
                |> List.filter (fun (x, _) -> 
                    x.Name <> macro.Name)
                        
            macroBody
            |> List.map (function
                | CommandCall call, debugInfo -> 
                    CommandCall <| replaceParamToArgs args call, debugInfo
                | x -> x
                >> expandSingleOperation macros)
            |> switchResultList
            |> Result.map List.concat
    | x -> Ok [x]

    
let expandBlock macros (block: Block) =
    List.map (expandSingleOperation macros) block
    |> switchResultList 
    |> Result.map List.concat


let expandSystemMacros (block: Block) =
    let systemMacros = [ "__diagram_link_to"]
    block
    |> List.map (function
        | CommandCall cmdCall, dbg when 
            List.exists ((=) cmdCall.Callee) systemMacros ->
            EmptyLine, dbg
        | x -> x)
