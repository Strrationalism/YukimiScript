namespace YukimiScript.Parser

open YukimiScript.Parser.Elements


type IntermediateCommandCall = 
    { Callee: string
      Arguments: Constant list 
      DebugInformation: DebugInformation }


type IntermediateScene = 
    { Name: string
      Block: IntermediateCommandCall list
      DebugInformation: DebugInformation }


type Intermediate = Intermediate of IntermediateScene list


module Intermediate =
    let ofDom (dom: Dom) =
        dom.Scenes
        |> List.map
            (fun ({ Name = sceneName }, block, debugScene) ->
                let commands =
                    block
                    |> List.choose
                        (fun (op, debugCommand) ->
                            match op with
                            | EmptyLine -> None
                            | CommandCall c -> 
                                { Callee = c.Callee
                                  Arguments = 
                                    c.UnnamedArgs
                                    |> List.map (function
                                        | Constant x -> x
                                        | _ -> failwith "Should not here.")
                                  DebugInformation = debugCommand }
                                |> Some
                            | a -> failwith <| "Not support in intermediate: " + string a)

                { Name = sceneName; Block = commands; DebugInformation = debugScene })
        |> Intermediate
