#pragma warning disable KMEXP00 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using LinqKit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.AI.ParagraphPreprocess;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Commands;
using MoAI.Wiki.Embedding.Models;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// <inheritdoc cref="WikiDocumentTextPartitionAiGenerationCommand"/>
/// </summary>
public class WikiDocumentTextPartitionAiGenerationCommandHandler : IRequestHandler<WikiDocumentTextPartitionAiGenerationCommand, WikiDocumentTextPartitionAiGenerationCommandResponse>
{
    private readonly IParagraphPreprocessBuilder _paragraphPreprocessAiBuilder;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiDocumentTextPartitionAiGenerationCommandHandler"/> class.
    /// </summary>
    /// <param name="paragraphPreprocessAiBuilder"></param>
    /// <param name="databaseContext"></param>
    public WikiDocumentTextPartitionAiGenerationCommandHandler(IParagraphPreprocessBuilder paragraphPreprocessAiBuilder, DatabaseContext databaseContext)
    {
        _paragraphPreprocessAiBuilder = paragraphPreprocessAiBuilder;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<WikiDocumentTextPartitionAiGenerationCommandResponse> Handle(WikiDocumentTextPartitionAiGenerationCommand request, CancellationToken cancellationToken)
    {
        List<KeyValue<long, ParagraphPreprocessResult>> list = new();
        var chunkOrders = request.Chunks.Select(x => x).ToArray();

        // 获取模型配置
        var chatAiModel = await _databaseContext.AiModels
            .Where(x => x.Id == request.AiModelId && x.IsPublic)
            .FirstOrDefaultAsync();

        if (chatAiModel == null)
        {
            throw new BusinessException("未找到可用 ai 模型");
        }

        var embeddingAiModel = await _databaseContext.Wikis
            .Where(x => x.Id == request.WikiId)
            .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, b) => b).FirstOrDefaultAsync();

        if (embeddingAiModel == null || !AiModelType.Embedding.ToString().Equals(embeddingAiModel.AiModelType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("知识库未配置向量化模型") { StatusCode = 409 };
        }

        var chatAiEndpoint = new AiEndpoint
        {
            Name = chatAiModel.Name,
            DeploymentName = chatAiModel.DeploymentName,
            Title = chatAiModel.Title,
            AiModelType = Enum.Parse<AiModelType>(chatAiModel.AiModelType, true),
            Provider = Enum.Parse<AiProvider>(chatAiModel.AiProvider, true),
            ContextWindowTokens = chatAiModel.ContextWindowTokens,
            Endpoint = chatAiModel.Endpoint,
            Key = chatAiModel.Key,
            Abilities = new ModelAbilities
            {
                Files = chatAiModel.Files,
                FunctionCall = chatAiModel.FunctionCall,
                ImageOutput = chatAiModel.ImageOutput,
                Vision = chatAiModel.IsVision,
            },
            MaxDimension = chatAiModel.MaxDimension,
            TextOutput = chatAiModel.TextOutput
        };

        var embeddingAiEndpoint = new AiEndpoint
        {
            Name = embeddingAiModel.Name,
            DeploymentName = embeddingAiModel.DeploymentName,
            Title = embeddingAiModel.Title,
            AiModelType = Enum.Parse<AiModelType>(embeddingAiModel.AiModelType, true),
            Provider = Enum.Parse<AiProvider>(embeddingAiModel.AiProvider, true),
            ContextWindowTokens = embeddingAiModel.ContextWindowTokens,
            Endpoint = embeddingAiModel.Endpoint,
            Key = embeddingAiModel.Key,
            Abilities = new ModelAbilities
            {
                Files = embeddingAiModel.Files,
                FunctionCall = embeddingAiModel.FunctionCall,
                ImageOutput = embeddingAiModel.ImageOutput,
                Vision = embeddingAiModel.IsVision,
            },
            MaxDimension = embeddingAiModel.MaxDimension,
            TextOutput = embeddingAiModel.TextOutput
        };

        // 智能处理文本块
        var documentPreprocessor = _paragraphPreprocessAiBuilder.GetDocumentPreprocessor(chatAiEndpoint, embeddingAiEndpoint);

        var preprocessResults = await documentPreprocessor.PreprocessBatchAsync(chunkOrders.Select(x => x.Value).ToArray(), request.PreprocessStrategyType);

        for (int i = 0; i < preprocessResults.Count; i++)
        {
            list.Add(new KeyValue<long, ParagraphPreprocessResult>(chunkOrders[i].Key, preprocessResults[i]));
        }

        return new WikiDocumentTextPartitionAiGenerationCommandResponse
        {
            Items = list
        };
    }
}