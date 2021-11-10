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
    let string = ParameterType ("string", set [ String' ])
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


