// <copyright file="DBJsonHelper.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

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

    public static string ToDBString<T>(this T model)
    {
        return System.Text.Json.JsonSerializer.Serialize(model, _jsonOptions);
    }

    public static T? FromDBString<T>(this string json)
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
