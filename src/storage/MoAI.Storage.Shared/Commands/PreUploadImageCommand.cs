// <copyright file="PreUploadImageCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Storage.Commands.Response;
using MoAI.Store.Enums;

namespace MoAI.Storage.Commands;

/// <summary>
/// 上传图像，例如头像、公有图像等，文件公开访问，都根路径下.<br />
/// </summary>
public class PreUploadImageCommand : IRequest<PreUploadFileCommandResponse>
{
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
    /// 设置文件类型存放文件分类.
    /// </summary>
    public UploadFileType ImageType { get; set; } = default!;
}
