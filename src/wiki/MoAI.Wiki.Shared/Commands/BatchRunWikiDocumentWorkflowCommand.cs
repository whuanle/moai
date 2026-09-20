using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MediatR;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 批量执行知识库文档工作流：多选文档后按勾选步骤（切割 / 生成元数据 / 向量化）一次性完成，也可只执行其中一步，仅团队成员可操作.
/// 切割（内容缺失时自动提取）在请求内同步逐文档执行并逐文档隔离失败；
/// 元数据生成与向量化复用向量化后台任务（WorkerTask + MQ）异步执行，每个文档一个任务.
/// </summary>
public class BatchRunWikiDocumentWorkflowCommand : IRequest<BatchRunWikiDocumentWorkflowCommandResponse>, IModelValidator<BatchRunWikiDocumentWorkflowCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 目标文档 id 集合（去重后最多 50 个）.
    /// </summary>
    public IReadOnlyCollection<long> DocumentIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 是否执行文档切割（内容缺失时自动提取）；重新切割会替换已有切片并清空旧元数据与向量.
    /// </summary>
    public bool IsPartition { get; init; }

    /// <summary>
    /// 是否使用 AI 智能切割（对话模型按语义输出 JSON 字符串数组，异步任务执行）；为否时按普通切割参数同步切割.
    /// </summary>
    public bool IsAiPartition { get; init; }

    /// <summary>
    /// AI 智能切割使用的对话模型 id，AI 切割时必填.
    /// </summary>
    public Guid AiModelId { get; init; }

    /// <summary>
    /// AI 切割提示词模板，为空使用内置默认；仅 AI 切割.
    /// </summary>
    public string? PromptTemplate { get; init; }

    /// <summary>
    /// 切割模式为普通切割时的切割方式.
    /// </summary>
    public DocumentPartitionSplitMode SplitMode { get; init; } = DocumentPartitionSplitMode.Markdown;

    /// <summary>
    /// 切片大小（1-8192，单位由 <see cref="SizeUnit"/> 决定），仅普通切割.
    /// </summary>
    public int ChunkSize { get; init; }

    /// <summary>
    /// 切片重叠大小（0-8192，单位由 <see cref="OverlapUnit"/> 决定），仅普通切割.
    /// </summary>
    public int ChunkOverlap { get; init; }

    /// <summary>
    /// 重叠单位.
    /// </summary>
    public DocumentPartitionOverlapUnit OverlapUnit { get; init; } = DocumentPartitionOverlapUnit.Character;

    /// <summary>
    /// 切片大小计量单位.
    /// </summary>
    public DocumentPartitionSizeUnit SizeUnit { get; init; } = DocumentPartitionSizeUnit.Character;

    /// <summary>
    /// Token 计量时使用的编码名或模型名，为空默认 cl100k_base，仅普通切割.
    /// </summary>
    public string? TokenEncodingOrModel { get; init; }

    /// <summary>
    /// 是否执行元数据生成（对文档全部切片替换式生成，异步任务执行）.
    /// </summary>
    public bool IsGenerateMetadata { get; init; }

    /// <summary>
    /// 元数据生成使用的对话模型 id，勾选元数据生成时必填.
    /// </summary>
    public Guid MetadataModelId { get; init; }

    /// <summary>
    /// 元数据生成策略（可多选）；为空时生成全套元数据.
    /// </summary>
    public List<MetadataGenerationStrategy>? StrategyTypes { get; init; }

    /// <summary>
    /// 是否执行向量化（异步任务执行）.
    /// </summary>
    public bool IsEmbedding { get; init; }

    /// <summary>
    /// 是否对原文切片内容向量化.
    /// </summary>
    public bool EmbedSourceText { get; init; } = true;

    /// <summary>
    /// 是否对生成的元数据向量化.
    /// </summary>
    public bool EmbedMetadata { get; init; } = true;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<BatchRunWikiDocumentWorkflowCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.DocumentIds)
            .NotEmpty().WithMessage("请选择要处理的文档.")
            .Must(x => x.All(id => id > 0)).WithMessage("文档 id 不正确.")
            .Must(x => x.Count <= 50).WithMessage("单次最多处理 50 个文档.");
        validate.RuleFor(x => x)
            .Must(x => x.IsPartition || x.IsGenerateMetadata || x.IsEmbedding)
            .WithMessage("至少需要选择一个执行步骤（切割 / 生成元数据 / 向量化）。");
        validate.RuleFor(x => x)
            .Must(x => !x.IsAiPartition || x.IsPartition)
            .WithMessage("使用 AI 切割时必须勾选切割步骤。");
        validate.RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("请选择用于智能切割的对话模型.")
            .When(x => x.IsAiPartition);
        validate.RuleFor(x => x.ChunkSize)
            .GreaterThan(0).WithMessage("切片大小必须大于 0.")
            .LessThanOrEqualTo(8192).WithMessage("切片大小不能超过 8192.")
            .When(x => x.IsPartition && !x.IsAiPartition);
        validate.RuleFor(x => x.ChunkOverlap)
            .GreaterThanOrEqualTo(0).WithMessage("切片重叠不能小于 0.")
            .LessThanOrEqualTo(8192).WithMessage("切片重叠不能超过 8192.")
            .When(x => x.IsPartition && !x.IsAiPartition);
        validate.RuleFor(x => x.ChunkOverlap)
            .LessThan(x => x.ChunkSize).WithMessage("按字符重叠时，切片重叠必须小于切片大小.")
            .When(x => x.IsPartition && !x.IsAiPartition && x.OverlapUnit == DocumentPartitionOverlapUnit.Character);
        validate.RuleFor(x => x.TokenEncodingOrModel)
            .MaximumLength(64).WithMessage("Token 编码或模型名不能超过 64 个字符.")
            .When(x => x.IsPartition && !x.IsAiPartition);
        validate.RuleFor(x => x.MetadataModelId)
            .NotEmpty().WithMessage("请选择元数据生成模型.")
            .When(x => x.IsGenerateMetadata);
        validate.RuleFor(x => x)
            .Must(x => !x.IsEmbedding || x.EmbedSourceText || x.EmbedMetadata)
            .WithMessage("至少需要选择一种向量化内容（原文或元数据）。");
    }
}
