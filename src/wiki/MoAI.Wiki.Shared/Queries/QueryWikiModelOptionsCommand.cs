using FluentValidation;
using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询团队可用的向量化/对话模型选项（公开模型 + 已授权模型），仅团队成员可访问.
/// </summary>
public class QueryWikiModelOptionsCommand : IRequest<QueryWikiModelOptionsCommandResponse>, IModelValidator<QueryWikiModelOptionsCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从查询参数回填.
    /// </summary>
    public int TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiModelOptionsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
