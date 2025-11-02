using MediatR;
using MoAI.Infra.Models;
using MoAIPrompt.Queries.Responses;

namespace MoAIPrompt.Queries;

/// <summary>
/// 查询能看到的提示词列表.
/// </summary>
public class QueryPromptListCommand : PagedParamter, IUserIdContext, IRequest<QueryPromptListCommandResponse>
{
    /// <summary>
    /// 筛选分类.
    /// </summary>
    public int? ClassId { get; init; }

    /// <summary>
    /// 筛选名称.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// 筛选条件.
    /// </summary>
    public PromptFilterCondition Condition { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }
}
