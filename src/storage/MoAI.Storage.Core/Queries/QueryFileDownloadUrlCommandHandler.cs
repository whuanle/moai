using LinqKit;
using MediatR;
using MoAI.Infra;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Storage.Queries;
using MoAI.Storage.Queries.Response;
using MoAI.Storage.Services;
using System.Net;

namespace MoAI.Store.Queries;

/// <summary>
/// <inheritdoc cref="QueryFileDownloadUrlCommand"/>
/// </summary>
public class QueryFileDownloadUrlCommandHandler : IRequestHandler<QueryFileDownloadUrlCommand, QueryFileDownloadUrlCommandResponse>
{
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFileDownloadUrlCommandHandler"/> class.
    /// </summary>
    /// <param name="storage"></param>
    public QueryFileDownloadUrlCommandHandler(IStorage storage)
    {
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<QueryFileDownloadUrlCommandResponse> Handle(QueryFileDownloadUrlCommand request, CancellationToken cancellationToken)
    {
        var results = await _storage.GetFilesUrlAsync(request.ObjectKeys, request.ExpiryDuration);

        return new QueryFileDownloadUrlCommandResponse
        {
            Urls = results
        };
    }
}
