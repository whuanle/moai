// <copyright file="ComplateUploadWikiDocumentCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 结束上传文件.
/// </summary>
public class ComplateUploadWikiDocumentCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库ID.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; set; }
}