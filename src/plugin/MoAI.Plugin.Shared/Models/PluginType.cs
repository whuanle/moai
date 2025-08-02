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
    /// 系统插件.
    /// </summary>
    [JsonPropertyName("system")]
    System,

    /// <summary>
    /// MCP.
    /// </summary>
    [JsonPropertyName("mcp")]
    MCP,

    /// <summary>
    /// OpenAPI.
    /// </summary>
    [JsonPropertyName("openapi")]
    OpenApi,

    /// <summary>
    /// 知识库
    /// </summary>
    [JsonPropertyName("wiki")]
    Wiki
}
