// <copyright file="QueryPromptCreateUserCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;

namespace MoAI.Prompt.Queries;

public class QueryPromptCreateUserCommand : IRequest<QueryPromptCreateUserCommandResponse>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }
}

public class QueryPromptCreateUserCommandResponse
{
    public int UserId { get; init; }
}
