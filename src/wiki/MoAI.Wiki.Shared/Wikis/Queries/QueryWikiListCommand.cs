// <copyright file="QueryWikiListCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询当前用户可以看到的所有知识库.
/// </summary>
public class QueryWikiListCommand : IRequest<IReadOnlyCollection<QueryWikiSimpleInfoResponse>>
{
}