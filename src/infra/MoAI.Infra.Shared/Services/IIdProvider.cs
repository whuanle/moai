// <copyright file="IdProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Services
{
    /// <summary>
    /// Snowflake id generator.<br />
    /// 雪花 id 生成器.
    /// </summary>
    public interface IIdProvider
    {
        /// <summary>
        /// 生成雪花 id.
        /// </summary>
        /// <param name="value">雪花 id 字符串.</param>
        /// <returns>雪花 id.</returns>
        long GeneratorId(out string value);

        /// <summary>
        /// 生成一个字符串 key.
        /// </summary>
        /// <returns>key.</returns>
        string GeneratorKey();

        /// <summary>
        /// Get snowflake id.<br />
        /// 获取雪花 id.
        /// </summary>
        /// <returns>id.</returns>
        long NextId();
    }
}