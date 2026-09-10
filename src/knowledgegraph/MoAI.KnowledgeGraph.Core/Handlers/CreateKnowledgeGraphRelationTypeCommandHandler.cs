using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphRelationTypeCommand"/>
/// </summary>
public class CreateKnowledgeGraphRelationTypeCommandHandler : IRequestHandler<CreateKnowledgeGraphRelationTypeCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphRelationTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public CreateKnowledgeGraphRelationTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateKnowledgeGraphRelationTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KgId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var nameExist = await _databaseContext.KnowledgeGraphRelationTypes
            .AnyAsync(x => x.KgId == request.KgId && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("关系类型名称已存在.") { StatusCode = 409 };
        }

        await EnsureEntityTypesExistAsync(request.KgId, request.SourceTypeId, request.TargetTypeId, cancellationToken);

        var maxSort = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KgId == request.KgId)
            .Select(x => (int?)x.Sort)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new KnowledgeGraphRelationTypeEntity
        {
            KgId = request.KgId,
            Name = request.Name,
            Color = request.Color ?? string.Empty,
            Description = request.Description ?? string.Empty,
            SourceTypeId = request.SourceTypeId,
            TargetTypeId = request.TargetTypeId,
            Sort = maxSort + 1,
        };
        _databaseContext.KnowledgeGraphRelationTypes.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleLong { Value = entity.Id };
    }

    private async Task EnsureEntityTypesExistAsync(long kgId, long? sourceTypeId, long? targetTypeId, CancellationToken cancellationToken)
    {
        if (sourceTypeId.HasValue)
        {
            var sourceExist = await _databaseContext.KnowledgeGraphEntityTypes
                .AnyAsync(x => x.KgId == kgId && x.Id == sourceTypeId.Value, cancellationToken);
            if (!sourceExist)
            {
                throw new BusinessException("关联的实体类型不存在.") { StatusCode = 400 };
            }
        }

        if (targetTypeId.HasValue)
        {
            var targetExist = await _databaseContext.KnowledgeGraphEntityTypes
                .AnyAsync(x => x.KgId == kgId && x.Id == targetTypeId.Value, cancellationToken);
            if (!targetExist)
            {
                throw new BusinessException("关联的实体类型不存在.") { StatusCode = 400 };
            }
        }
    }
}
