// <copyright file="AiProviderInfo.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.AiModel.Models;

/// <summary>
/// 模型提供商信息.
/// </summary>
public class AiProviderInfo
{
    /// <summary>
    /// 类型.
    /// </summary>
    public AiProvider Provider { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// Icon 图标地址.
    /// </summary>
    public string Icon { get; init; } = default!;

    /// <summary>
    /// 默认端点.
    /// </summary>
    public string DefaultEndpoint { get; init; } = default!;
}