// <copyright file="EnumHelper.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Runtime.CompilerServices;

namespace MoAI.Infra.Helpers;

/// <summary>
/// 枚举帮助类.
/// </summary>
public static class EnumHelper
{
    /// <summary>
    /// 将 Flags 枚举值拆分为单独的枚举值数组.
    /// </summary>
    /// <typeparam name="TEnum">枚举类型.</typeparam>
    /// <param name="flagsValue"></param>
    /// <returns></returns>
    public static TEnum[] DecomposeFlags<TEnum>(TEnum flagsValue)
        where TEnum : Enum
    {
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
        {
            return new TEnum[] { flagsValue };
        }

        var result = new List<TEnum>();

        foreach (var value in EnumType<TEnum>.EnumValues)
        {
            if (flagsValue.HasFlag(value) && Convert.ToInt32(value) != 0)
            {
                result.Add(value);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// 将 Flags 枚举值拆分为单独的枚举值数组.
    /// </summary>
    /// <typeparam name="TEnum">枚举类型.</typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public static TEnum[] DecomposeFlags<TEnum>(int value)
        where TEnum : Enum
    {
        var flagsValue = Unsafe.As<int, TEnum>(ref value);

        return DecomposeFlags(flagsValue);
    }

    /// <summary>
    /// 将单独的枚举值数组组合成一个 Flags 枚举值.
    /// </summary>
    /// <typeparam name="TEnum">枚举类型.</typeparam>
    /// <param name="values"></param>
    /// <returns></returns>
    public static TEnum ComposeFlags<TEnum>(params TEnum[] values)
        where TEnum : Enum
    {
        int result = 0;
        foreach (var value in values)
        {
            result |= Convert.ToInt32(value);
        }

        return Unsafe.As<int, TEnum>(ref result);
    }

    private class EnumType<TEnum>
        where TEnum : Enum
    {
        public static readonly int[] _intValues = Array.Empty<int>();
        public static readonly TEnum[] _enumValues = Array.Empty<TEnum>();

        public static IReadOnlyCollection<int> IntValues => _intValues;

        public static IReadOnlyCollection<TEnum> EnumValues => _enumValues;

#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段
        static EnumType()
        {
            var vs = Enum.GetValues(typeof(TEnum));
            _enumValues = vs.OfType<TEnum>().ToArray();
            _intValues = new int[vs.Length];

            for (int i = 0; i < vs.Length; i++)
            {
                var item = _enumValues[i];
                _intValues[i] = Unsafe.As<TEnum, int>(ref item);
            }
        }
    }
}