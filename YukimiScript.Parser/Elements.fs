namespace YukimiScript.Parser.Elements


type Constant =
    | String of string
    | Real of float
    | Integer of int32
    | Symbol of string


type CommandArg =
    | Constant of Constant
    | StringFormat of string


type CommandCall =
    { Callee: string
      UnnamedArgs: CommandArg list
      NamedArgs: (string * CommandArg) list }


type TextSlice =
    | Text of string
    | CommandCall of CommandCall
    | Marked of mark: string * inner: TextSlice list


type Parameter =
    { Parameter: string
      Default: CommandArg option }


type MacroDefination = { Name: string; Param: Parameter list }


type SceneDefination =
    { Name: string
      Inherit: string option }


type TextBlock =
    { Character: string option
      Text: TextSlice list
      HasMore: bool }


type ExternDefination = ExternCommand of string * Parameter list


type Line =
    | EmptyLine
    | SceneDefination of SceneDefination
    | MacroDefination of MacroDefination
    | ExternDefination of ExternDefination
    | CommandCall of CommandCall
    | Text of TextBlock


type DebugInformation =
    { LineNumber: int
      File: string }


type Operation =
    | Text of TextBlock
    | CommandCall of CommandCall
    | EmptyLine


module Operation =
    let toLine: Operation -> Line =
        function
        | Text t -> Line.Text t
        | CommandCall c -> Line.CommandCall c
        | EmptyLine -> Line.EmptyLine


    exception CanNotConvertToOperationException of Line


    let ofLine: Line -> Operation =
        function
        | Line.Text t -> Text t
        | Line.CommandCall c -> CommandCall c
        | Line.EmptyLine -> EmptyLine
        | x -> raise <| CanNotConvertToOperationException x


type Block = (Operation * DebugInformation) list
