// <copyright file="LLMParams.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.AiModel.Models;

/// <summary>
/// 语言模型的设置参数.
/// </summary>
public class LLMParams
{
    /// <summary>
    /// 控制生成文本中的惩罚系数，用于减少重复性
    /// </summary>
    public double? FrequencyPenalty { get; init; } = 0;

    /// <summary>
    /// 生成文本的最大长度
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 控制生成文本中的惩罚系数，用于减少主题的变化
    /// </summary>
    public double? PresencePenalty { get; init; } = 0;

    /// <summary>
    /// 生成文本的随机度量，用于控制文本的创造性和多样性
    /// </summary>
    public double? Temperature { get; init; } = 1;

    /// <summary>
    /// 控制生成文本中最高概率的单个 token
    /// </summary>
    public double? TopP { get; init; } = 1;
}