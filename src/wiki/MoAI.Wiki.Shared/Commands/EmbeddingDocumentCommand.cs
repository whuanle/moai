using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 触发知识库文档向量化（复用已提取内容 + 已切割切片 → 可选元数据生成 → 向量化），仅团队成员可操作.
/// 需先执行 <see cref="ExtractDocumentContentCommand"/> 提取内容、<see cref="PartitionDocumentCommand"/>（或 AI 切割）生成切片。
/// 元数据采用已保存结果按本次触发选择是否参与向量化，不再在该命令中传入元数据模型。
/// </summary>
public class EmbeddingDocumentCommand : IRequest<EmbeddingDocumentCommandResponse>, IModelValidator<EmbeddingDocumentCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id，由 Controller 从路由参数回填.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 是否对原文切片内容向量化.
    /// </summary>
    public bool IsEmbedSourceText { get; init; } = true;

    /// <summary>
    /// 是否对生成的元数据（大纲/问题/关键词/摘要）向量化.
    /// </summary>
    public bool IsEmbedMetadata { get; init; } = true;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<EmbeddingDocumentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处校验请求体字段.
        validate.RuleFor(x => x)
            .Must(x => x.IsEmbedSourceText || x.IsEmbedMetadata)
            .WithMessage("至少需要选择一种向量化内容（原文或元数据）。");
    }
}
