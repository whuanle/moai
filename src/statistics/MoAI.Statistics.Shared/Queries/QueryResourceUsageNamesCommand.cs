using FluentValidation;
using MediatR;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Queries;

/// <summary>
/// 查询已有统计的资源类型名清单（去重排序），仅管理员.
/// </summary>
public class QueryResourceUsageNamesCommand : IRequest<QueryResourceUsageNamesCommandResponse>, IModelValidator<QueryResourceUsageNamesCommand>
{
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryResourceUsageNamesCommand> validate)
    {
    }
}
