module YukimiScript.Parser.Diagram


type SceneNode = 
    { SceneName: string }


type FileNode = 
    { FileName: string
      Scenes: SceneNode list }


type LinkNode =
    { From: SceneNode
      To: SceneNode }


type Diagram =
    { Files: FileNode
      Links: LinkNode }


let analyzeDiagram (path: string) : Result<Diagram, exn> = failwith "No Impl"


let generateDgml (diagram: Diagram) : string = failwith "No Impl"

