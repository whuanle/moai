// <copyright file="ErrorResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// 错误信息.
/// </summary>
public class BusinessExceptionError
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 错误信息列表.
    /// </summary>
    public IReadOnlyCollection<string> Errors { get; init; } = default!;
}