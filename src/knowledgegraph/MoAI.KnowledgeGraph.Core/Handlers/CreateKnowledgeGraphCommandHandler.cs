using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphCommand"/>
/// </summary>
public class CreateKnowledgeGraphCommandHandler : IRequestHandler<CreateKnowledgeGraphCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public CreateKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.RequireTeamRoleAsync(request.TeamId, adminOnly: true, cancellationToken);

        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力，请先在系统设置中配置 Neo4j.") { StatusCode = 409 };
        }

        KnowledgeGraphTemplate? template = null;
        if (!string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            template = KnowledgeGraphTemplates.Find(request.TemplateKey)
                ?? throw new BusinessException("知识图谱模板不存在.") { StatusCode = 400 };
        }

        var nameExist = await _databaseContext.KnowledgeGraphs
            .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("知识图谱名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var graph = new KnowledgeGraphEntity
        {
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            TemplateKey = request.TemplateKey,
        };
        _databaseContext.KnowledgeGraphs.Add(graph);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (template != null && template.EntityTypes.Count > 0)
        {
            var sort = 0;
            var typeNameToId = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var typeName in template.EntityTypes)
            {
                var entityType = new KnowledgeGraphEntityTypeEntity
                {
                    KgId = graph.Id,
                    Name = typeName,
                    Description = string.Empty,
                    Color = string.Empty,
                    Sort = sort++,
                };
                _databaseContext.KnowledgeGraphEntityTypes.Add(entityType);
                typeNameToId[typeName] = entityType.Id;
            }

            // 先确保实体类型落库拿到自增 id
            await _databaseContext.SaveChangesAsync(cancellationToken);

            sort = 0;
            foreach (var relation in template.RelationTypes)
            {
                _databaseContext.KnowledgeGraphRelationTypes.Add(new KnowledgeGraphRelationTypeEntity
                {
                    KgId = graph.Id,
                    Name = relation.Name,
                    Description = string.Empty,
                    Color = string.Empty,
                    Sort = sort++,
                    SourceTypeId = relation.SourceType != null && typeNameToId.TryGetValue(relation.SourceType, out var sid) ? sid : null,
                    TargetTypeId = relation.TargetType != null && typeNameToId.TryGetValue(relation.TargetType, out var tid) ? tid : null,
                });
            }

            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return new SimpleLong { Value = graph.Id };
    }
}
