// <copyright file="UploadLocalFilesCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Store.Enums;

namespace MoAI.Storage.Commands;

/// <summary>
/// 批量上传文件.
/// </summary>
public class UploadLocalFilesCommand : IRequest<UploadLocalFilesCommandResponse>
{
    /// <summary>
    /// 文件可见性.
    /// </summary>
    public FileVisibility Visibility { get; init; }

    /// <summary>
    /// 文件列表.
    /// </summary>
    public IReadOnlyCollection<FileUploadItem> Files { get; init; } = new List<FileUploadItem>();
}

public class FileUploadItem
{
    public string FileName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public string MD5 { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;
}

public class FileUploadResult
{
    public string FileName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public int FileId { get; init; }
}

public class UploadLocalFilesCommandResponse
{
    public IReadOnlyCollection<FileUploadResult> Files { get; init; } = new List<FileUploadResult>();
}
