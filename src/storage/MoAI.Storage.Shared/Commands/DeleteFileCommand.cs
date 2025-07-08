// <copyright file="DeleteFileCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 删除文件.
/// </summary>
public class DeleteFileCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 文件id.
    /// </summary>
    public int FileId { get; init; }
}
