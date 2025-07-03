// <copyright file="QueryFileDownloadUrlCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Storage.Queries.Response;
using MoAI.Store.Enums;
using MoAI.Store.Services;

namespace MoAI.Store.Queries;

public class QueryFileDownloadUrlCommandHandler : IRequestHandler<QueryFileDownloadUrlCommand, QueryFileDownloadUrlCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;

    public QueryFileDownloadUrlCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<QueryFileDownloadUrlCommandResponse> Handle(QueryFileDownloadUrlCommand request, CancellationToken cancellationToken)
    {
        if (request.Visibility == FileVisibility.Public)
        {
            var fileStorage = _serviceProvider.GetRequiredService<IPublicFileStorage>();
            var urls = await fileStorage.GetFileUrlAsync(request.ObjectKeys);
            return new QueryFileDownloadUrlCommandResponse
            {
                Urls = urls
            };
        }
        else
        {
            var fileStorage = _serviceProvider.GetRequiredService<IPublicFileStorage>();
            var urls = await fileStorage.GetFileUrlAsync(request.ObjectKeys);
            return new QueryFileDownloadUrlCommandResponse
            {
                Urls = urls
            };
        }
    }
}
