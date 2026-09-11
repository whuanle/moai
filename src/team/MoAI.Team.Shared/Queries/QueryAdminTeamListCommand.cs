using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 分页查询系统内全部团队（仅管理员），支持按名称/简介模糊搜索与禁用状态筛选.
/// </summary>
public class QueryAdminTeamListCommand : PagedParamter, IRequest<QueryAdminTeamListCommandResponse>, IModelValidator<QueryAdminTeamListCommand>
{
    /// <summary>
    /// 搜索关键字，模糊匹配团队名称/简介，可为空.
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// 禁用状态筛选：null=全部 true=仅已禁用 false=仅正常，可为空.
    /// </summary>
    public bool? IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAdminTeamListCommand> validate)
    {
        validate.RuleFor(x => x.SearchText).MaximumLength(50).WithMessage("搜索关键字最长 50 个字符.");
    }
}
