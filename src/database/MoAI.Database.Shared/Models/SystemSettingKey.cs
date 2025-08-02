// <copyright file="SystemSettingKey.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Database.Models;

/// <summary>
/// 系统配置项.
/// </summary>
public struct SystemSettingKey : IEquatable<SystemSettingKey>
{
    /// <summary>
    /// key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// 值.
    /// </summary>
    public string Value { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// 是否可以更新.
    /// </summary>
    public bool IsEdit { get; init; }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is SystemSettingKey other)
        {
            return Key == other.Key;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Equals(SystemSettingKey other)
    {
        return Key == other.Key;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Key);
    }

    /// <summary>
    /// 重载相等运算符.
    /// </summary>
    /// <param name="left">左操作数.</param>
    /// <param name="right">右操作数.</param>
    /// <returns>如果相等返回 true，否则返回 false.</returns>
    public static bool operator ==(SystemSettingKey left, SystemSettingKey right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// 重载不相等运算符.
    /// </summary>
    /// <param name="left">左操作数.</param>
    /// <param name="right">右操作数.</param>
    /// <returns>如果不相等返回 true，否则返回 false.</returns>
    public static bool operator !=(SystemSettingKey left, SystemSettingKey right)
    {
        return !(left == right);
    }
}
