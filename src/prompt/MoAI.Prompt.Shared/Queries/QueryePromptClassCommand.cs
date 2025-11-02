using MediatR;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 查询提示词分类列表.
/// </summary>
public class QueryePromptClassCommand : IRequest<QueryePromptClassCommandResponse>
{
}