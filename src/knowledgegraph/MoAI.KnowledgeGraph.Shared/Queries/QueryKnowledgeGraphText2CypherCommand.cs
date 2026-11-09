using FluentValidation;
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// Text2Cypher 画布问图（B7/D31）：自然语言 → 对话模型按图谱结构生成只读 Cypher → 守卫与 $kgId 隔离执行 →
/// 返回生成的 Cypher、表格结果与结果中抽取的本图节点 id 集合（供画布高亮）；Member 可用（只读面）.
/// </summary>
public class QueryKnowledgeGraphText2CypherCommand : IRequest<QueryKnowledgeGraphText2CypherCommandResponse>, IModelValidator<QueryKnowledgeGraphText2CypherCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 自然语言问题.
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// 对话模型 id（可空：缺省回退图谱默认抽取模型，两者皆空 400，D30 同款回退）.
    /// </summary>
    public Guid? AiModelId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryKnowledgeGraphText2CypherCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Question)
            .NotEmpty().WithMessage("请输入问题.")
            .MaximumLength(500).WithMessage("问题长度不能超过 500.");
    }
}
