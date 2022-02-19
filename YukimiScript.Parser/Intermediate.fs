namespace YukimiScript.Parser

open YukimiScript.Parser.Elements


type IntermediateCommandCall = 
    { Callee: string
      Arguments: Constant list 
      DebugInformation: DebugInformation }


type IntermediateScene = 
    { Scene: SceneDefination
      Block: IntermediateCommandCall list
      DebugInformation: DebugInformation }


type Intermediate = Intermediate of IntermediateScene list


module Intermediate =
    let ofDom (dom: Dom) =
        dom.Scenes
        |> List.map
            (fun (scene, block, debugScene) ->
                let commands =
                    block
                    |> List.choose
                        (fun (op, debugCommand) ->
                            match op with
                            | EmptyLine -> None
                            | CommandCall c -> 
                                { Callee = c.Callee
                                  Arguments = c.UnnamedArgs
                                  DebugInformation = debugCommand }
                                |> Some
                            | a -> failwith <| "Not support in intermediate: " + string a)

                { Scene = scene; Block = commands; DebugInformation = debugScene })
        |> Intermediate
