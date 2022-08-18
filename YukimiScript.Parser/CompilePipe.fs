module YukimiScript.Parser.CompilePipe

open System.IO


exception ParseLinesException of (int * exn) list


let loadDom path =
    try Ok <| File.ReadAllLines path with e -> Error e
    |> Result.bind (Parser.parseLines >> Result.mapError ParseLinesException)
    |> Result.bind (Dom.analyze <| Path.GetFileName path)

    
exception CanNotFindLib of string list


let findLib libDirs (libName: string) =
    let libFileName =
        if libName.ToLower().EndsWith ".ykm" 
            then libName 
            else "lib" + libName + ".ykm"

    libDirs
    |> List.tryPick (fun x ->
        let libPath = Path.Combine (x, "/" + libFileName)
        if Directory.Exists libPath || File.Exists libPath 
            then Some libPath else None)

    
let findLibs libDirs libs =
    let succs, fails =
        List.foldBack
            (fun libName (succs, fails) ->
                match findLib libDirs libName with
                | Some path -> (path :: succs, fails)
                | None -> (succs, libName :: fails))
            libs
            ([], [])
    
    if fails <> [] then Error <| CanNotFindLib fails else Ok succs

