using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 为知识库文档切片生成元数据，仅团队成员可操作.
/// </summary>
public class GenerateDocumentChunkMetadataCommand : IRequest<SimpleInt>, IUserIdContext, IModelValidator<GenerateDocumentChunkMetadataCommand>
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
    /// 元数据生成使用的对话模型 id.
    /// </summary>
    public Guid MetadataModelId { get; init; }

    /// <summary>
    /// 目标切片 id；为空时表示当前文档全部切片.
    /// </summary>
    public List<long> ChunkIds { get; init; } = new();

    /// <summary>
    /// 是否保留切片已有元数据并追加本次生成结果.
    /// </summary>
    public bool AppendExisting { get; init; }

    /// <summary>
    /// 元数据生成策略；为空时兼容旧行为，生成大纲、问题、关键词和摘要.
    /// </summary>
    public MetadataGenerationStrategy? StrategyType { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<GenerateDocumentChunkMetadataCommand> validate)
    {
        validate.RuleFor(x => x.MetadataModelId)
            .NotEmpty().WithMessage("请选择元数据生成模型.");
        validate.RuleForEach(x => x.ChunkIds)
            .GreaterThan(0).WithMessage("切片 id 无效.");
    }
}