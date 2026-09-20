using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 更新知识库默认工作流配置（切割 / 元数据生成 / 向量化三步预设），需要团队 Admin 及以上角色.
/// 整体覆盖保存：某步骤传 null 表示清除该步骤预设.
/// 仅保存预设，不触发任何文档处理；批量执行见 <see cref="BatchRunWikiDocumentWorkflowCommand"/>.
/// </summary>
public class UpdateWikiWorkflowCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiWorkflowCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档切割预设，为空表示未配置该步骤.
    /// </summary>
    public WikiWorkflowPartitionOptions? Partition { get; init; }

    /// <summary>
    /// 元数据生成预设，为空表示未配置该步骤.
    /// </summary>
    public WikiWorkflowMetadataOptions? Metadata { get; init; }

    /// <summary>
    /// 向量化预设，为空表示未配置该步骤.
    /// </summary>
    public WikiWorkflowEmbeddingOptions? Embedding { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiWorkflowCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.Partition)
            .Must(x => x == null || x.Mode != WorkflowPartitionMode.Ai || x.AiModelId != Guid.Empty)
            .WithMessage("请选择用于智能切割的对话模型.");
        validate.RuleFor(x => x.Partition)
            .Must(x => x == null || x.Mode == WorkflowPartitionMode.Ai || (x.ChunkSize > 0 && x.ChunkSize <= 8192))
            .WithMessage("切片大小必须大于 0 且不超过 8192.");
        validate.RuleFor(x => x.Partition)
            .Must(x => x == null || x.Mode == WorkflowPartitionMode.Ai || (x.ChunkOverlap >= 0 && x.ChunkOverlap <= 8192))
            .WithMessage("切片重叠不能小于 0 且不超过 8192.");
        validate.RuleFor(x => x.Partition)
            .Must(x => x == null || x.Mode == WorkflowPartitionMode.Ai || x.OverlapUnit != DocumentPartitionOverlapUnit.Character || x.ChunkOverlap < x.ChunkSize)
            .WithMessage("按字符重叠时，切片重叠必须小于切片大小.");
        validate.RuleFor(x => x.Partition)
            .Must(x => x == null || string.IsNullOrEmpty(x.TokenEncodingOrModel) || x.TokenEncodingOrModel.Length <= 64)
            .WithMessage("Token 编码或模型名不能超过 64 个字符.");
        validate.RuleFor(x => x.Metadata)
            .Must(x => x == null || x.MetadataModelId != Guid.Empty)
            .WithMessage("请选择元数据生成模型.");
        validate.RuleFor(x => x.Embedding)
            .Must(x => x == null || x.EmbedSourceText || x.EmbedMetadata)
            .WithMessage("至少需要选择一种向量化内容（原文或元数据）。");
    }
}
