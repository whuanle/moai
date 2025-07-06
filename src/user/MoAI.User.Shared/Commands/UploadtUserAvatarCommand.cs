// <copyright file="UploadtUserAvatarCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.User.Shared.Commands;

/// <summary>
/// 上传用户头像.
/// </summary>
public class UploadtUserAvatarCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 文件id.
    /// </summary>
    public int FileId { get; init; }
}
