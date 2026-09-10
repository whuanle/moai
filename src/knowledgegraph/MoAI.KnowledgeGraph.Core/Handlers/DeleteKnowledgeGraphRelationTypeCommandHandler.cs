using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphRelationTypeCommand"/>
/// </summary>
public class DeleteKnowledgeGraphRelationTypeCommandHandler : IRequestHandler<DeleteKnowledgeGraphRelationTypeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphRelationTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphRelationTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphRelationTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var entity = await _databaseContext.KnowledgeGraphRelationTypes
            .FirstOrDefaultAsync(x => x.Id == request.RelationTypeId && x.KgId == request.KgId, cancellationToken)
            ?? throw new BusinessException("关系类型不存在.") { StatusCode = 404 };

        var edgeCount = await _store.CountEdgesByRelationTypeAsync(request.KgId, request.RelationTypeId, cancellationToken);
        if (edgeCount > 0)
        {
            throw new BusinessException("该关系类型下仍有关系，无法删除.") { StatusCode = 409 };
        }

        _databaseContext.KnowledgeGraphRelationTypes.Remove(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
