using System.Text.Json.Serialization;
using System.Text.Json;

namespace System.Text.Json;

public class LongStringConverter : JsonConverter<long>
{
    // 从 JSON 字符串读取时调用
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string stringValue = reader.GetString();
            if (long.TryParse(stringValue, out long value))
            {
                return value;
            }
            throw new JsonException("Invalid format for long integer.");
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        throw new JsonException("Unexpected token type.");
    }

    // 写入 JSON 字符串时调用
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}