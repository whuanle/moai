// <copyright file="LongStringConverter.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace System.Text.Json;

public class LongStringConverter : JsonConverter<long>
{
    // 从 JSON 字符串读取时调用

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}