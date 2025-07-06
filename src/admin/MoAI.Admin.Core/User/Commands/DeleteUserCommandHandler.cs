// <copyright file="DeleteUserCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Login.Commands;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// <inheritdoc cref="DeleteUserCommand"/>
/// </summary>
public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public DeleteUserCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.SoftDeleteAsync(_databaseContext.Users.Where(x => request.UserIds.Contains(x.Id)));

        foreach (var item in request.UserIds)
        {
            await _mediator.Send(new RefreshUserStateCommand
            {
                UserId = item,
            });
        }

        return EmptyCommandResponse.Default;
    }
}
