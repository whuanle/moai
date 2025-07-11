// <copyright file="PluginType.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.Text.Json.Serialization;

namespace MoAI.Plugin.Models;

/// <summary>
/// 插件类型.
/// </summary>
public enum PluginType
{
    /// <summary>
    /// OpenAPi 插件.
    /// </summary>
    [JsonPropertyName("openapi")]
    OpenApi,

    /// <summary>
    /// mcp 插件.
    /// </summary>
    [JsonPropertyName("mcp")]
    Mcp,

    /// <summary>
    /// 系统插件.
    /// </summary>
    [JsonPropertyName("system")]
    System,
}
