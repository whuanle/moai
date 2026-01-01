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
/// <inheritdoc cref="QueryAvatarUrlCommand"/>
/// </summary>
public class QueryAvatarUrlCommandHandler : IRequestHandler<QueryAvatarUrlCommand, IReadOnlyCollection<IAvatarPath>>
{
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAvatarUrlCommandHandler"/> class.
    /// </summary>
    /// <param name="storage"></param>
    public QueryAvatarUrlCommandHandler(IStorage storage)
    {
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IAvatarPath>> Handle(QueryAvatarUrlCommand request, CancellationToken cancellationToken)
    {
        var results = await _storage.GetFilesUrlAsync(request.Items.Where(x => !string.IsNullOrEmpty(x.AvatarKey)).Select(x => new KeyValueString { Key = x.AvatarKey, Value = x.AvatarKey }).ToArray(), request.ExpiryDuration);

        request.Items.ForEach(kv =>
        {
            var url = results.FirstOrDefault(x => x.Key == kv.AvatarKey).Value;
            if (url != null)
            {
                kv.Avatar = url.ToString();
            }
        });

        return request.Items;
    }
}