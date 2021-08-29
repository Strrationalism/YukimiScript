namespace YukimiScript.Parser.Elements


type Constant =
    | String of string
    | Number of float
    | Integer of int32
    | Symbol of string


type CommandCall =
    { Callee: string
      UnnamedArgs: Constant list
      NamedArgs: (string * Constant) list }


type TextSlice =
    | Text of string
    | CommandCall of CommandCall
    | Marked of 
        mark: string *
        inner: TextSlice list


type MacroDefination =
    { Name: string
      Param: (string * Constant option) list }


type SceneDefination =
    { Name: string
      Inherit: string option }


type TextBlock =
    { Character: string option
      Text: TextSlice list
      HasMore: bool }


type Line =
    | EmptyLine
    | SceneDefination of SceneDefination
    | MacroDefination of MacroDefination
    | CommandCall of CommandCall
    | Text of TextBlock