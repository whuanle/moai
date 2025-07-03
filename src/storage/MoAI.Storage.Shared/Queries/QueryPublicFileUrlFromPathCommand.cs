// <copyright file="QueryPublicFileUrlFromPathCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Store.Queries.Response;

namespace MoAI.Store.Queries;

/// <summary>
/// 根据文件路径获取文件的访问路径，只支持公有文件.
/// </summary>
public class QueryPublicFileUrlFromPathCommand : IRequest<QueryPublicFileUrlFromPathResponse>
{
    /// <summary>
    /// 对象 key.
    /// </summary>
    public IReadOnlyCollection<string> ObjectKeys { get; init; } = new List<string>();
}
