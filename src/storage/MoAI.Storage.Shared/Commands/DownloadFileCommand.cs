// <copyright file="DownloadFileCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Store.Enums;

namespace MoAI.Storage.Commands;

/// <summary>
/// 下载文件.
/// </summary>
public class DownloadFileCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 文件可见性
    /// </summary>
    public FileVisibility Visibility { get; init; }

    /// <summary>
    /// key.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <summary>
    /// 本地路径.
    /// </summary>
    public string StoreFilePath { get; init; } = default!;
}
