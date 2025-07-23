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
/// 添加一个用户私有的模型.
/// </summary>
public class AddUserAiModelRequest : AiEndpoint, IRequest<SimpleInt>
{
}
