namespace YukimiScript.CommandLineTool

open System


exception FailException


module ErrorProcessing =
    let e2str = YukimiScript.Parser.ErrorStringing.schinese


    let private printError (fileName: string) (e: string) =
        lock stdout (fun _ ->
            Console.WriteLine(fileName + e))


    let private printErrorL (fileName: string) (lineNumber: int) (e: string) = 
        printError fileName <| "(" + string lineNumber + "):" + e

    
    let printExn fileName e = 
        printError fileName <| ":" + e2str e


    let printLExn fileName lineNumber e =
        printErrorL fileName lineNumber <| e2str e

