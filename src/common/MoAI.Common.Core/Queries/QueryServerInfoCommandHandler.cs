using MediatR;
using MoAI.Common.Queries.Response;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Services;

namespace MoAI.Common.Queries;

/// <summary>
/// <inheritdoc cref="QueryServerInfoCommand"/>
/// </summary>
public class QueryServerInfoCommandHandler : IRequestHandler<QueryServerInfoCommand, QueryServerInfoCommandResponse>
{
    private readonly SystemOptions _systemOptions;
    private readonly IRsaProvider _rsaProvider;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryServerInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="databaseContext"></param>
    public QueryServerInfoCommandHandler(SystemOptions systemOptions, IRsaProvider rsaProvider, DatabaseContext databaseContext)
    {
        _systemOptions = systemOptions;
        _rsaProvider = rsaProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryServerInfoCommandResponse> Handle(QueryServerInfoCommand request, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(_systemOptions.Server), "statics");

        return new QueryServerInfoCommandResponse
        {
            PublicStoreUrl = endpoint.ToString(),
            ServiceUrl = _systemOptions.Server,
            RsaPublic = _rsaProvider.GetPublicKey(),
            MaxUploadFileSize = _systemOptions.MaxUploadFileSize,
            Name = _systemOptions.Name
        };
    }
}
