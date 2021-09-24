namespace YukimiScript.Parser.Elements

open YukimiScript.Parser


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
      Comment: string option
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


    let ofLine: Line -> Result<Operation> =
        function
        | Line.Text t -> Ok <| Text t
        | Line.CommandCall c -> Ok <| CommandCall c
        | Line.EmptyLine -> Ok <| EmptyLine
        | x -> Error <| CanNotConvertToOperationException x


type Block = (Operation * DebugInformation) list

