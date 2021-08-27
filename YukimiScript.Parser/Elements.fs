namespace YukimiScript.Parser.Elements


type ObjectName = 
    | ObjectName of
        parent: ObjectName option *
        name: string


type Constant =
    | Flag
    | Null
    | String of string
    | Integer of int
    | Number of float


type Expression = 
    | Constant of Constant
    | Call of 
        methodName: ObjectName *
        unnamedArguments: Expression list *
        namedArguments: (string * Expression) list
    | ReferenceOrCall of name: ObjectName
    | Reference of name: ObjectName
    | Tuple of Expression list
    | Bracket of Expression


type Parameter = 
    | Parameter of
        name: string *
        defaultExpr: Expression option


type Statment =
    | Binding of
        name: ObjectName *
        expr: Expression
    | IfScopeBegin of 
        cond: Expression
    | ElseIfScopeBegin of
        cond: Expression
    | ElseScopeBegin
    | WhileScopeBegin of
        cond: Expression
    | ForScopeBegin of
        variable: string *
        init: Expression *
        final: Expression
    | MarkScopeBegin of
        marks: Expression
    | NormalScopeBegin
    | ScopeEnd
    | Do of Expression


type TextSlice =
    | Text of string
    | Do of Expression
    | MarkSlice of 
        marks: Expression *
        TextSlice

