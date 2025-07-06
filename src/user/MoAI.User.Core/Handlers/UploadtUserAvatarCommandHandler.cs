// <copyright file="UploadtUserAvatarCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.User.Shared.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.User.Core.Handlers;

/// <summary>
/// <inheritdoc cref="UploadtUserAvatarCommand"/>
/// </summary>
public class UploadtUserAvatarCommandHandler : IRequestHandler<UploadtUserAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadtUserAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public UploadtUserAvatarCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UploadtUserAvatarCommand request, CancellationToken cancellationToken)
    {
        var file = await _databaseContext.Files.FirstOrDefaultAsync(x => x.Id == request.FileId);
        if (file == null)
        {
            throw new BusinessException("头像文件不存在") { StatusCode = 400 };
        }

        if (!file.IsUploaded)
        {
            throw new BusinessException("头像文件尚未上传完毕") { StatusCode = 400 };
        }

        var user = await _databaseContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在") { StatusCode = 404 };
        }

        user.AvatarPath = file.ObjectKey;

        _databaseContext.Update(user);
        await _databaseContext.SaveChangesAsync();

        return EmptyCommandResponse.Default;
    }
}