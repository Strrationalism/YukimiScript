module internal YukimiScript.Parser.StatmentParser

open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.BasicParser
open YukimiScript.Parser.Elements


let private doExprStatment =
    parser {
        let! expr = ExpressionParser.expression ()
        return Statment.Do expr
    }
    |> name "<doexpr statment>"
    

let private bindStatment =
    parser {
        let! name = objectName
        do! whitespace0
        do! literal ":="
        do! whitespace0
        let! expr = ExpressionParser.expression ()
        return Binding (name, expr)
    }


let statment =
    parser {
        do! literal "@"
        do! whitespace0
        return! choices [
            doExprStatment
            bindStatment
        ]
    }
    |> name "<statment>"

