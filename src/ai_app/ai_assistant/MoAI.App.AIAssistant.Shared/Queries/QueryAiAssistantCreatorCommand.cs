using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries;

public class QueryAiAssistantCreatorCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }
}
