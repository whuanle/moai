// <copyright file="QueryPublicFilePathCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Storage.Queries.Response;

namespace MoAI.Storage.Queries;

/// <summary>
/// 获取文件的访问路径，只支持公有文件.
/// </summary>
public class QueryPublicFilePathCommand : IRequest<QueryPublicFilePathResponse>
{
    /// <summary>
    /// 文件 id.
    /// </summary>
    public int? FileId { get; init; }

    /// <summary>
    /// md5 值.
    /// </summary>
    public string? MD5 { get; init; }

    /// <summary>
    ///  object key.
    /// </summary>
    public string? Key { get; init; }
}
