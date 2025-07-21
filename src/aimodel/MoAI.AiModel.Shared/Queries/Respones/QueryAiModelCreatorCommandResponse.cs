// <copyright file="QueryAiModelCreatorCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;

namespace MoAI.AiModel.Queries.Respones;

public class QueryAiModelCreatorCommandResponse
{
    public bool Exist { get; init; }
    public bool IsSystem { get; init; }
    public int CreatorId { get; init; }
    public bool IsPublic { get; init; }
}
