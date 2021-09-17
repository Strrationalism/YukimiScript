module YukimiScript.Parser.Diagram

open YukimiScript.Parser.Elements
open YukimiScript.Parser.Dom
open YukimiScript.Parser.Macro

type SceneNode =
    { Name: string }


type FileNode =
    { Name: string
      Scenes: SceneNode list }

 
type SceneArrow =
    { From: SceneNode
      Target: SceneNode }


type Diagram = 
    { Files: FileNode list
      Arrows: SceneArrow list }


exception DiagramMacroErrorException of DebugInformation


exception CannotFindSceneException of string


let analyze (files: (string * Dom) list) : Result<Diagram> =
    try
        let fileNodes, arrows =
            files
            |> List.map (fun (fileName, dom) ->
                let scenes = 
                    dom.Scenes
                    |> List.map (fun (scene, block, _) ->
                        let linkTo = 
                            block
                            |> List.choose (function
                                | CommandCall c, debug when c.Callee = "__diagram_link_to" ->
                                    let p = { Parameter = "target"; Default = None }
                                    matchArguments debug [p] c
                                    |> function
                                        | Ok [ "target", (String target) ] -> 
                                            Some target
                                        | Error e -> raise e
                                        | _ -> raise <| DiagramMacroErrorException debug
                                | _ -> None)
                        { Name = scene.Name }, linkTo
                    )
                
                { Name = fileName; Scenes = List.map fst scenes },
                scenes)
            |> List.unzip

        let arrows =
            let scenes = Seq.collect (fun x -> x.Scenes) fileNodes
            arrows
            |> List.concat
            |> List.collect (fun (src, dst) ->
                dst
                |> List.map (fun dst ->
                    scenes
                    |> Seq.tryFind (fun x -> x.Name = dst)
                    |> function
                        | None -> raise <| CannotFindSceneException dst
                        | Some x -> src, x))
            |> List.map (fun (a, b) -> { From = a; Target = b })

        Ok { Files = fileNodes; Arrows = arrows }
    with e -> Error e


let exportDgml (diagram: Diagram) : string =
    let sb = System.Text.StringBuilder ()

    sb  .AppendLine("""<?xml version="1.0" encoding="utf-8"?>""")
        .AppendLine("""<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">""")
        .AppendLine("""  <Nodes>""") |> ignore

    for file in diagram.Files do
        sb  .Append("    <Node Id=\"")
            .Append(file.Name)
            .Append("\" Group=\"Expanded\" />")
            .AppendLine() |> ignore
        
        for scene in file.Scenes do
            sb  .Append("    <Node Id=\"")
                .Append(scene.Name)
                .Append("\" />")
                .AppendLine() |> ignore
    
    sb  .AppendLine("  </Nodes>")
        .AppendLine("  <Links>") |> ignore

    for file in diagram.Files do
        for scene in file.Scenes do
            sb  .Append("    <Link Source=\"")
                .Append(file.Name)
                .Append("\" Target=\"")
                .Append(scene.Name)
                .Append("\" Category=\"Contains\" />")
                .AppendLine () |> ignore

    for arrows in diagram.Arrows do
        sb  .Append("    <Link Source=\"")
            .Append(arrows.From.Name)
            .Append("\" Target=\"")
            .Append(arrows.Target.Name)
            .Append("\" />")
            .AppendLine() |> ignore
    
    sb  .AppendLine("  </Links>")
        .AppendLine("  <Categories>")
        .AppendLine("    <Category")
        .AppendLine("      Id=\"Contains\" ")
        .AppendLine("      Label=\"Contains\" ")
        .AppendLine("      CanBeDataDriven=\"False\" ")
        .AppendLine("      IncomingActionLabel=\"Contained By\" ")
        .AppendLine("      IsContainment=\"True\"")
        .AppendLine("      OutgoingActionLabel=\"Contains\" />")
        .AppendLine("  </Categories>")
        .AppendLine("</DirectedGraph>")
        .ToString()