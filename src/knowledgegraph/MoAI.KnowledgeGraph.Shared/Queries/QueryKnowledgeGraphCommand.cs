using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询知识图谱详情.
/// </summary>
public class QueryKnowledgeGraphCommand : IRequest<QueryKnowledgeGraphCommandResponse>, IModelValidator<QueryKnowledgeGraphCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphCommand> validate)
    {
        validate.RuleFor(x => x.KnowledgeGraphId).GreaterThan(0).WithMessage("图谱 id 不正确.");
    }
}
