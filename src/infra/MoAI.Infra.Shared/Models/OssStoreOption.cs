// <copyright file="SystemStoreOption.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Infra.Models;

/// <summary>
/// 文件存储选项.
/// </summary>
public class OssStoreOption
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