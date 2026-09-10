using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询图谱 schema（实体类型 + 关系类型）.
/// </summary>
public class QueryKnowledgeGraphSchemaCommand : IRequest<QueryKnowledgeGraphSchemaCommandResponse>, IModelValidator<QueryKnowledgeGraphSchemaCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphSchemaCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
    }
}
