// <copyright file="TypeAsHelper.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MoAI.Infra.Helpers
{
    /// <summary>
    /// 类型转换工具.
    /// </summary>
    public static class TypeAsHelper
    {
        /// <summary>
        /// 字符串转特定类型.
        /// </summary>
        /// <typeparam name="TTargetValue">目标类型.</typeparam>
        /// <param name="sourceValue">字符串.</param>
        /// <returns>TTargetValue .</returns>
        public static TTargetValue? AS<TTargetValue>(string? sourceValue)
        {
            if (string.IsNullOrEmpty(sourceValue))
            {
                return default;
            }

            TypeCode c2 = Type.GetTypeCode(typeof(TTargetValue));
            if (c2 == TypeCode.String)
            {
                return Unsafe.As<string, TTargetValue>(ref sourceValue);
            }

            if (typeof(TTargetValue).IsValueType)
            {
                switch (c2)
                {
                    case TypeCode.Boolean:
                        bool v1 = Convert.ToBoolean(sourceValue, null);
                        return Unsafe.As<bool, TTargetValue>(ref v1);
                    case TypeCode.SByte:
                        sbyte v2 = Convert.ToSByte(sourceValue, null);
                        return Unsafe.As<sbyte, TTargetValue>(ref v2);
                    case TypeCode.Byte:
                        byte v3 = Convert.ToByte(sourceValue, null);
                        return Unsafe.As<byte, TTargetValue>(ref v3);
                    case TypeCode.Int16:
                        short v4 = Convert.ToInt16(sourceValue, null);
                        return Unsafe.As<short, TTargetValue>(ref v4);
                    case TypeCode.UInt16:
                        ushort v5 = Convert.ToUInt16(sourceValue, null);
                        return Unsafe.As<ushort, TTargetValue>(ref v5);
                    case TypeCode.Int32:
                        int v6 = Convert.ToInt32(sourceValue, null);
                        return Unsafe.As<int, TTargetValue>(ref v6);
                    case TypeCode.UInt32:
                        uint v7 = Convert.ToUInt32(sourceValue, null);
                        return Unsafe.As<uint, TTargetValue>(ref v7);
                    case TypeCode.Int64:
                        long v8 = Convert.ToInt64(sourceValue, null);
                        return Unsafe.As<long, TTargetValue>(ref v8);
                    case TypeCode.UInt64:
                        ulong v9 = Convert.ToUInt64(sourceValue, null);
                        return Unsafe.As<ulong, TTargetValue>(ref v9);
                    case TypeCode.Single:
                        float v10 = Convert.ToSingle(sourceValue, null);
                        return Unsafe.As<float, TTargetValue>(ref v10);
                    case TypeCode.Double:
                        double v11 = Convert.ToDouble(sourceValue, null);
                        return Unsafe.As<double, TTargetValue>(ref v11);
                    case TypeCode.Decimal:
                        decimal v12 = Convert.ToDecimal(sourceValue, null);
                        return Unsafe.As<decimal, TTargetValue>(ref v12);
                    case TypeCode.Char:
                        char v13 = Convert.ToChar(Convert.ToUInt16(sourceValue, null));
                        return Unsafe.As<char, TTargetValue>(ref v13);
                }
            }

            return JsonSerializer.Deserialize<TTargetValue>(sourceValue);
        }

        /// <summary>
        /// 值类型映射，支持以下类型互转：<br />
        /// <see cref="bool"/>、
        /// <see cref="sbyte"/>、
        /// <see cref="byte"/>、
        /// <see cref="short"/>、
        /// <see cref="ushort"/>、
        /// <see cref="int"/>、
        /// <see cref="uint"/>、
        /// <see cref="long"/>、
        /// <see cref="ulong"/>、
        /// <see cref="float"/>、
        /// <see cref="double"/>、
        /// <see cref="decimal"/>、
        /// <see cref="char"/> 。<br />
        /// 不支持 <see cref="DateTime"/> 。<br />
        /// 注意，浮点型转非浮点型，会出乎意料之外.
        /// </summary>
        /// <typeparam name="TSourceValue">源值类型.</typeparam>
        /// <typeparam name="TTargetValue">转换后值类型.</typeparam>
        /// <param name="sourceValue">源值.</param>
        /// <returns>TTargetValue.</returns>
        /// <exception cref="InvalidCastException">不支持的类型.</exception>
        public static TTargetValue AS<TSourceValue, TTargetValue>(TSourceValue sourceValue)
            where TSourceValue : struct
            where TTargetValue : struct
        {
            TypeCode c1 = Type.GetTypeCode(typeof(TSourceValue));
            TypeCode c2 = Type.GetTypeCode(typeof(TTargetValue));

            if (c1 == c2)
            {
                return Unsafe.As<TSourceValue, TTargetValue>(ref sourceValue);
            }

            if (c1 == TypeCode.DateTime || c2 == TypeCode.DateTime)
            {
                throw new InvalidCastException(
                    $"不支持该类型字段的转换： {typeof(TSourceValue).Name}  => {typeof(TTargetValue).Name}");
            }

            switch (c2)
            {
                case TypeCode.Boolean:
                    bool v1 = Convert.ToBoolean(sourceValue, null);
                    return Unsafe.As<bool, TTargetValue>(ref v1);
                case TypeCode.SByte:
                    sbyte v2 = Convert.ToSByte(sourceValue, null);
                    return Unsafe.As<sbyte, TTargetValue>(ref v2);
                case TypeCode.Byte:
                    byte v3 = Convert.ToByte(sourceValue, null);
                    return Unsafe.As<byte, TTargetValue>(ref v3);
                case TypeCode.Int16:
                    short v4 = Convert.ToInt16(sourceValue, null);
                    return Unsafe.As<short, TTargetValue>(ref v4);
                case TypeCode.UInt16:
                    ushort v5 = Convert.ToUInt16(sourceValue, null);
                    return Unsafe.As<ushort, TTargetValue>(ref v5);
                case TypeCode.Int32:
                    int v6 = Convert.ToInt32(sourceValue, null);
                    return Unsafe.As<int, TTargetValue>(ref v6);
                case TypeCode.UInt32:
                    uint v7 = Convert.ToUInt32(sourceValue, null);
                    return Unsafe.As<uint, TTargetValue>(ref v7);
                case TypeCode.Int64:
                    long v8 = Convert.ToInt64(sourceValue, null);
                    return Unsafe.As<long, TTargetValue>(ref v8);
                case TypeCode.UInt64:
                    ulong v9 = Convert.ToUInt64(sourceValue, null);
                    return Unsafe.As<ulong, TTargetValue>(ref v9);
                case TypeCode.Single:
                    float v10 = Convert.ToSingle(sourceValue, null);
                    return Unsafe.As<float, TTargetValue>(ref v10);
                case TypeCode.Double:
                    double v11 = Convert.ToDouble(sourceValue, null);
                    return Unsafe.As<double, TTargetValue>(ref v11);
                case TypeCode.Decimal:
                    decimal v12 = Convert.ToDecimal(sourceValue, null);
                    return Unsafe.As<decimal, TTargetValue>(ref v12);
                case TypeCode.Char:
                    char v13 = Convert.ToChar(Convert.ToUInt16(sourceValue, null));
                    return Unsafe.As<char, TTargetValue>(ref v13);
            }

            throw new InvalidCastException(
                $"不支持该类型字段的转换： {typeof(TSourceValue).Name}  => {typeof(TTargetValue).Name}");
        }
    }
}