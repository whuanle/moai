// <copyright file="KeyValue.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

public class KeyValue<TKey, TValue>
{
    public TKey Key { get; set; } = default!;

    public TValue Value { get; set; } = default!;

    public KeyValue(TKey key, TValue value)
    {
        Value = value;
    }

    public KeyValue()
    {
    }
}
