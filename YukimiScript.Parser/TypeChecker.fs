module YukimiScript.Parser.TypeChecker

open YukimiScript.Parser.Elements


type SimpleType =
    | Int'
    | Real'
    | String'
    | Symbol'
    | ExplicitSymbol' of string


type ParameterType = ParameterType of name: string * Set<SimpleType>


module Types = 
    let any = ParameterType ("any", set [ Int'; Real'; String'; Symbol' ])
    let int = ParameterType ("int", set [ Int' ])
    let number = ParameterType ("number", set [ Int'; Real' ])
    let real = ParameterType ("real", set [ Real' ])
    let symbol = ParameterType ("symbol", set [ Symbol' ])
    let string = ParameterType ("string", set [ String' ])
    let bool = ParameterType ("bool", set [ ExplicitSymbol' "true"; ExplicitSymbol' "false"])
    let ``null`` = ParameterType ("null", set [ ExplicitSymbol' "null"])
    let all = [ any; int; number; real; symbol; string; bool; ``null`` ]


let sumParameterType (ParameterType (n1, s1)) (ParameterType (n2, s2)) =
    ParameterType (n1 + " | " + n2, Set.union s1 s2)


let checkType = 
    function
    | Constant (String _) -> String'
    | Constant (Integer _) -> Int'
    | Constant (Real _) -> Real'
    | Constant (Symbol x) -> ExplicitSymbol' x
    | StringFormat _ -> String'


let unify src dst =
    match (src, dst) with
    | (a, b) when a = b -> Some a
    | (ExplicitSymbol' _, Symbol') -> Some Symbol'
    | _ -> None


exception TypeCheckFailedException of DebugInformation * int * ParameterType * SimpleType


let matchType d i (ParameterType (t, paramTypes)) (argType: SimpleType) : Result<unit, exn> =
    if Set.exists (fun paramType -> unify argType paramType |> Option.isSome) paramTypes
    then Ok ()
    else Error <| TypeCheckFailedException (d, i, (ParameterType (t, paramTypes)), argType)


exception IsNotAType of string * DebugInformation


type BlockParamTypes = (string * ParameterType) list


let checkApplyTypeCorrect d (paramTypes: BlockParamTypes) (args: (string * CommandArg) list) =
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


