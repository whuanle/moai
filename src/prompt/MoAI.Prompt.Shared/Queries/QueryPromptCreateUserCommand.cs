using MediatR;

namespace MoAI.Prompt.Queries;

public class QueryPromptCreateUserCommand : IRequest<QueryPromptCreateUserCommandResponse>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }
}

public class QueryPromptCreateUserCommandResponse
{
    public int UserId { get; init; }
}
