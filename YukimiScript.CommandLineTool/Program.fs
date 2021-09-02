open System
open System.IO
open YukimiScript.Parser
open YukimiScript.Parser.ParserMonad
open YukimiScript.CommandLineTool


let private help () =
    [ "Yukimi Script Command Tool"
      "by Strrationalism Studio 2021"
      ""
      "Usage: ykmc <path-to-scripts> [options]"
      ""
      "Options:"
      "    -lib <libDir>         Add other library."
      "    -dgml <output>        Create the diagram."
      "    -voicedoc <outDir>    Create the voice documents."
      ""
      "Examples:"
      "    Check the scripts:"
      "        ykmc \"./scripts\" -lib \"./api\""
      "    Create the diagram from scripts:"
      "        ykmc \"./scripts\" -lib \"./api\" -dgml \"./diagram.dgml\""
      "    Create the voice documents:"
      "        ykmc \"./scripts\" -lib \"./api\" -voicedoc \"./outdir\""
      "" ]
    |> Seq.iter Console.WriteLine


type Option =
    { ScriptDir: string
      VoiceDocumentOutputDir: string option
      DiagramOutputFile: string option
      LibraryDirs: string list }


exception private OptionErrorException


let private optionParser =
    let arg = 
        parser {
            do! Basics.whitespace1
            return! Constants.stringParser
        }
        |> name "command line arg"

    let rec options cur = 
        arg
        |> bind (function
            | "-lib" ->
                arg
                |> bind (fun lib ->
                    { cur with LibraryDirs = lib :: cur.LibraryDirs } 
                    |> options)
            | "-dgml" ->
                if cur.DiagramOutputFile.IsSome then
                    raise OptionErrorException
                else 
                    arg
                    |> bind (fun dgml ->
                        { cur with DiagramOutputFile = Some dgml }
                        |> options)
            | "-voicedoc" ->
                if cur.VoiceDocumentOutputDir.IsSome then
                    raise OptionErrorException
                else
                    arg
                    |> bind (fun voice ->
                        { cur with VoiceDocumentOutputDir = Some voice }
                        |> options)
            | _ -> raise OptionErrorException)
        |> zeroOrOne
        |> map (Option.defaultValue cur)

    parser {
        let! scriptDir = arg
        return! options  { 
            ScriptDir = scriptDir
            VoiceDocumentOutputDir = None
            DiagramOutputFile = None
            LibraryDirs = [] 
        }
    }
    

[<EntryPoint>]
let main argv =
    argv
    |> Array.map (fun x -> 
        match x.Trim() with
        | x when x.StartsWith "\"" && x.EndsWith "\"" ->
            x
        | x when x.StartsWith "\"" -> x + "\""
        | x when x.EndsWith "\"" -> "\"" + x
        | x -> "\"" + x + "\"")
    |> Array.fold (fun a b -> a + " " + b) ""
    |> fun x -> " " + x.Trim ()
    |> fun argv -> run argv optionParser
    |> function
        | Error _ -> 
            help ()
            0
        | Ok option ->
            try
                let project = Project.openProject option.ScriptDir
                let libs =
                    option.LibraryDirs
                    |> Seq.collect (fun libDir ->
                        Directory.EnumerateFiles(
                            libDir, 
                            "*.ykm", 
                            SearchOption.AllDirectories))

                // 1. 处理Lib
                // 1.1 加载Lib并编译为Line array
                // 1.2 输出Line array中的Error
                // 1.3 转换为Dom
                // 1.4 输出转换为Dom过程中的Error
                // 1.5 检查是否存在Scene，如果存在则报错
                // 1.6 合并所有的Lib
                // 1.7 检查是否存在重复的Macros和Externs
                // 2. 处理Scenario
                // 2.1 加载Scenario并编译为Line array
                // 2.2 输出Line array中的Error
                // 2.3 转换为Dom
                // 2.4 输出转换为Dom过程中的Error
                // 2.5 检查是否存在重复的Scenes、Macros和Externs
                // 2.6 输出配音稿
                // 2.7 载入Lib的内容，检查是否存在重复的Scenes、Macros和Externs
                // 2.8 展开文本命令
                // 2.9 展开用户宏
                // 2.10 绘制分支图
                // 3. 处理Program
                // 3.1 加载Program并编译为Line array
                // 3.2 输出Line array中的error
                // 3.3 转换为Dom
                // 3.4 输出转换为Dom过程中的Error
                // 3.5 检查是否存在重复的Scenes、Macros和Externs
                // 3.6 载入Lib内容，检查是否存在重复的Scenes、Macros和Externs
                // 3.7 展开文本命令
                // 3.8 展开用户宏
                // 4. 合并以上所有内容并检查是否存在重复的Scenes、Macros和Externs
                // 5. 展开系统宏
                // 6. 链接外部函数
                // 7. 生成目标代码
                
                    
                0
            with 
            | :? FailException -> 
                Console.WriteLine()
                -1
            | e ->
                Console.WriteLine(e.Message)
                -1
