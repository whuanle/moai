using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Queries;

/// <summary>
/// 分页查询资源使用统计（总量），仅管理员.
/// </summary>
public class QueryResourceUsageTotalCommand : PagedParamter, IRequest<QueryResourceUsageTotalCommandResponse>, IModelValidator<QueryResourceUsageTotalCommand>
{
    /// <summary>
    /// 资源类型名，为空则不过滤（见 <see cref="MoAI.Statistics.Services.ResourceUsageNames"/>）.
    /// </summary>
    public string? Name { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryResourceUsageTotalCommand> validate)
    {
    }
}
