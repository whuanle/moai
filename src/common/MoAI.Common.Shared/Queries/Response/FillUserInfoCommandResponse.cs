// <copyright file="FillUserInfoCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;

namespace MoAI.Common.Queries.Response;

public class FillUserInfoCommandResponse
{
    public IReadOnlyCollection<AuditsInfo> Items { get; init; }
}