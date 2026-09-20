using FluentValidation;
using MediatR;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Queries;

/// <summary>
/// 查询流程设计器可选的 Agent 应用列表（本团队、已发布、非外部），并标记引入后是否与当前流程构成循环嵌套.
/// </summary>
public class QueryAppAgentOptionsCommand : IRequest<QueryAppAgentOptionsCommandResponse>, IUserIdContext, IModelValidator<QueryAppAgentOptionsCommand>
{
    /// <summary>
    /// 当前流程应用 id（循环嵌套判定基准）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppAgentOptionsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不正确.");
    }
}
