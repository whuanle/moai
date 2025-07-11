// <copyright file="KeyValue.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// kv.
/// </summary>
/// <typeparam name="TKey">key.</typeparam>
/// <typeparam name="TValue">value.</typeparam>
public class KeyValue<TKey, TValue>
{
    /// <summary>
    /// Key.
    /// </summary>
    public TKey Key { get; set; } = default!;

    /// <summary>
    /// Value.
    /// </summary>
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyValue{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public KeyValue(TKey key, TValue value)
    {
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyValue{TKey, TValue}"/> class.
    /// </summary>
    public KeyValue()
    {
    }
}
