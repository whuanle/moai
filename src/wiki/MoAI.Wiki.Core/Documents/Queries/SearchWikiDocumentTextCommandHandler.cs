// <copyright file="SearchWikiDocumentTextCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchWikiDocumentTextCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    public SearchWikiDocumentTextCommandHandler(IServiceProvider serviceProvider, DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _serviceProvider = serviceProvider;
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<SearchWikiDocumentTextCommandResponse> Handle(SearchWikiDocumentTextCommand request, CancellationToken cancellationToken)
    {
        var (wikiConfig, aiEndpoint) = await GetWikiConfigAsync(request.WikiId);

        if (wikiConfig == null || aiEndpoint == null)
        {
            throw new BusinessException("知识库配置错误") { StatusCode = 409 };
        }

        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder().WithSimpleFileStorage(Path.GetTempPath());

        var textEmbeddingGeneration = _serviceProvider.GetKeyedService<ITextEmbeddingGeneration>(aiEndpoint.Provider);

        if (textEmbeddingGeneration == null)
        {
            throw new BusinessException("不支持该模型提供商") { StatusCode = 409 };
        }

        textEmbeddingGeneration.Configure(memoryBuilder, aiEndpoint, wikiConfig);

        var memoryClient = memoryBuilder.WithoutTextGenerator()
            .WithPostgresMemoryDb(new PostgresConfig
            {
                ConnectionString = _systemOptions.Wiki.Database,
            })
            .Build();

        MemoryFilter filter = new MemoryFilter();
        if (request.DocumentId != null)
        {
            var document = await _databaseContext.WikiDocuments.FirstOrDefaultAsync(x => x.Id == request.DocumentId);
            if (document == null)
            {
                throw new BusinessException("文档不存在") { StatusCode = 404 };
            }

            filter = new MemoryFilter
            {
                { "fileId", document.FileId.ToString() },
            };
        }

        var query = string.IsNullOrEmpty(request.Query) ? string.Empty : request.Query;
        var searchResult = await memoryClient.SearchAsync(query: query, index: request.WikiId.ToString(), limit: 10, filter: filter);

        if (searchResult == null)
        {
            return new SearchWikiDocumentTextCommandResponse
            {
                SearchResult = new SearchResult()
            };
        }

        return new SearchWikiDocumentTextCommandResponse
        {
            SearchResult = searchResult,
        };
    }

    private async Task<(WikiConfig? WikiConfig, AiEndpoint? AiEndpoint)> GetWikiConfigAsync(int wikiId)
    {
        var result = await _databaseContext.Wikis
        .Where(x => x.Id == wikiId)
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
        {
            WikiConfig = new WikiConfig
            {
                EmbeddingDimensions = a.EmbeddingDimensions,
                EmbeddingBatchSize = a.EmbeddingBatchSize,
                MaxRetries = a.MaxRetries,
                EmbeddingModelTokenizer = a.EmbeddingModelTokenizer.JsonToObject<EmbeddingTokenizer>(),
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