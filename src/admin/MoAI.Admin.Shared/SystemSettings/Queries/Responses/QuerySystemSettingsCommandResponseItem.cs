// <copyright file="QuerySystemSettingsCommandResponse.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;

namespace MoAI.Admin.SystemSettings.Queries.Responses;

/// <summary>
/// QuerySystemSettingsCommandResponseItem.
/// </summary>
public class QuerySystemSettingsCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 配置名称.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 配置值.
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;
}