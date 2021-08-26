module TopLevelParser

open YukimiScript.Parser
open YukimiScript.Parser.Elements
open YukimiScript.Parser.ParserMonad
open YukimiScript.Parser.BasicParser


exception InvalidParameterException
exception UnknownTopLevelException of string 


let private methodDefination =
    parser {
        do!  explicit whitespace1
        let! methodName = explicit objectName

        let  paramsParser = parser {
            do!  whitespace1
            let! paramName = identifier
            
            let defaultExprParser = parser {
                do! literal "="
                failwith "Expression not impl."
                return ()
            }

            let defaultExpr = None

            return Parameter (paramName, defaultExpr)
        }

        let! param = zeroOrMore paramsParser

        return MethodDefination (methodName, param)
    }
    |> name "<method defination>"
    

let private sceneDefination =
    parser {
        do!  explicit whitespace1
        let! sceneName = explicit identifier
        return SceneDefination sceneName
    }
    |> name "<scene defination>"
    

let topLevelScopeDefination : Parser<Line> =
    parser {
        do!  literal "-"
        do!  whitespace0
        let! topLevelLabel = explicit identifier

        return! (
            match topLevelLabel with
            | "scene" -> sceneDefination
            | "method" -> methodDefination
            | "global" -> return' GlobalDefination
            | x -> raise (UnknownTopLevelException x)
        )
    }
    |> name "<top level defination>"

