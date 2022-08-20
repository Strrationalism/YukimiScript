namespace YukimiScript.Parser.Utils


exception MultiException of exn list


module Result =  

    let transposeList listOfResult =
        let oks, errs =
            List.partition 
                (function Ok _ -> true | Error _ -> false)
                listOfResult
        
        let oks = List.map (function Ok x -> x | _ -> failwith "") oks
        match List.map (function Error e -> e | _ -> failwith "") errs with
        | [] -> Ok oks
        | errs -> Error <| MultiException errs

