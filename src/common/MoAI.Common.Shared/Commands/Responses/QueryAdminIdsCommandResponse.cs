namespace MoAI.Common.Commands.Responses;

public class QueryAdminIdsCommandResponse
{
    public int RootId { get; init; }

    public IReadOnlyCollection<int> AdminIds { get; init; } = new List<int>();
}