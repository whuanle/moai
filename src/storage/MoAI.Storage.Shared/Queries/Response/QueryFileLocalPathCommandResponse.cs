using MediatR;

namespace MoAI.Storage.Queries.Response;

public class QueryFileLocalPathCommandResponse
{
    public required string FilePath { get; init; }
}
