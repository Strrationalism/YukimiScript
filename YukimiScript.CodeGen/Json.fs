module YukimiScript.CodeGen.Json

open YukimiScript.Parser

let private writeConstant (writer: System.Text.Json.Utf8JsonWriter) =
    function
    | Elements.Symbol "true" -> writer.WriteBooleanValue(true)
    | Elements.Symbol "false" -> writer.WriteBooleanValue(false)
    | Elements.Symbol "null" -> writer.WriteNullValue()
    | Elements.String x -> writer.WriteStringValue(x)
    | Elements.Symbol x -> writer.WriteStringValue(x)
    | Elements.Integer x -> writer.WriteNumberValue(x)
    | Elements.Real x -> writer.WriteNumberValue(x)

let genJson debug (Intermediate scenes) targetFile = 
    use fileStream = 
        System.IO.File.Open (targetFile, System.IO.FileMode.Create)

    use writer = 
        new System.Text.Json.Utf8JsonWriter (fileStream)   

    writer.WriteStartArray()

    let rec writeDebugInfo (debugInfo: Elements.DebugInfo) =
        writer.WriteString("src", debugInfo.File)
        writer.WriteNumber("line", debugInfo.LineNumber)
        match debugInfo.Scope with
        | Some (Choice1Of2 a) -> 
            writer.WriteString ("scope_type", "macro")
            writer.WriteString ("macro", a.Name)
        | Some (Choice2Of2 a) ->
            writer.WriteString ("scope_type", "scene")
            writer.WriteString ("scene", a.Name)
        | None ->
            writer.WriteNull ("scope_type")
        writer.WriteStartObject ("vars")
        for (name, var) in debugInfo.MacroVars do
            writer.WritePropertyName name
            writeConstant writer var
        writer.WriteEndObject ()

        writer.WriteStartObject ("outter")
        debugInfo.Outter |> Option.iter writeDebugInfo
        writer.WriteEndObject ()        

    for scene in scenes do 
        writer.WriteStartObject()
        writer.WriteString("scene", scene.Name)

        if debug then
            writer.WriteStartObject("debug")
            writeDebugInfo scene.DebugInformation
            writer.WriteEndObject()
        
        begin
            writer.WriteStartArray("block")
            for call in scene.Block do
                writer.WriteStartObject()
                writer.WriteString("call", call.Callee)
                writer.WriteStartArray("args")
                for arg in call.Arguments do
                    writeConstant writer arg
                    
                writer.WriteEndArray()
                if debug then
                    writer.WriteStartObject("debug")
                    writeDebugInfo call.DebugInformation
                    writer.WriteEndObject()
                writer.WriteEndObject()
            writer.WriteEndArray()
        end

        writer.WriteEndObject()

    writer.WriteEndArray()

