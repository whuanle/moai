// <copyright file="FillUserInfoCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Common.Queries.Response;

namespace MoAI.User.Queries;

/// <summary>
/// 用户信息查询填充.
/// </summary>
public class FillUserInfoCommand : IRequest<FillUserInfoCommandResponse>
{
    /// <summary>
    /// 集合.
    /// </summary>
    public IReadOnlyCollection<AuditsInfo> Items { get; init; }
}
