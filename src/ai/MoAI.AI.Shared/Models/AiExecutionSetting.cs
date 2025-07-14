// <copyright file="AiExecutionSetting.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.AI.Models;

/// <summary>
/// AI 对话属性，不同模型的属性设置不一样.
/// </summary>
public class AiExecutionSetting
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 值.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
