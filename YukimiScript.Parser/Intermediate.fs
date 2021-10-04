namespace YukimiScript.Parser

open YukimiScript.Parser.Elements


type IntermediateScene = 
    IntermediateScene of SceneDefination * CommandCall list


type Intermediate = 
    Intermediate of IntermediateScene list


module Intermediate =
    let ofDom (dom: Dom) =
        dom.Scenes
        |> List.map (fun (scene, block, _) -> 
            let commands =
                block
                |> List.choose (fun (op, _) ->
                    match op with
                    | EmptyLine -> None
                    | CommandCall c -> Some c
                    | a -> 
                        failwithf "Not support in intermediate: %A" a)
                        
            IntermediateScene (scene, commands))
        |> Intermediate

