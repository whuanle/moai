using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="GenerateDocumentChunkMetadataCommand"/>
/// </summary>
public class GenerateDocumentChunkMetadataCommandHandler : IRequestHandler<GenerateDocumentChunkMetadataCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly WikiEmbeddingService _embeddingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateDocumentChunkMetadataCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="embeddingService">文档向量化领域服务.</param>
    public GenerateDocumentChunkMetadataCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        WikiEmbeddingService embeddingService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _embeddingService = embeddingService;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(GenerateDocumentChunkMetadataCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var documentExists = await _databaseContext.WikiDocuments
            .AnyAsync(x => x.Id == request.DocumentId && x.WikiId == request.WikiId && x.IsDeleted == 0, cancellationToken);
        if (!documentExists)
        {
            throw new BusinessException("知识库文档不存在.") { StatusCode = 404 };
        }

        await EnsureMetadataModelAsync(request.MetadataModelId, wiki.TeamId, cancellationToken);
        var strategyTypes = request.StrategyType.HasValue
            ? new List<MoAI.Wiki.Models.MetadataGenerationStrategy> { request.StrategyType.Value }
            : null;
        var generatedCount = await _embeddingService.GenerateAndSaveChunkMetadataAsync(
            (int)request.WikiId,
            (int)request.DocumentId,
            request.MetadataModelId,
            request.ChunkIds,
            request.AppendExisting,
            strategyTypes,
            cancellationToken);
        return generatedCount;
    }

    private async Task EnsureMetadataModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == modelId && x.Enabled && x.IsDeleted == 0, cancellationToken);
        if (model == null)
        {
            throw new BusinessException("元数据生成模型不存在或未启用.") { StatusCode = 404 };
        }

        if (!string.Equals(model.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是对话模型，无法用于元数据生成.") { StatusCode = 400 };
        }

        if (model.IsPublic)
        {
            return;
        }

        var authorized = await _databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
        if (!authorized)
        {
            throw new BusinessException("元数据生成模型未授权给你的团队使用.") { StatusCode = 403 };
        }
    }
}