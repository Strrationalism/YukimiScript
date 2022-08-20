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


let loadSrcs paths =
    List.map loadDom paths
    |> ParserMonad.sequenceRL
    |> Result.map (List.fold Dom.merge Dom.empty)


let checkRepeat (dom: Dom) =
    let findRepeat (items: (string * Elements.DebugInfo) seq) =
        Seq.groupBy fst items
        |> Seq.choose
            (fun (key, matches) ->
                if Seq.length matches <= 1 then
                    None
                else
                    Some(key, matches |> Seq.map snd))
                    
    dom.Externs
    |> Seq.map (fun (Elements.ExternCommand (cmd, _), _, dbg) -> cmd, dbg)
    |> findRepeat
    |> Seq.tryHead
    |> function
        | None -> Ok dom
        | Some x ->
            Dom.ExternRepeatException x
            |> Error
    |> Result.bind (fun dom ->
        dom.Scenes
        |> Seq.map (fun (s, _, dbg) -> s.Name, dbg)
        |> findRepeat
        |> Seq.tryHead
        |> function
            | None -> Ok dom
            | Some x ->
                Dom.SceneRepeatException x
                |> Error)
    |> Result.bind (fun dom ->
        dom.Macros
        |> Seq.map (fun (s, _, _, dbg) -> s.Name, dbg)
        |> findRepeat
        |> Seq.tryHead
        |> function
            | None -> Ok dom
            | Some x ->
                Dom.MacroRepeatException x
                |> Error)


let checkLib = Ok


let loadLibs = loadSrcs >> Result.bind checkLib >> Result.bind checkRepeat


let getYkmFiles (path: string) =
    if File.Exists path
    then [|path|]
    else
        Directory.EnumerateFiles(path, "*.ykm", SearchOption.AllDirectories)
        |> Array.ofSeq


let generateIntermediate dom =
    checkRepeat dom
    |> Result.bind (Dom.expandTextCommands >> Dom.expandUserMacros)
    |> Result.bind (fun externAndSysMacro ->
        Dom.expandSystemMacros externAndSysMacro
        |> Dom.linkToExternCommands
        |> Result.map Intermediate.ofDom)


let compile lib srcPath =
    loadDom srcPath
    |> Result.map (Dom.merge lib)
    |> Result.bind generateIntermediate
    

