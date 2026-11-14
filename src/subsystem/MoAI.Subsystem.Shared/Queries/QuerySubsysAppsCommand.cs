using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 系统级接入列表查询（仅 root）：子系统与应用接入同表回显，按类型可选过滤.
/// </summary>
public class QuerySubsysAppsCommand : IRequest<QuerySubsysAppsCommandResponse>, IModelValidator<QuerySubsysAppsCommand>
{
    /// <summary>
    /// 按标识/名称模糊搜索，可选.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// 按接入类型过滤，可选；不传返回全部系统级接入.
    /// </summary>
    public AccessAppType? AppType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsysAppsCommand> validate)
    {
        validate.RuleFor(x => x.AppType).IsInEnum().When(x => x.AppType.HasValue).WithMessage("接入类型不合法.");
    }
}
