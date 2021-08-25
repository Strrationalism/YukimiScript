namespace YukimiScript.AST

open YukimiScript.AST.Elements
open System.IO

type Line =
    | GlobalDefination
    | LocalDefination
    | SceneDefination of sceneName: string
    | MethodDefination of
        name: ObjectName *
        param: Parameter
    | Statment of Statment
    | TextLine of 
        character: ObjectName option *
        slices: TextSlice list *
        more: bool
    | EmptyLine


