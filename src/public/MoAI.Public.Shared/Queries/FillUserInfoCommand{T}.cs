// <copyright file="FillUserInfoCommand{T}.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Public.Queries.Response;

namespace MoAI.User.Queries;

/// <summary>
/// 用户信息查询填充.
/// </summary>
/// <typeparam name="T">带有审计属性的.</typeparam>
public class FillUserInfoCommand : IRequest<FillUserInfoCommandResponse>
{
    public IReadOnlyCollection<AuditsInfo> Items { get; init; }
}
