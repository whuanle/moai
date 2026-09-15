using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.External;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateExternalWikiEmbeddingCommand"/>
/// </summary>
public class UpdateExternalWikiEmbeddingCommandHandler : IRequestHandler<UpdateExternalWikiEmbeddingCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExternalWikiEmbeddingCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    public UpdateExternalWikiEmbeddingCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateExternalWikiEmbeddingCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        if (wiki.IsLock)
        {
            throw new BusinessException("知识库已有文档向量化，配置已锁定，不能修改.") { StatusCode = 409 };
        }

        await EnsureEmbeddingModelAsync(request.EmbeddingModelId, wiki.TeamId, cancellationToken);

        wiki.EmbeddingModelId = request.EmbeddingModelId;
        wiki.EmbeddingDimensions = request.EmbeddingDimensions;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task EnsureEmbeddingModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == modelId && x.Enabled, cancellationToken);
        if (model == null)
        {
            throw new BusinessException("向量化模型不存在或未启用.") { StatusCode = 404 };
        }

        if (!string.Equals(model.ModelKind, "embedding", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是向量化模型.") { StatusCode = 400 };
        }

        await EnsureAuthorizedAsync(modelId, teamId, cancellationToken);
    }

    private async Task EnsureAuthorizedAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var isPublic = await _databaseContext.AiModels
            .Where(x => x.Id == modelId)
            .Select(x => x.IsPublic)
            .FirstOrDefaultAsync(cancellationToken);
        if (isPublic)
        {
            return;
        }

        var authorized = await _databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
        if (!authorized)
        {
            throw new BusinessException("该模型未授权给你的团队使用.") { StatusCode = 403 };
        }
    }
}
