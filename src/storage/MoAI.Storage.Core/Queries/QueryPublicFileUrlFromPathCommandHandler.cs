// <copyright file="QueryPublicFileUrlFromPathCommandHandler.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Storage.Queries.Response;
using MoAI.Store.Enums;
using MoAI.Store.Queries.Response;
using MoAI.Store.Services;

namespace MoAI.Store.Queries;

/// <summary>
/// 通过 path/objectkey 查询公有文件的访问路径.
/// </summary>
public class QueryPublicFileUrlFromPathCommandHandler : IRequestHandler<QueryPublicFileUrlFromPathCommand, QueryPublicFileUrlFromPathResponse>
{
    private readonly SystemOptions _systemOptions;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicFileUrlFromPathCommandHandler"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    /// <param name="serviceProvider"></param>
    public QueryPublicFileUrlFromPathCommandHandler(SystemOptions systemOptions, IServiceProvider serviceProvider)
    {
        _systemOptions = systemOptions;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryPublicFileUrlFromPathResponse> Handle(QueryPublicFileUrlFromPathCommand request, CancellationToken cancellationToken)
    {
        var fileStorage = _serviceProvider.GetRequiredService<IPublicFileStorage>();
        var urls = await fileStorage.GetFileUrlAsync(request.ObjectKeys);
        return new QueryPublicFileUrlFromPathResponse
        {
            Urls = urls
        };
    }
}
