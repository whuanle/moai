using MediatR;
using MoAI.Storage.Queries;
using MoAI.Storage.Queries.Response;
using MoAI.Storage.Services;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// <inheritdoc cref="QueryFileLocalPathCommand"/>
/// </summary>
public class QueryFileLocalPathCommandHandler : IRequestHandler<QueryFileLocalPathCommand, QueryFileLocalPathCommandResponse>
{
    private readonly IStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFileLocalPathCommandHandler"/> class.
    /// </summary>
    /// <param name="storage"></param>
    public QueryFileLocalPathCommandHandler(IStorage storage)
    {
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<QueryFileLocalPathCommandResponse> Handle(QueryFileLocalPathCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var tmpDir = Path.GetTempPath();
        tmpDir = Path.Combine(tmpDir, DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
        if (!Directory.Exists(tmpDir))
        {
            Directory.CreateDirectory(tmpDir);
        }

        var filePath = Path.Combine(tmpDir, request.ObjectKey);

        var fileDir = Directory.GetParent(filePath)!.FullName;

        if (!Directory.Exists(fileDir))
        {
            Directory.CreateDirectory(fileDir);
        }

        await _storage.DownloadAsync(request.ObjectKey, filePath);

        return new QueryFileLocalPathCommandResponse
        {
            FilePath = filePath
        };
    }
}
