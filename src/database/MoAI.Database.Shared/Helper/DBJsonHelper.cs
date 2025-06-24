using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Database.Helper;

public static class DBJsonHelper
{
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        AllowTrailingCommas = true
    };

    public static string ToJsonString<T>(T model)
    {
        return System.Text.Json.JsonSerializer.Serialize(model, _jsonOptions);
    }

    public static T? FromJsonString<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            return default;
        }
    }
}
