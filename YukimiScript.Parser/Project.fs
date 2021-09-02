module YukimiScript.Parser.Project

open System.IO


type Project =
    { Library: string seq
      Scenario: string seq
      Program: string seq }


let openProject (path: string) : Project =
    let search subDir =
        match Path.Combine(path, subDir) with
        | lib when Directory.Exists(lib) ->
            Directory.EnumerateFiles(lib, "*.ykm", SearchOption.AllDirectories)
        | _ -> Seq.empty
        
    let lib = search "lib"
    let scenario = search "scenario"
    let program = 
        search "" 
        |> Seq.except lib 
        |> Seq.except scenario

    { Library = lib
      Scenario = scenario
      Program = program }
    
