using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 配置知识图谱的向量化模型与维度（管理员）；配置后图谱参与向量检索.
/// </summary>
public class UpdateKnowledgeGraphEmbeddingConfigCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphEmbeddingConfigCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 向量化模型 id（需已启用且公开或已授权给该团队）.
    /// </summary>
    public Guid EmbeddingModelId { get; init; }

    /// <summary>
    /// 向量维度（1-2000）.
    /// </summary>
    public int EmbeddingDimensions { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphEmbeddingConfigCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.EmbeddingModelId).NotEmpty().WithMessage("向量化模型不能为空.");
        validate.RuleFor(x => x.EmbeddingDimensions).InclusiveBetween(1, 2000).WithMessage("向量维度取值 1-2000.");
    }
}
