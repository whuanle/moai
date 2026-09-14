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
    public long KnowledgeGraphId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphSchemaCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
    }
}
