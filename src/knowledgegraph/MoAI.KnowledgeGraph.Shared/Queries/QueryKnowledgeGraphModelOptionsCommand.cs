using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询团队可用的 AI 对话模型选项（公开模型 + 已授权模型），仅团队成员可访问，用于 AI 导入文件.
/// </summary>
public class QueryKnowledgeGraphModelOptionsCommand : IRequest<QueryKnowledgeGraphModelOptionsCommandResponse>, IModelValidator<QueryKnowledgeGraphModelOptionsCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从查询参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphModelOptionsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
