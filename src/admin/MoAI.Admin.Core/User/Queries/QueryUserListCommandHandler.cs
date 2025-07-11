// <copyright file="QueryUserListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Admin.User.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Storage.Queries;
using MoAI.Store.Queries;
using MoAI.User.Queries;

namespace MoAI.Admin.User.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserListCommand"/>
/// </summary>
public class QueryUserListCommandHandler : IRequestHandler<QueryUserListCommand, QueryUserListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryUserListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryUserListCommandResponse> Handle(QueryUserListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Users.AsQueryable();

        if (request.UserId is not null && request.UserId > 0)
        {
            query = query.Where(x => x.Id == request.UserId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x => x.UserName.Contains(request.Search) || x.NickName.Contains(request.Search) || x.Email.Contains(request.Search) || x.Phone.Contains(request.Search));
        }

        if (request.IsAdmin is true)
        {
            query = query.Where(x => x.IsAdmin);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(x => x.Id)
            .Skip(request.PageSize * (request.PageNo - 1))
            .Take(request.PageSize)
            .Select(x => new QueryUserListCommandResponseItem
            {
                IsDisable = x.IsDisable,
                AvatarPath = x.AvatarPath,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                Email = x.Email,
                Id = x.Id,
                IsAdmin = x.IsAdmin,
                NickName = x.NickName,
                Phone = x.Phone,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                UserName = x.UserName
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = users
        });

        var userAvatars = users.Where(x => !string.IsNullOrEmpty(x.AvatarPath)).Distinct().Select(x => new KeyValueString { Key = x.AvatarPath, Value = x.AvatarPath }).ToHashSet();
        var avatarResult = await _mediator.Send(new QueryFileDownloadUrlCommand
        {
            Visibility = Store.Enums.FileVisibility.Public,
            ObjectKeys = userAvatars
        });

        foreach (var item in users)
        {
            avatarResult.Urls.TryGetValue(item.AvatarPath, out var avatarUrl);
            item.AvatarPath = avatarUrl?.ToString() ?? string.Empty;
        }

        return new QueryUserListCommandResponse
        {
            Items = users,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
            Total = totalCount
        };
    }
}
