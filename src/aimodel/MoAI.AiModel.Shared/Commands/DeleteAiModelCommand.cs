// <copyright file="DeleteAiModelCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Commands;

/// <summary>
/// 删除 ai 模型.
/// </summary>
public class DeleteAiModelCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public int AiModelId { get; init; }
}
