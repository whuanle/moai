// <copyright file="SearchWikiDocumentTextCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.Document.Core.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 搜索知识库文本.
/// </summary>
public class SearchWikiDocumentTextCommandHandler : IRequestHandler<SearchWikiDocumentTextCommand, SearchWikiDocumentTextCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly CustomKernelMemoryBuilder _customKernelMemoryBuilder;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchWikiDocumentTextCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="customKernelMemoryBuilder"></param>
    /// <param name="systemOptions"></param>
    public SearchWikiDocumentTextCommandHandler(DatabaseContext databaseContext, CustomKernelMemoryBuilder customKernelMemoryBuilder, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _customKernelMemoryBuilder = customKernelMemoryBuilder;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<SearchWikiDocumentTextCommandResponse> Handle(SearchWikiDocumentTextCommand request, CancellationToken cancellationToken)
    {
        var teamWikiAiConfig = await _databaseContext.Wikis
        .Where(x => x.Id == request.WikiId)
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
        {
            WikiConfig = new WikiConfig
            {
                EmbeddingDimensions = a.EmbeddingDimensions,
                EmbeddingBatchSize = a.EmbeddingBatchSize,
                MaxRetries = a.MaxRetries,
                EmbeddingModelTokenizer = a.EmbeddingModelTokenizer,
            },
            AiEndpoint = new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = Enum.Parse<AiModelType>(x.AiModelType, true),
                Provider = Enum.Parse<AiProvider>(x.AiProvider, true),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Key = x.Key,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput
            }
        }).FirstOrDefaultAsync();

        if (teamWikiAiConfig == null)
        {
            throw new BusinessException("团队配置错误") { StatusCode = 500 };
        }

        // 构建客户端
        var memoryBuilder = new KernelMemoryBuilder().WithSimpleFileStorage(Path.GetTempPath());

        _customKernelMemoryBuilder.ConfigEmbeddingModel(memoryBuilder, teamWikiAiConfig.AiEndpoint, teamWikiAiConfig.WikiConfig);

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
        var searchResult = await memoryClient.SearchAsync(query: query, index: request.WikiId.ToString(), limit: 5, filter: filter);

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
}