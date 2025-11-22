using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra.System.Text.Json;

public class JsonSerializerOptionValues
{
    public static readonly JsonSerializerOptions UnsafeRelaxedJsonEscaping;

    static JsonSerializerOptionValues()
    {
        UnsafeRelaxedJsonEscaping = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            //DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}