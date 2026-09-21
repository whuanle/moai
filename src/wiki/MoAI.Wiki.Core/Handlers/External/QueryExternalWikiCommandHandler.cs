using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Wiki.External;
using MoAI.Wiki.Queries.Responses;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalWikiCommand"/>
/// </summary>
public class QueryExternalWikiCommandHandler : IRequestHandler<QueryExternalWikiCommand, QueryWikiCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalWikiAuthorizer _externalWikiAuthorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalWikiAuthorizer">外部知识库授权器.</param>
    public QueryExternalWikiCommandHandler(DatabaseContext databaseContext, IExternalWikiAuthorizer externalWikiAuthorizer)
    {
        _databaseContext = databaseContext;
        _externalWikiAuthorizer = externalWikiAuthorizer;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCommandResponse> Handle(QueryExternalWikiCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _externalWikiAuthorizer.AuthorizeAsync(request.WikiId, request.Caller.TeamId, cancellationToken);

        var embeddingModelName = await _databaseContext.AiModels
            .Where(x => x.Id == wiki.EmbeddingModelId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var rerankModelName = wiki.RerankModelId.HasValue
            ? await _databaseContext.AiModels
                .Where(x => x.Id == wiki.RerankModelId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : string.Empty;

        // 应用 token 对团队知识库权限等价团队 Admin，MyRole 固定返回 Admin.
        return new QueryWikiCommandResponse
        {
            WikiId = wiki.Id,
            TeamId = wiki.TeamId,
            Name = wiki.Name,
            Description = wiki.Description,
            AvatarPath = wiki.AvatarPath,
            CreateTime = wiki.CreateTime,
            EmbeddingModelId = wiki.EmbeddingModelId,
            EmbeddingModelName = embeddingModelName,
            EmbeddingDimensions = wiki.EmbeddingDimensions,
            IsLock = wiki.IsLock,
            RerankModelId = wiki.RerankModelId,
            RerankModelName = rerankModelName,
            MyRole = (int)TeamRole.Admin,
        };
    }
}
