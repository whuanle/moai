// <copyright file="QueryUserInfoCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Models;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Public.Queries.Response;
using MoAI.Store.Queries;

namespace MoAI.Public.Queries;

/// <summary>
/// 处理查询用户信息的命令.
/// </summary>
public class QueryUserInfoCommandHandler : IRequestHandler<QueryUserInfoCommand, QueryUserInfoCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="mediator"></param>
    public QueryUserInfoCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryUserInfoCommandResponse> Handle(QueryUserInfoCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new BusinessException("未找到用户.") { StatusCode = 404 };
        }

        var avatarUrl = string.Empty;
        if (!string.IsNullOrEmpty(user.AvatarPath))
        {
            var fileUrls = await _mediator.Send(new QueryPublicFileUrlFromPathCommand { ObjectKeys = new List<string>() { user.AvatarPath } });
            avatarUrl = fileUrls.Urls.FirstOrDefault().Value?.ToString() ?? string.Empty;
        }
        else
        {
            avatarUrl = string.Empty;
        }

        var isRoot = await _databaseContext.Settings.AnyAsync(x => x.Key == SystemSettingKeys.Root && x.Value == user.Id.ToString());

        return new QueryUserInfoCommandResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            Avatar = avatarUrl,
            IsRoot = isRoot,
            IsAdmin = isRoot ? true : user.IsAdmin
        };
    }
}
