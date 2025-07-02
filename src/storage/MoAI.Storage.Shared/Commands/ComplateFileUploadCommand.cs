// <copyright file="ComplateFileUploadCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Storage.Commands.Response;

namespace MoAI.Storage.Commands;

/// <summary>
/// 结束上传文件.
/// </summary>
public class ComplateFileUploadCommand : IRequest<ComplateFileCommandResponse>
{
    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; set; }
}
