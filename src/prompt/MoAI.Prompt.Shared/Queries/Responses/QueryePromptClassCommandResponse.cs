namespace MoAI.Prompt.Queries.Responses;

public class QueryePromptClassCommandResponse
{
    public IReadOnlyCollection<PromptClassifyItem> Items { get; init; } = new List<PromptClassifyItem>();
}
