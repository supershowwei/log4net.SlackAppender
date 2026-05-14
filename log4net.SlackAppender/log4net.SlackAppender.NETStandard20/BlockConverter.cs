using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using log4net.Appender;

namespace log4net.SlackAppender
{
    internal class BlockConverter : JsonConverter<Payload.Block>
    {
        public override Payload.Block Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (var doc = JsonDocument.ParseValue(ref reader))
            {
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "header": return JsonSerializer.Deserialize<Payload.HeaderBlock>(root.GetRawText(), options);
                    case "markdown": return JsonSerializer.Deserialize<Payload.MarkdownBlock>(root.GetRawText(), options);
                    default: throw new JsonException($"Unknown block type: {type}");
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, Payload.Block value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}