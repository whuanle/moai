// <copyright file="SystemOptions.cs" company="MaomiAI">
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
    /// 服务访问地址.
    /// </summary>
    public string Server { get; init; } = string.Empty;

    /// <summary>
    /// 文件存储路径.
    /// </summary>
    public SystemOptionsStorage Storage { get; init; } = new SystemOptionsStorage();

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
    public DatabaseStore Wiki { get; init; } = new DatabaseStore();
}

public class SystemOptionsStorage
{
    public string Type { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;

    public SystemOptionsStorageS3 S3Public { get; init; } = new SystemOptionsStorageS3();
    public SystemOptionsStorageS3 S3Private { get; init; } = new SystemOptionsStorageS3();
}

public class SystemOptionsStorageS3
{
    /// <summary>
    /// 节点.
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// 是否强制使用路径样式，flase 时会自动在 Endpoint 前加上存储桶路径，true 时在 Endpoint 后面加上存储桶路径.
    /// </summary>
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>
    /// 存储桶.
    /// </summary>
    public string Bucket { get; init; } = string.Empty;

    /// <summary>
    /// AccessKeyId.
    /// </summary>
    public string AccessKeyId { get; init; } = string.Empty;

    /// <summary>
    /// AccessKeySecret.
    /// </summary>
    public string AccessKeySecret { get; init; } = string.Empty;
}