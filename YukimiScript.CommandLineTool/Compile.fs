module YukimiScript.CommandLineTool.Compile

open System.IO
open YukimiScript.Parser


exception FailException


let unwrapParseException (errStringing: ErrorStringing.ErrorStringing) fileName =
    function
    | Ok x -> x
    | Error ls ->
        ls
        |> List.iter
            (fun (lineNumber, err) ->
                Path.GetFileName(fileName: string)
                + "("
                + string lineNumber
                + "):"
                + errStringing err
                |> stderr.WriteLine)

        raise FailException


let unwrapDomException (errorStringing: ErrorStringing.ErrorStringing) =
    function
    | Ok x -> x
    | Error err ->
        errorStringing err |> stderr.WriteLine
        raise FailException


let private loadDom errStringing src =
    File.ReadAllLines src
    |> Parser.parseLines
    |> unwrapParseException errStringing src
    |> Dom.analyze src
    |> unwrapDomException errStringing


let private findRepeat (items: (string * Elements.DebugInformation) seq) =
    Seq.groupBy fst items
    |> Seq.choose
        (fun (key, matches) ->
            if Seq.length matches <= 1 then
                None
            else
                Some(key, matches |> Seq.map snd))


let checkRepeat errStringing (dom: Dom) =
    dom.Externs
    |> Seq.map (fun (Elements.ExternCommand (cmd, _), _, dbg) -> cmd, dbg)
    |> findRepeat
    |> Seq.tryHead
    |> function
        | None -> ()
        | Some x ->
            Dom.ExternRepeatException x
            |> Error
            |> unwrapDomException errStringing

    dom.Scenes
    |> Seq.map (fun (s, _, dbg) -> s.Name, dbg)
    |> findRepeat
    |> Seq.tryHead
    |> function
        | None -> ()
        | Some x ->
            Dom.SceneRepeatException x
            |> Error
            |> unwrapDomException errStringing

    dom.Macros
    |> Seq.map (fun (s, _, _, dbg) -> s.Name, dbg)
    |> findRepeat
    |> Seq.tryHead
    |> function
        | None -> ()
        | Some x ->
            Dom.MacroRepeatException x
            |> Error
            |> unwrapDomException errStringing

    dom


let getYkmFiles (path: string) =
    if File.Exists path
    then [|path|]
    else
        Directory.EnumerateFiles(path, "*.ykm", SearchOption.AllDirectories)
        |> Array.ofSeq


let loadLib errStringing libPath =
    let doms =
        getYkmFiles libPath
        |> Array.map (fun srcPath -> srcPath, loadDom errStringing srcPath)

    doms
    |> Array.iter
        (fun (fileName, dom) ->
            if Seq.isEmpty dom.Scenes |> not then
                unwrapDomException errStringing
                <| Error(Dom.CannotDefineSceneInLibException fileName))

    doms
    |> Array.map snd
    |> Array.fold Dom.merge Dom.empty


let loadLibs errStringing libPaths =
    libPaths
    |> List.map (loadLib errStringing)
    |> List.fold Dom.merge Dom.empty


let loadSrc errStringing (lib: Dom) srcPath =
    Dom.merge lib (loadDom errStringing srcPath)
    |> Dom.expandTextCommands
    |> Dom.expandUserMacros
    |> unwrapDomException errStringing


let prepareCodegen errStringing dom =
    Dom.expandSystemMacros dom
    |> Dom.linkToExternCommands
    |> unwrapDomException errStringing
