// <copyright file="UploadLocalFilesCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Store.Enums;

namespace MoAI.Storage.Commands;

public class UploadFileStreamCommand : IRequest<FileUploadResult>
{
    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ObjectKey { get; init; } = string.Empty;
    public string MD5 { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}
