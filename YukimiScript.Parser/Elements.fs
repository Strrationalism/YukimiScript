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


type Parameter = 
    { Parameter: string
      Default: Constant option }


type MacroDefination =
    { Name: string
      Param: Parameter list }


type SceneDefination =
    { Name: string
      Inherit: string option }


type TextBlock =
    { Character: string option
      Text: TextSlice list
      HasMore: bool }


type ExternDefination =
    | ExternCommand of string * Parameter list


type Line =
    | EmptyLine
    | SceneDefination of SceneDefination
    | MacroDefination of MacroDefination
    | ExternDefination of ExternDefination
    | CommandCall of CommandCall
    | Text of TextBlock


type DebugInformation =
    { LineNumber: int
      Comment: string option }


type Operation =
    | Text of TextBlock
    | CommandCall of CommandCall
    | EmptyLine


type Block = (Operation * DebugInformation) list
