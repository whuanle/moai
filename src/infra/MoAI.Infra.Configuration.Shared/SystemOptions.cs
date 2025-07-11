// <copyright file="SystemOptions.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;

namespace MoAI.Infra;

/// <summary>
/// 系统配置.
/// </summary>
public class SystemOptions
{
    /// <summary>
    /// 系统名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 服务访问地址.
    /// </summary>
    public string Server { get; init; } = string.Empty;

    /// <summary>
    /// 前端地址.
    /// </summary>
    public string WebUI { get; init; } = string.Empty;

    /// <summary>
    /// 文件路径.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// 加密密钥.
    /// </summary>
    public string AES { get; init; } = string.Empty;

    /// <summary>
    /// 系统数据库类型.
    /// </summary>
    public string DBType { get; init; } = string.Empty;

    /// <summary>
    /// 系统数据库连接字符串.
    /// </summary>
    public string Database { get; init; } = string.Empty;

    /// <summary>
    /// Redis 连接字符串.
    /// </summary>
    public string Redis { get; init; } = string.Empty;

    /// <summary>
    /// RabbitMQ 连接字符串.
    /// </summary>
    public string RabbitMQ { get; init; } = string.Empty;

    /// <summary>
    /// 文档向量化存储.
    /// </summary>
    public DatabaseStorage Wiki { get; init; } = new DatabaseStorage();
}