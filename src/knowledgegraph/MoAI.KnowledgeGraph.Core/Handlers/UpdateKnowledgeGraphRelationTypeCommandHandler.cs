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
/// <inheritdoc cref="UpdateKnowledgeGraphRelationTypeCommand"/>
/// </summary>
public class UpdateKnowledgeGraphRelationTypeCommandHandler : IRequestHandler<UpdateKnowledgeGraphRelationTypeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphRelationTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public UpdateKnowledgeGraphRelationTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphRelationTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KgId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var entity = await _databaseContext.KnowledgeGraphRelationTypes
            .FirstOrDefaultAsync(x => x.Id == request.RelationTypeId && x.KgId == request.KgId, cancellationToken)
            ?? throw new BusinessException("关系类型不存在.") { StatusCode = 404 };

        var nameExist = await _databaseContext.KnowledgeGraphRelationTypes
            .AnyAsync(x => x.KgId == request.KgId && x.Name == request.Name && x.Id != entity.Id, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("关系类型名称已存在.") { StatusCode = 409 };
        }

        if (request.SourceTypeId.HasValue)
        {
            var sourceExist = await _databaseContext.KnowledgeGraphEntityTypes
                .AnyAsync(x => x.KgId == request.KgId && x.Id == request.SourceTypeId.Value, cancellationToken);
            if (!sourceExist)
            {
                throw new BusinessException("关联的实体类型不存在.") { StatusCode = 400 };
            }
        }

        if (request.TargetTypeId.HasValue)
        {
            var targetExist = await _databaseContext.KnowledgeGraphEntityTypes
                .AnyAsync(x => x.KgId == request.KgId && x.Id == request.TargetTypeId.Value, cancellationToken);
            if (!targetExist)
            {
                throw new BusinessException("关联的实体类型不存在.") { StatusCode = 400 };
            }
        }

        entity.Name = request.Name;
        entity.Color = request.Color ?? string.Empty;
        entity.Description = request.Description ?? string.Empty;
        entity.SourceTypeId = request.SourceTypeId;
        entity.TargetTypeId = request.TargetTypeId;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
