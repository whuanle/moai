// <copyright file="SizeInfo.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Enums;

namespace Maomi.AI.Infra.Models
{
    /// <summary>
    /// 大小信息.
    /// </summary>
    public struct SizeInfo : IEquatable<SizeInfo>
    {
        /// <summary>
        /// Byte 长度.
        /// </summary>
        public long Length { get; private set; }

        /// <summary>
        /// 大小.
        /// </summary>
        public decimal Size { get; private set; }

        /// <summary>
        /// 单位.
        /// </summary>
        public SizeUnit Unit { get; private set; }

        /// <summary>
        /// 将字节单位转换为合适的单位.
        /// </summary>
        /// <param name="byteLength">字节长度.</param>
        /// <returns>SizeInfo.</returns>
        public static SizeInfo Get(long byteLength)
        {
            SizeUnit unit = 0;
            decimal number = byteLength;
            if (byteLength < 1000)
            {
                return new SizeInfo()
                {
                    Length = byteLength,
                    Size = byteLength,
                    Unit = SizeUnit.B
                };
            }

            // 避免出现 1023B 这种情况；这样 1023B 会显示 0.99KB
            while (Math.Round(number / 1000) >= 1)
            {
                number = number / 1024;
                unit++;
            }

            return new SizeInfo
            {
                Size = Math.Round(number, 2),
                Unit = unit,
                Length = byteLength
            };
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is SizeInfo size)
            {
                return size.Length == Length;
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Length.GetHashCode();
        }

        /// <summary>
        /// 重载.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>是否相等.</returns>
        public static bool operator ==(SizeInfo left, SizeInfo right)
        {
            return left.Length == right.Length;
        }

        /// <summary>
        /// 重载.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>不相等.</returns>
        public static bool operator !=(SizeInfo left, SizeInfo right)
        {
            return left.Length != right.Length;
        }

        /// <inheritdoc/>
        public bool Equals(SizeInfo other)
        {
            return other.Length == Length;
        }
    }
}