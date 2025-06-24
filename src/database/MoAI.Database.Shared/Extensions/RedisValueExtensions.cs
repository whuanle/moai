// <copyright file="RedisValueExtensions.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using StackExchange.Redis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MaomiAI;

/// <summary>
/// RedisValue 扩展.
/// </summary>
public static class RedisValueExtensions
{
    /// <summary>
    /// 转换为对应的值.
    /// </summary>
    /// <typeparam name="T">任何类型.</typeparam>
    /// <param name="redisValue"></param>
    /// <param name="jsonSerializerOptions"></param>
    /// <returns>值.</returns>
    public static T? RedisValueTo<T>(this RedisValue redisValue, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (!redisValue.HasValue)
        {
            return default;
        }

        var typeCode = Type.GetTypeCode(typeof(T));

        switch (typeCode)
        {
            case TypeCode.Boolean:
                var v1 = (Boolean)redisValue;
                return Unsafe.As<Boolean, T>(ref v1);
            case TypeCode.SByte:
                var v2 = (SByte)redisValue;
                return Unsafe.As<SByte, T>(ref v2);
            case TypeCode.Byte:
                var v3 = (Byte)(uint)redisValue;
                return Unsafe.As<Byte, T>(ref v3);
            case TypeCode.Int16:
                var v4 = (Int16)redisValue;
                return Unsafe.As<Int16, T>(ref v4);
            case TypeCode.UInt16:
                var v5 = (Int16)(uint)redisValue;
                return Unsafe.As<Int16, T>(ref v5);
            case TypeCode.Int32:
                var v6 = (Int32)redisValue;
                return Unsafe.As<Int32, T>(ref v6);
            case TypeCode.UInt32:
                var v7 = (UInt32)redisValue;
                return Unsafe.As<UInt32, T>(ref v7);
            case TypeCode.Int64:
                var v8 = (Int64)redisValue;
                return Unsafe.As<Int64, T>(ref v8);
            case TypeCode.UInt64:
                var v9 = (UInt64)redisValue;
                return Unsafe.As<UInt64, T>(ref v9);
            case TypeCode.Single:
                var v10 = (Single)redisValue;
                return Unsafe.As<Single, T>(ref v10);
            case TypeCode.Double:
                var v11 = (Double)redisValue;
                return Unsafe.As<Double, T>(ref v11);
            case TypeCode.Decimal:
                var v12 = (Decimal)redisValue;
                return Unsafe.As<Decimal, T>(ref v12);
            case TypeCode.Char:
                char v13 = (Char)(uint)redisValue;
                return Unsafe.As<Char, T>(ref v13);
            case TypeCode.String:
                string? v14 = redisValue.ToString();
                if (v14 == null)
                {
                    return default;
                }

                return Unsafe.As<string, T>(ref v14);
            case TypeCode.Object:
                var v15 = System.Text.Json.JsonSerializer.Deserialize<T>(redisValue!, jsonSerializerOptions);
                return v15;
        }

        return default;
    }

    /// <summary>
    /// 类型转换为 RedisValue.
    /// </summary>
    /// <typeparam name="T">类型.</typeparam>
    /// <param name="t"></param>
    /// <param name="jsonSerializerOptions"></param>
    /// <returns><see cref="RedisValue"/>.</returns>
    public static RedisValue ToRedisValue<T>(this T t, JsonSerializerOptions? jsonSerializerOptions = null)
        where T : class
    {
        var text = System.Text.Json.JsonSerializer.Serialize<T>(t, jsonSerializerOptions);
        return new RedisValue(text);
    }
}
