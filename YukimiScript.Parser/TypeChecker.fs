module YukimiScript.Parser.TypeChecker

open YukimiScript.Parser.Elements


type SimpleType =
    | Int'
    | Real'
    | String'
    | Symbol'


type ParameterType = ParameterType of name: string * Set<SimpleType>


module Types = 
    let any = ParameterType ("any", set [ Int'; Real'; String'; Symbol' ])
    let int = ParameterType ("int", set [ Int' ])
    let number = ParameterType ("number", set [ Int'; Real' ])
    let real = ParameterType ("real", set [ Real' ])
    let symbol = ParameterType ("symbol", set [ Symbol' ])
    let all = [ any; int; number; real; symbol ]


let sumParameterType (ParameterType (n1, s1)) (ParameterType (n2, s2)) =
    ParameterType (n1 + " | " + n2, Set.union s1 s2)


let checkType = 
    function
    | String _ -> String'
    | Integer _ -> Int'
    | Real _ -> Real'
    | Symbol _ -> Symbol'

    
exception TypeCheckFailedException of DebugInformation * int * ParameterType * SimpleType


let matchType d i (ParameterType (t, types)) (argType: SimpleType) : Result<unit, exn> =
    if Set.contains argType types
    then Ok ()
    else Error <| TypeCheckFailedException (d, i, (ParameterType (t, types)), argType)


exception IsNotAType of string


type BlockParamTypes = (string * ParameterType) list


let checkApplyTypeCorrect d (paramTypes: BlockParamTypes) (args: (string * Constant) list) =
    paramTypes
    |> List.mapi (fun i (paramName, paramType) ->
        args
        |> List.find (fst >> ((=) paramName))
        |> snd
        |> checkType
        |> matchType d i paramType)
    |> ParserMonad.sequenceRL
    |> Result.map (fun _ -> args)


exception CannotGetParameterException of (string * DebugInformation) list


let parametersTypeFromBlock (par: Parameter list) (b: Block) : Result<BlockParamTypes, exn> =
    let typeMacroParams = 
        [ { Parameter = "param"; Default = None }
          { Parameter = "type"; Default = None } ]

    let typeMacroParamsTypes =
        [ "param", Types.symbol
          "type", Types.symbol ]
    
    List.choose (function
        | CommandCall c, d when c.Callee = "__type" -> Some (c, d)
        | _ -> None) b
    |> List.map (fun (c, d) -> 
        Macro.matchArguments d typeMacroParams c
        |> Result.bind (checkApplyTypeCorrect d typeMacroParamsTypes)
        |> Result.map (fun x -> x, d))
    |> ParserMonad.sequenceRL
    |> Result.bind (fun x ->
        let paramTypePairs =
            List.map (fun (x, d) ->
                x
                |> readOnlyDict 
                |> fun x ->
                match x.["param"], x.["type"] with
                | Symbol par, Symbol t -> par, t, d
                | _ -> failwith "parametersTypeFromBlock: failed!") x

        let dummy =
            paramTypePairs
            |> List.filter (not << fun (n, _, d) -> 
                List.exists (fun p -> p.Parameter = n) par)
            |> List.map (fun (n, _, d) -> n, d)

        if List.length dummy = 0
        then
            par
            |> List.map (fun { Parameter = name; Default = _ } -> 
                paramTypePairs
                |> List.filter ((=) name << fun (n, _, _) -> n)
                |> List.map (fun (_, t, d) -> t, d)
                |> function
                    | [] -> Ok (name, Types.any)
                    | types -> 
                        types
                        |> List.map (fun (typeName, d) -> 
                            Types.all
                            |> List.tryFind (fun (ParameterType (n, _)) -> n = typeName)
                            |> function
                                | Some x -> Ok x
                                | None -> Error <| IsNotAType typeName)
                        |> ParserMonad.sequenceRL
                        |> Result.map (fun t -> 
                            name, List.reduce sumParameterType t))
            |> ParserMonad.sequenceRL
        else Error <| CannotGetParameterException dummy)
