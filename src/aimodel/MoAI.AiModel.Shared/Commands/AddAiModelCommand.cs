// <copyright file="AddAiModelCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Commands;

/// <summary>
/// 添加 AI 模型.
/// </summary>
public class AddAiModelCommand : AiEndpoint, IRequest<SimpleInt>
{
    /// <summary>
    /// 是否系统模型，创建后无法修改该属性.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 公开使用，只有系统模型才能公开使用.
    /// </summary>
    public bool IsPublic { get; init; }
}
