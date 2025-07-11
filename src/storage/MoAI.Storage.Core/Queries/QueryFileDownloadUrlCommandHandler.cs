// <copyright file="QueryFileDownloadUrlCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Storage.Queries.Response;
using MoAI.Store.Enums;
using System.Net;

namespace MoAI.Store.Queries;

/// <summary>
/// <inheritdoc cref="QueryFileDownloadUrlCommand"/>
/// </summary>
public class QueryFileDownloadUrlCommandHandler : IRequestHandler<QueryFileDownloadUrlCommand, QueryFileDownloadUrlCommandResponse>
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFileDownloadUrlCommandHandler"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public QueryFileDownloadUrlCommandHandler(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public QueryFileDownloadUrlCommandResponse Handle(QueryFileDownloadUrlCommand request, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, Uri>();

        if (request.Visibility == FileVisibility.Public)
        {
            foreach (var file in request.ObjectKeys)
            {
                var objectPath = WebUtility.UrlEncode(file.Key);
                results[file.Key] = new Uri(new Uri(_systemOptions.Server), relativeUri: $"/download/public/{file.Key}");
            }
        }
        else
        {
            foreach (var file in request.ObjectKeys)
            {
                var objectPath = WebUtility.UrlEncode(file.Key);

                var expiry = DateTimeOffset.Now.AddMinutes(5).ToUnixTimeMilliseconds();
                var token = HashHelper.ComputeSha256Hash($"{file.Value}|{file.Key}|{expiry}");
                results[file.Key] = new Uri(new Uri(_systemOptions.Server), relativeUri: $"/download/private/{objectPath}?key={file.Key}&expiry={expiry}&token={token}");
            }
        }

        return new QueryFileDownloadUrlCommandResponse
        {
            Urls = results
        };
    }

    Task<QueryFileDownloadUrlCommandResponse> IRequestHandler<QueryFileDownloadUrlCommand, QueryFileDownloadUrlCommandResponse>.Handle(QueryFileDownloadUrlCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
