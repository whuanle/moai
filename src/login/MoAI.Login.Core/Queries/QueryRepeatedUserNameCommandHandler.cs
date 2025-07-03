// <copyright file="QueryRepeatedUserNameCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.Login.Queries;

public class QueryRepeatedUserNameCommandHandler : IRequestHandler<QueryRepeatedUserNameCommand, SimpleBool>
{
    private readonly DatabaseContext _dbContext;

    public QueryRepeatedUserNameCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SimpleBool> Handle(QueryRepeatedUserNameCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _dbContext.Users
        .Where(u => u.UserName == request.UserName || u.Email == request.UserName || u.Phone == request.UserName)
        .Select(u => new { u.UserName, u.Email, u.Phone })
        .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            return new SimpleBool() { Value = true };
        }

        return new SimpleBool() { Value = false };
    }
}
