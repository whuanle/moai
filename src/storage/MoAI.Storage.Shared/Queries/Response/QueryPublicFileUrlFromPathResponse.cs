// <copyright file="QueryPublicFileUrlFromPathCommand.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

using MediatR;

namespace MoAI.Store.Queries.Response;

/// <summary>
/// 获取文件的访问路径，只支持公有文件.
/// </summary>
public class QueryPublicFileUrlFromPathResponse
{
    public IReadOnlyDictionary<string, Uri> Urls { get; init; } = new Dictionary<string, Uri>();
}