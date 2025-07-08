// <copyright file="PreUploadWikiDocumentCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Wiki.Documents.Commands.Responses;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 预上传知识库文件.
/// </summary>
public class PreUploadWikiDocumentCommand : IRequest<PreloadWikiDocumentResponse>
{
    /// <summary>
    ///  知识库 id.
    /// </summary>
    public int WikiId { get; init; } = default!;

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
}
