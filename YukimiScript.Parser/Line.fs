namespace YukimiScript.Parser

open YukimiScript.Parser.Elements


type Line =
    | GlobalDefination
    | SceneDefination of sceneName: string
    | MethodDefination of
        name: ObjectName *
        param: Parameter list
    | Statment of Statment
    | TextLine of 
        character: ObjectName option *
        slices: TextSlice list *
        more: bool
    | EmptyLine


