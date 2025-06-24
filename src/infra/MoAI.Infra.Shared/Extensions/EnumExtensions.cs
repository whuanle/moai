// <copyright file="EnumExtensions.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MaomiAI;

/// <summary>
/// EnumExtensions.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Convert to int.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>int.</returns>
    public static int ToInt(this Enum value)
    {
        return Convert.ToInt32(value);
    }

    /// <summary>
    /// 将字符串转换为指定的枚举类型。
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型。</typeparam>
    /// <param name="value">要转换的字符串。</param>
    /// <param name="ignoreCase">是否忽略大小写，默认为 true。</param>
    /// <returns>转换后的枚举值。</returns>
    /// <exception cref="ArgumentException">如果 value 不是有效的枚举名称。</exception>
    public static TEnum ToEnum<TEnum>(this string value, bool ignoreCase = true)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(value));
        }

        return Enum.Parse<TEnum>(value, ignoreCase);
    }
}
