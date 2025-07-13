// <copyright file="TextToJsonExtensions.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Extensions;

/// <summary>
/// json 对象转换.
/// </summary>
public static class TextToJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        AllowTrailingCommas = true,
    };

    static TextToJsonExtensions()
    {
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, true));
        _jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }

    /// <summary>
    /// 转换为 json 字符串.
    /// </summary>
    /// <typeparam name="T">类型.</typeparam>
    /// <param name="model"></param>
    /// <returns></returns>
    public static string ToJsonString<T>(this T? model)
    {
        if (model == null)
        {
            return string.Empty;
        }

        var result = JsonSerializer.Serialize(model, _jsonOptions);

        if (typeof(T).IsEnum)
        {
            return result.Trim('"');
        }

        // 解析可空类型
        var nullType = Nullable.GetUnderlyingType(typeof(T));
        if (nullType != null && nullType.IsEnum)
        {
            return result.Trim('"');
        }

        return result;
    }

    /// <summary>
    /// json 转换为对象.
    /// </summary>
    /// <typeparam name="T">类型.</typeparam>
    /// <param name="json"></param>
    /// <returns></returns>
    public static T? JsonToObject<T>(this string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        if (typeof(T).IsEnum)
        {
            json = $"\"{json}\"";
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
