// <copyright file="EmbeddingDocumentCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi.MQ;
using MaomiAI.Document.Core.Consumers.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 向量化文档.
/// </summary>
public class EmbeddingDocumentCommandHandler : IRequestHandler<EmbeddingDocumentCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="messagePublisher"></param>
    public EmbeddingDocumentCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions, IMessagePublisher messagePublisher)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _messagePublisher = messagePublisher;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(EmbeddingDocumentCommand request, CancellationToken cancellationToken)
    {
        var documentTask = await _databaseContext.WikiDocumentTasks
            .AnyAsync(x => x.DocumentId == request.DocumentId && x.State < (int)FileEmbeddingState.Processing);

        if (documentTask == true)
        {
            throw new BusinessException("当前文档已在处理任务，请勿重复添加") { StatusCode = 409 };
        }

        var fileId = await _databaseContext.WikiDocuments.Where(x => x.Id == request.DocumentId)
            .Select(x => x.FileId)
            .FirstOrDefaultAsync();

        if (fileId == default)
        {
            throw new BusinessException("知识库文档不存在") { StatusCode = 404 };
        }

        var teamWikiConfig = await _databaseContext.Wikis
            .Where(x => x.Id == request.WikiId)
            .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, b) => new
            {
                b.Id,
                b.AiModelType,
            }).FirstOrDefaultAsync();

        if (teamWikiConfig == null || !AiModelType.Embedding.ToString().Equals(teamWikiConfig.AiModelType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("知识库未配置向量化模型") { StatusCode = 409 };
        }

        var documentTaskEntity = new WikiDocumentTaskEntity
        {
            DocumentId = request.DocumentId,
            WikiId = request.WikiId,
            TaskTag = Guid.NewGuid().ToString(),
            State = (int)FileEmbeddingState.Wait,
            MaxTokensPerParagraph = request.MaxTokensPerParagraph,
            OverlappingTokens = request.OverlappingTokens,
            Tokenizer = TextToJsonExtensions.ToJsonString(request.Tokenizer),
            Message = "任务已创建"
        };

        await _databaseContext.WikiDocumentTasks.AddAsync(documentTaskEntity, cancellationToken);
        await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).ExecuteUpdateAsync(x => x.SetProperty(x => x.IsLock, true), cancellationToken: cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 后台处理
        var embeddingDocumentEvent = new EmbeddingDocumentTask
        {
            DocumentId = request.DocumentId,
            WikiId = request.WikiId,
            TaskId = documentTaskEntity.Id,
        };

        await _messagePublisher.AutoPublishAsync(embeddingDocumentEvent);

        return EmptyCommandResponse.Default;
    }
}
