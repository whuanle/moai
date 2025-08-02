// <copyright file="QueryFileLocalPathCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra;
using MoAI.Storage.Queries;
using MoAI.Store.Enums;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// <inheritdoc cref="QueryFileLocalPathCommand"/>
/// </summary>
public class QueryFileLocalPathCommandHandler : IRequestHandler<QueryFileLocalPathCommand, QueryFileLocalPathCommandResponse>
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFileLocalPathCommandHandler"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public QueryFileLocalPathCommandHandler(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<QueryFileLocalPathCommandResponse> Handle(QueryFileLocalPathCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var visibility = request.Visibility.ToString().ToLower();
        var filePath = Path.Combine(_systemOptions.FilePath, visibility, request.ObjectKey);

        return new QueryFileLocalPathCommandResponse
        {
            FilePath = filePath
        };
    }
}
