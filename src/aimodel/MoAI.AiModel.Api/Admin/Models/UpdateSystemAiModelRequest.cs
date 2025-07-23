// <copyright file="AddUserAiModelRequest.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Admin.Models;

/// <summary>
/// 修改一个系统级模型.
/// </summary>
public class UpdateSystemAiModelRequest : AiEndpoint, IRequest<SimpleInt>
{
    /// <summary>
    /// AI 模型 id.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 公开使用，只有系统模型才能公开使用.
    /// </summary>
    public bool IsPublic { get; init; }
}
