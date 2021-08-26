namespace YukimiScript.AST.Elements


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


type Operator =
    | Add
    | Sub
    | Mul
    | Div
    | Mod
    | And
    | Or


type Expression = 
    | Constant of Constant
    | Call of 
        methodName: ObjectName *
        unnamedArguments: Expression list *
        namedArguments: (string * Expression) list
    | IdOrCall of name: ObjectName
    | Tuple of Expression list
    | BinaryOperation of 
        left: Expression *
        operator: Operator *
        right: Expression


type Parameter = 
    | Parameter of
        name: string *
        defaultExpr: Expression


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
    | End
    | Do of Expression


type TextSlice =
    | Text of string
    | Do of Expression
    | MarkSlice of 
        marks: Expression *
        TextSlice

