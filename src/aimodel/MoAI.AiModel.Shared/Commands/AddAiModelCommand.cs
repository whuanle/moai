// <copyright file="AddAiModelCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MaomiAI.AiModel.Shared.Commands;

/// <summary>
/// 添加 AI 模型.
/// </summary>
public class AddAiModelCommand : AiEndpoint, IRequest<SimpleInt>
{
}
