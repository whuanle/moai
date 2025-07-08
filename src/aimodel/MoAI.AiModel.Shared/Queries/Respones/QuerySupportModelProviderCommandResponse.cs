// <copyright file="QuerySupportModelProviderCommand.cs" company="MaomiAI">
// Copyright (c) MaomiAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

using MaomiAI.AiModel.Shared.Models;
using MediatR;

namespace MoAI.AiModel.Queries.Respones;

public class QuerySupportModelProviderCommandResponse
{
    /// <summary>
    /// 支持的模型供应商列表.
    /// </summary>
    public IReadOnlyCollection<AiProviderInfo> Providers { get; init; } = new List<AiProviderInfo>();
}