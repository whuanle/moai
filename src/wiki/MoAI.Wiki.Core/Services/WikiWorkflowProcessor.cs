using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Wiki.Consumers.Events;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库文档工作流处理器：按任务数据依次执行 AI 切割 → 元数据生成（可多策略）→ 向量化，各步骤可选.
/// 纯向量化与元数据生成的领域实现分别在 <see cref="WikiEmbeddingService"/>，AI 切割在 <see cref="WikiDocumentProcessingService"/>.
/// </summary>
[InjectOnScoped]
public class WikiWorkflowProcessor : IWikiWorkflowProcessor
{
    private readonly WikiDocumentProcessingService _processingService;
    private readonly WikiEmbeddingService _embeddingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiWorkflowProcessor"/> class.
    /// </summary>
    /// <param name="processingService">文档内容处理服务（AI 切割）.</param>
    /// <param name="embeddingService">文档向量化领域服务（元数据生成/向量化）.</param>
    public WikiWorkflowProcessor(WikiDocumentProcessingService processingService, WikiEmbeddingService embeddingService)
    {
        _processingService = processingService;
        _embeddingService = embeddingService;
    }

    /// <inheritdoc/>
    public async Task ProcessAsync(WikiDocumentEmbeddingTaskData data, CancellationToken cancellationToken = default)
    {
        if (!data.IsEmbedSourceText && !data.IsEmbedMetadata && data.MetadataModelId == Guid.Empty && data.AiPartitionModelId == Guid.Empty)
        {
            throw new ArgumentException("至少需要选择一个处理步骤（AI 切割 / 元数据生成 / 向量化）。", nameof(data));
        }

        if (data.AiPartitionModelId != Guid.Empty)
        {
            await _processingService.AiPartitionAsync(data.WikiId, data.DocumentId, data.AiPartitionModelId, data.AiPartitionPromptTemplate, cancellationToken);
        }

        if (data.MetadataModelId != Guid.Empty)
        {
            await _embeddingService.GenerateAndSaveChunkMetadataAsync(
                data.WikiId,
                data.DocumentId,
                data.MetadataModelId,
                new List<long>(),
                appendExisting: false,
                data.MetadataStrategyTypes,
                cancellationToken);
        }

        if (!data.IsEmbedSourceText && !data.IsEmbedMetadata)
        {
            return;
        }

        await _embeddingService.ProcessAsync(data.WikiId, data.DocumentId, data.IsEmbedSourceText, data.IsEmbedMetadata, cancellationToken);
    }
}
