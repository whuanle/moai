using MediatR;
using MoAI.Infra.Models;
using MoAIPrompt.Queries.Responses;

namespace MoAIPrompt.Queries;

/// <summary>
/// 查询提示词列表.
/// </summary>
public class QueryPromptListCommand : PagedParamter, IUserIdContext, IRequest<QueryPromptListCommandResponse>, IDynamicOrderable
{
    /// <summary>
    /// 筛选分类.
    /// </summary>
    public int? ClassId { get; init; }

    /// <summary>
    /// 只查询自己创建的提示词.
    /// </summary>
    public bool? IsOwn { get; init; }

    /// <summary>
    /// 筛选名称.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// 排序，支持 [CreateTime,Name]，默认升序 ，value 为 true 则是降序.
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
