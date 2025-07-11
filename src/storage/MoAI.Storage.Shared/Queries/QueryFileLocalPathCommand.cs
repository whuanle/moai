// <copyright file="QueryFileLocalPathCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Store.Enums;

namespace MoAI.Storage.Queries;

public class QueryFileLocalPathCommand : IRequest<QueryFileLocalPathCommandResponse>
{
    /// <summary>
    /// 文件可见性
    /// </summary>
    public FileVisibility Visibility { get; init; }

    /// <summary>
    /// key.
    /// </summary>
    public string ObjectKey { get; init; } = default!;
}

public class QueryFileLocalPathCommandResponse
{
    public string FilePath { get; init; }
}
