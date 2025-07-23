// <copyright file="AddUserAiModelRequest.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.AiModel.User.Models;

/// <summary>
/// 修改一个用户私有的模型.
/// </summary>
public class UpdateUserAiModelRequest : AiEndpoint, IRequest<SimpleInt>
{
    /// <summary>
    /// AI 模型 id.
    /// </summary>
    public int AiModelId { get; init; }
}
