// <copyright file="SimpleInt.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

public class SimpleInt : Simple<int>
{
    public static implicit operator int(SimpleInt value)
    {
        return value.Value;
    }

    public static implicit operator SimpleInt(int value)
    {
        return new SimpleInt { Value = value };
    }

    public int ToInt32()
    {
        throw new NotImplementedException();
    }

    public SimpleInt ToSimpleInt()
    {
        throw new NotImplementedException();
    }
}