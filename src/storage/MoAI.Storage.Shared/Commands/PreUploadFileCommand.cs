// <copyright file="PreUploadFileCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Storage.Commands.Response;
using MoAI.Store.Enums;

namespace MoAI.Storage.Commands;

/// <summary>
/// 生成文件预上传地址.
/// </summary>
public class PreUploadFileCommand : IRequest<PreUploadFileCommandResponse>
{
    /// <summary>
    /// 文件可见性.
    /// </summary>
    public FileVisibility Visibility { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; set; } = default!;

    /// <summary>
    /// 文件 MD5.
    /// </summary>
    public string MD5 { get; set; } = default!;

    /// <summary>
    /// 文件路径，即 ObjectKey.
    /// </summary>
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// 预签名上传地址有效期.
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(2)!;
}
