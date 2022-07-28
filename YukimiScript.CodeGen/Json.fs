module YukimiScript.CodeGen.Json

open YukimiScript.Parser


let genJson debug (Intermediate scenes) targetFile = 
    use fileStream = 
        System.IO.File.Open (targetFile, System.IO.FileMode.Create)

    use writer = 
        new System.Text.Json.Utf8JsonWriter (fileStream)   

    writer.WriteStartArray()

    let writeDebugInfo (debugInfo: Elements.DebugInfo) =
        if debug then
            writer.WriteStartObject("debug")
            writer.WriteString("src", debugInfo.File)
            writer.WriteNumber("line", debugInfo.LineNumber)
            writer.WriteEndObject()

    for scene in scenes do 
        writer.WriteStartObject()
        writer.WriteString("scene", scene.Name)
        writeDebugInfo scene.DebugInformation
        
        begin
            writer.WriteStartArray("block")
            for call in scene.Block do
                writer.WriteStartObject()
                writer.WriteString("call", call.Callee)
                writer.WriteStartArray("args")
                for arg in call.Arguments do
                    match arg with
                    | Elements.Symbol "true" -> writer.WriteBooleanValue(true)
                    | Elements.Symbol "false" -> writer.WriteBooleanValue(false)
                    | Elements.Symbol "null" -> writer.WriteNullValue()
                    | Elements.String x -> writer.WriteStringValue(x)
                    | Elements.Symbol x -> writer.WriteStringValue(x)
                    | Elements.Integer x -> writer.WriteNumberValue(x)
                    | Elements.Real x -> writer.WriteNumberValue(x)
                writer.WriteEndArray()
                writeDebugInfo call.DebugInformation
                writer.WriteEndObject()
            writer.WriteEndArray()
        end

        writer.WriteEndObject()

    writer.WriteEndArray()

