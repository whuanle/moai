// <copyright file="QueryUserIsAdminCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Database;
using MoAI.Login.Commands;
using MoAI.Public.Queries;
using MoAI.Public.Queries.Response;

namespace MoAI.Login.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserIsAdminCommand"/>
/// </summary>
public class QueryUserIsAdminCommandHandler : IRequestHandler<QueryUserIsAdminCommand, QueryUserIsAdminCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserIsAdminCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryUserIsAdminCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryUserIsAdminCommandResponse> Handle(QueryUserIsAdminCommand request, CancellationToken cancellationToken)
    {
        var adminList = await _mediator.Send(new QueryAdminIdsCommand(), cancellationToken);

        var isRoot = adminList.RootId == request.UserId;
        return new QueryUserIsAdminCommandResponse { IsRoot = isRoot, IsAdmin = isRoot ? true : adminList.AdminIds.Contains(request.UserId) };
    }
}
