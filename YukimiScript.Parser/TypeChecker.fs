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
    let void' = ParameterType ("void", set [])
    let all = [ any; int; number; real; symbol; void' ]


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


let matchCallTypes d (pars: ParameterType list) (argType: SimpleType list) =
    List.zip pars argType |> List.mapi (fun i (a, b) -> matchType d i a b)
    |> ParserMonad.sequenceRL |> Result.map ignore


let matchCall d (pars: ParameterType list) (argType: Constant list) =
    argType |> List.map checkType |> matchCallTypes d pars


exception IsNotAType of string


let parametersTypeFromBlock (par: Parameter list) (b: Block) =
    let typeMacroParams = 
        [ { Parameter = "param"; Default = None }
          { Parameter = "type"; Default = None } ]
    
    List.choose (function
        | CommandCall c, d when c.Callee = "__type" -> Some (c, d)
        | _ -> None) b
    |> List.map (fun (c, d) -> Macro.matchArguments d typeMacroParams c)
    |> ParserMonad.sequenceRL
    |> Result.bind (fun x ->
        let paramTypePairs =
            List.map (readOnlyDict >> fun x ->
                match x.["param"], x.["type"] with
                | Symbol par, Symbol t -> par, t
                | _ -> failwith "parametersTypeFromBlock: failed!") x
                
        par
        |> List.map (fun { Parameter = name; Default = _ } -> 
            paramTypePairs
            |> List.filter (fst >> (=) name)
            |> List.map snd
            |> function
                | [] -> Ok Types.any
                | types -> 
                    types
                    |> List.map (fun typeName -> 
                        Types.all
                        |> List.tryFind (fun (ParameterType (n, _)) -> n = typeName)
                        |> function
                            | Some x -> Ok x
                            | None -> Error <| IsNotAType typeName)
                    |> ParserMonad.sequenceRL
                    |> Result.map (List.reduce sumParameterType))
        |> ParserMonad.sequenceRL)
