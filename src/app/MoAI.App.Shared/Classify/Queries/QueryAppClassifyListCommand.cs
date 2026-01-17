using MediatR;
using MoAI.App.Classify.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Classify.Queries;

/// <summary>
/// 查询应用分类列表.
/// </summary>
public class QueryAppClassifyListCommand : IUserIdContext, IRequest<QueryAppClassifyListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
