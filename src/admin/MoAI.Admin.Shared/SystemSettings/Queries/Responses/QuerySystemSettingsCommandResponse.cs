// <copyright file="QuerySystemSettingsCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Admin.SystemSettings.Queries.Responses;

/// <summary>
/// QuerySystemSettingsCommandResponse.
/// </summary>
public class QuerySystemSettingsCommandResponse
{
    public IReadOnlyCollection<QuerySystemSettingsCommandResponseItem> Items { get; init; } = Array.Empty<QuerySystemSettingsCommandResponseItem>();
}
