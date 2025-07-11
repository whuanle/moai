// <copyright file="UnbindUserAccountCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.User.Queries;

namespace MoAI.User.Handlers;

/// <summary>
/// <inheritdoc cref="UnbindUserAccountCommand"/>
/// </summary>
public class UnbindUserAccountCommandHandler : IRequestHandler<UnbindUserAccountCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnbindUserAccountCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UnbindUserAccountCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UnbindUserAccountCommand request, CancellationToken cancellationToken)
    {
        var bindEntity = await _databaseContext.UserOauths.FirstOrDefaultAsync(x => x.UserId == request.UserId & x.Id == request.BindId);

        if (bindEntity == null)
        {
            throw new BusinessException("未绑定第三方账号");
        }

        _databaseContext.UserOauths.Remove(bindEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
