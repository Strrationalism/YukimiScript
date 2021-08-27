namespace YukimiScript.Parser.Elements


type Constant =
    | String of string
    | Number of float
    | Integer of int32
    | Symbol of string


type CommandCall =
    | CommandCall of
        callee: string *
        unnamedArgs: Constant list *
        namedArgs: (string * Constant) list


type TextSlice =
    | Text of string
    | CommandCall of CommandCall
    | Marked of 
        mark: string *
        inner: TextSlice list


type Line =
    | EmptyLine
    | Import of string
    | SceneDefination of 
        sceneName: string *
        inheritScene: string option
    | MacroDefination of 
        name: string *
        param: (string * Constant option) list
    | CommandCall of CommandCall
    | Text of 
        character: string option *
        text: TextSlice list *
        more: bool
