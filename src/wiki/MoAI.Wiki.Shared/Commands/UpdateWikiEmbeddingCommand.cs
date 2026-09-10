using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 更新知识库向量化模型与维度配置，需要团队 Admin 及以上角色.
/// 维度上限 2000：pgvector 在 2000 以内才能建 hnsw 索引.
/// 一旦该 wiki 已有文档被向量化（IsLock），模型与维度不可再修改.
/// </summary>
public class UpdateWikiEmbeddingCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiEmbeddingCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 向量化模型 id.
    /// </summary>
    public Guid EmbeddingModelId { get; init; }

    /// <summary>
    /// 知识库向量维度（1-2000）.
    /// </summary>
    public int EmbeddingDimensions { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiEmbeddingCommand> validate)
    {
        validate.RuleFor(x => x.EmbeddingModelId).NotEmpty().WithMessage("请选择向量化模型.");
        validate.RuleFor(x => x.EmbeddingDimensions)
            .GreaterThan(0).WithMessage("向量维度必须大于 0.")
            .LessThanOrEqualTo(2000).WithMessage("向量维度不能超过 2000（pgvector 索引硬上限）.");
    }
}
