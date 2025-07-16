// <copyright file="InviteWikiUserCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// <inheritdoc cref="InviteWikiUserCommand"/>
/// </summary>
public class InviteWikiUserCommandHandler : IRequestHandler<InviteWikiUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteWikiUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public InviteWikiUserCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(InviteWikiUserCommand request, CancellationToken cancellationToken)
    {
        // 查找用户
        var userEntity = await _databaseContext.Users.FirstOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken);
        if (userEntity == null)
        {
            throw new BusinessException("请检查用户名是否正确.") { StatusCode = 409 };
        }

        var isCreator = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId && x.CreateUserId == userEntity.Id).AnyAsync();
        if (isCreator)
        {
            throw new BusinessException("已是知识库管理员.");
        }

        var wikiUser = new WikiUserEntity
        {
            WikiId = request.WikiId,
            UserId = userEntity.Id,
        };

        _databaseContext.WikiUsers.Add(wikiUser);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
