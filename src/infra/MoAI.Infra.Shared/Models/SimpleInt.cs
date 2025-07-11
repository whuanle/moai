// <copyright file="SimpleInt.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// SimpleInt.
/// </summary>
public class SimpleInt : Simple<int>
{
    /// <summary>
    /// int.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator int(SimpleInt value) => value.Value;

    /// <summary>
    /// SimpleInt.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator SimpleInt(int value) => new SimpleInt { Value = value };

    /// <summary>
    /// ToInt32.
    /// </summary>
    /// <returns></returns>
    public int ToInt32()
    {
        return this.Value;
    }

    /// <summary>
    /// ToSimpleInt.
    /// </summary>
    /// <returns></returns>
    public SimpleInt ToSimpleInt()
    {
        return new SimpleInt() { Value = Value };
    }
}