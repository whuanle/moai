using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;
using Npgsql;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphCommand"/>
/// </summary>
public class CreateKnowledgeGraphCommandHandler : IRequestHandler<CreateKnowledgeGraphCommand, SimpleLong>
{
    private const string NameUniqueConstraintName = "idx_kg_team_name_live_uindex";

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    /// <param name="store">图存储.</param>
    public CreateKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
        _store = store;
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

        if (string.IsNullOrWhiteSpace(settings.Uri))
        {
            throw new BusinessException("知识图谱已开启但未配置 Neo4j 连接地址，请先在系统设置中完善.") { StatusCode = 409 };
        }

        // 模板复制（图谱 + 实体类型 + 关系类型）必须整体成功或整体回滚
        await using var transaction = await _databaseContext.Database.BeginTransactionAsync(cancellationToken);

        if (request.Mode == KnowledgeGraphModes.Connected)
        {
            var database = request.Database!.Trim();
            if (!await _store.ProbeDatabaseAsync(database, cancellationToken))
            {
                throw new BusinessException("数据库不存在或无法访问.") { StatusCode = 400 };
            }

            var connectedNameExist = await _databaseContext.KnowledgeGraphs
                .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);
            if (connectedNameExist)
            {
                throw new BusinessException("知识图谱名称已存在，请更换后重试.") { StatusCode = 409 };
            }

            var connectedGraph = new KnowledgeGraphEntity
            {
                TeamId = (int)request.TeamId,
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                TemplateKey = null,
                Mode = KnowledgeGraphModes.Connected,
                Database = database,
            };
            _databaseContext.KnowledgeGraphs.Add(connectedGraph);
            try
            {
                await _databaseContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                if (IsNameUniqueConstraintViolation(ex))
                {
                    throw new BusinessException("知识图谱名称已存在，请更换后重试.") { StatusCode = 409 };
                }

                throw;
            }

            await transaction.CommitAsync(cancellationToken);
            return new SimpleLong { Value = connectedGraph.Id };
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
            Mode = KnowledgeGraphModes.Managed,
            Database = null,
        };
        _databaseContext.KnowledgeGraphs.Add(graph);
        try
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (IsNameUniqueConstraintViolation(ex))
            {
                throw new BusinessException("知识图谱名称已存在，请更换后重试.") { StatusCode = 409 };
            }

            throw;
        }

        if (template != null && template.EntityTypes.Count > 0)
        {
            var sort = 0;
            var entityTypes = new List<KnowledgeGraphEntityTypeEntity>();
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
                entityTypes.Add(entityType);
            }

            // 先确保实体类型落库拿到自增 id，再构建名称到 id 的映射
            await _databaseContext.SaveChangesAsync(cancellationToken);

            var typeNameToId = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var entityType in entityTypes)
            {
                typeNameToId[entityType.Name] = entityType.Id;
            }

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

        await transaction.CommitAsync(cancellationToken);
        return new SimpleLong { Value = graph.Id };
    }

    private static bool IsNameUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgresException
                && string.Equals(postgresException.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
                && string.Equals(postgresException.ConstraintName, NameUniqueConstraintName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
