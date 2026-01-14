using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 搜索知识库文本.
/// </summary>
public class SearchWikiDocumentTextCommandHandler : IRequestHandler<SearchWikiDocumentTextCommand, SearchWikiDocumentTextCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IAiClientBuilder _aiClientBuilder;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchWikiDocumentTextCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="mediator"></param>
    public SearchWikiDocumentTextCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, SystemOptions systemOptions, IAiClientBuilder aiClientBuilder, IMediator mediator)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _aiClientBuilder = aiClientBuilder;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<SearchWikiDocumentTextCommandResponse> Handle(SearchWikiDocumentTextCommand request, CancellationToken cancellationToken)
    {
        var (wikiConfig, wikiAiEndpoint) = await GetWikiConfigAsync(request.WikiId);

        if (wikiConfig == null || wikiAiEndpoint == null)
        {
            throw new BusinessException("知识库配置错误") { StatusCode = 409 };
        }

        AiEndpoint? chatAiEndpoint = null;

        if (request.AiModelId != 0)
        {
            chatAiEndpoint = await GetAiModelAsync(request);
        }

        // 1，优化提问
        var query = request.Query;
        var answer = string.Empty;

        if (request.IsOptimizeQuery)
        {
            var optimizeCommand = new OneSimpleChatCommand
            {
                Endpoint = chatAiEndpoint!,
                Question = request.Query,
                Prompt = "请将用户的问题优化为适合在知识库中搜索的简洁关键词，去除多余的描述和礼貌用语，确保关键词准确反映用户的查询意图。",
                AiModelId = request.AiModelId,
                ContextUserId = request.ContextUserId,
                Channel = "wiki_search_optimize"
            };

            var optimizeResponse = await _mediator.Send(optimizeCommand, cancellationToken);
            query = optimizeResponse.Content;
        }

        var wikiIndex = request.WikiId.ToString();

        var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(wikiAiEndpoint, wikiConfig.EmbeddingDimensions);
        var memoryDb = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

        await CheckIndexIsExistAsync(memoryDb, wikiIndex, wikiConfig.EmbeddingDimensions);

        MemoryFilter filter = new MemoryFilter();
        if (request.DocumentId != null)
        {
            // 从文档里面筛选
            var documentIndex = request.DocumentId.ToString();
            filter.Add("document_id", documentIndex);
        }

        // 为避免 CS0121 二义性，显式传递 CancellationToken：
        var results = await memoryDb
            .GetSimilarListAsync(index: wikiIndex, query, new[] { filter }, request.MinRelevance, request.Limit, true, cancellationToken)
            .ToArrayAsync(cancellationToken);

        if (results.Length == 0)
        {
            return new SearchWikiDocumentTextCommandResponse
            {
                Query = query
            };
        }

        // 获取关联的文本片段 id
        var allEmbddingIds = results.Select(x => x.Item1.Id)
            .Distinct()
            .Select(x => Guid.Parse(x!)).ToArray();

        // 只是元数据的，不包括源内容块
        var allEmbeddings = await _databaseContext.WikiDocumentChunkEmbeddings.AsNoTracking()
            .Where(x => allEmbddingIds.Contains(x.Id) && x.ChunkId != default)
            .ToListAsync();

        var chunkIds = allEmbeddings.Where(x => x.ChunkId != default).Select(x => x.ChunkId).Distinct().ToArray();
        var sourceChunkEmbeddings = await _databaseContext.WikiDocumentChunkEmbeddings.AsNoTracking()
            .Where(x => x.ChunkId == default && (chunkIds.Contains(x.Id) || allEmbddingIds.Contains(x.Id)))
            .ToArrayAsync();

        if (sourceChunkEmbeddings.Length > 0)
        {
            allEmbeddings.AddRange(sourceChunkEmbeddings);
        }

        var documentIds = allEmbeddings.Select(x => x.DocumentId).ToArray();
        var documents = await _databaseContext.WikiDocuments.Where(x => documentIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.FileType
            }).ToArrayAsync();

        List<SearchWikiDocumentTextItem> searches = new();

        foreach (var item in results)
        {
            var embedding = allEmbeddings.FirstOrDefault(x => x.Id == Guid.Parse(item.Item1.Id));
            if (embedding == null)
            {
                continue;
            }

            WikiDocumentChunkEmbeddingEntity? chunkEmbedding = null!;
            if (embedding.ChunkId != default)
            {
                chunkEmbedding = allEmbeddings.FirstOrDefault(x => x.Id == embedding.ChunkId);
            }

            if (chunkEmbedding == null)
            {
                chunkEmbedding = embedding;
            }

            var document = documents.FirstOrDefault(x => x.Id == embedding.DocumentId);

            var record = new SearchWikiDocumentTextItem
            {
                ChunkId = embedding.Id,
                SourceChunkId = chunkEmbedding.Id,
                Text = embedding.MetadataContent,
                ChunkText = chunkEmbedding.MetadataContent,
                RecordRelevance = item.Item2,
                DocumentId = document?.Id ?? 0,
                FileName = document?.FileName ?? string.Empty,
                FileType = document?.FileType ?? string.Empty
            };

            searches.Add(record);
        }

        if (request.IsAnswer)
        {
            var optimizeCommand = new OneSimpleChatCommand
            {
                Endpoint = chatAiEndpoint!,
                Question = request.Query,
                Prompt = $"""
仅根据下述事实，给出全面/详细的回答。
您不知道知识的来源，只需回答。
如果没有足够的信息，请回复“未找到信息”。
问题：{request.Query}
======
事实：
{string.Join("\n", searches.Select(x => x.ChunkText).Distinct())}
""",
                AiModelId = request.AiModelId,
                ContextUserId = request.ContextUserId,
                Channel = "wiki_search_answer"
            };

            var optimizeResponse = await _mediator.Send(optimizeCommand, cancellationToken);
            answer = optimizeResponse.Content;
        }

        return new SearchWikiDocumentTextCommandResponse
        {
            Query = query,
            Answer = answer,
            SearchResult = searches
        };
    }

    private async Task<AiEndpoint> GetAiModelAsync(SearchWikiDocumentTextCommand request)
    {
        var aiModel = await _databaseContext.AiModels
            .Where(x => x.Id == request.AiModelId && x.IsPublic)
            .FirstOrDefaultAsync();

        if (aiModel == null)
        {
            throw new BusinessException("未找到可用 ai 模型");
        }

        var aiEndpoint = new AiEndpoint
        {
            Name = aiModel.Name,
            DeploymentName = aiModel.DeploymentName,
            Title = aiModel.Title,
            AiModelType = Enum.Parse<AiModelType>(aiModel.AiModelType, true),
            Provider = Enum.Parse<AiProvider>(aiModel.AiProvider, true),
            ContextWindowTokens = aiModel.ContextWindowTokens,
            Endpoint = aiModel.Endpoint,
            Key = aiModel.Key,
            Abilities = new ModelAbilities
            {
                Files = aiModel.Files,
                FunctionCall = aiModel.FunctionCall,
                ImageOutput = aiModel.ImageOutput,
                Vision = aiModel.IsVision,
            },
            MaxDimension = aiModel.MaxDimension,
            TextOutput = aiModel.TextOutput
        };

        return aiEndpoint;
    }

    private static async Task CheckIndexIsExistAsync(Microsoft.KernelMemory.MemoryStorage.IMemoryDb memoryDb, string index, int vectorSize)
    {
        var indexs = await memoryDb.GetIndexesAsync();

        if (!indexs.Contains(index))
        {
            await memoryDb.CreateIndexAsync(index, vectorSize);
        }
    }

    private async Task<(EmbeddingConfig? WikiConfig, AiEndpoint? AiEndpoint)> GetWikiConfigAsync(int wikiId)
    {
        var result = await _databaseContext.Wikis
        .Where(x => x.Id == wikiId)
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
        {
            WikiConfig = new EmbeddingConfig
            {
                EmbeddingDimensions = a.EmbeddingDimensions,
                EmbeddingModelId = a.EmbeddingModelId,
            },
            AiEndpoint = new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
                Provider = x.AiProvider.JsonToObject<AiProvider>(),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput,
                Key = x.Key,
            }
        }).FirstOrDefaultAsync();

        return (result?.WikiConfig, result?.AiEndpoint);
    }
}