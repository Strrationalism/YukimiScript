module internal rec YukimiScript.Parser.ExpressionParser

open YukimiScript.Parser
open YukimiScript.Parser.Elements
open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.BasicParser

let private bracketExpression () =
    parser {
        do! literal "["
        do! whitespace0
        let! expr = expression ()
        do! whitespace0
        do! literal "]"
        return Bracket expr
    }
    |> name "<bracked expression>"


let private referenceOrCallExpression () =
    objectName
    |> map ReferenceOrCall
    |> name "<object expression>"


let private referenceExpression () =
    parser {
        do! literal "id"
        do! whitespace1
        let! expr = objectName
        return Reference expr
    }
    |> name "<reference expression>"
    

let private constantExpression () =
    ConstantParser.constantParser
    |> name "<constant expression>"
    |> map Constant


let callExpr () =
    parser {
        let! methodName = objectName
        do! whitespace1
    
        let! unnamedArgs =
            parser {
                do! whitespace1
                return! expression ()
            }
            |> zeroOrMore

        let! namedArgs =
            parser {
                do!  whitespace1
                do!  literal "--"
                do!  whitespace0
                let! argName = explicit identifier
                do!  explicit whitespace1
                let! arg = explicit <| expression ()
                return argName, arg
            }
            |> zeroOrMore

        return Call (methodName, unnamedArgs, namedArgs)
    }
    |> name "<call>"


let private atomExpr () = 
    choices [
        bracketExpression ()
        constantExpression ()
        referenceExpression ()
        referenceOrCallExpression ()
        callExpr ()
    ]
    |> name "<expression>"


let expression () =
    parser {
        let! first = atomExpr ()
        do! whitespace0
        let! next = 
            parser {
                do! literal ","
                do! whitespace0
                return! atomExpr ()
            }
            |> zeroOrMore

        match next with
        | [] -> return first
        | x -> return (Tuple x)
    }
    |> name "<expression>"

