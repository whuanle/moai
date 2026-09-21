using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Commands;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="SaveTeamDynamicPluginCommand"/> 创建/更新团队动态插件实例.
/// </summary>
public class SaveTeamDynamicPluginCommandHandler : IRequestHandler<SaveTeamDynamicPluginCommand, EmptyCommandResponse>
{
    /// <summary>
    /// kg_cypher_query 模板 key：实例配置必须绑定本团队的知识图谱.
    /// </summary>
    private const string KgCypherTemplateKey = "kg_cypher_query";

    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveTeamDynamicPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SaveTeamDynamicPluginCommandHandler(DatabaseContext databaseContext, IPluginRegistry registry, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _registry = registry;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveTeamDynamicPluginCommand request, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(request, cancellationToken);

        var template = _registry.Get(request.TempleteKey);
        if (template == null || !template.IsDynamic)
        {
            throw new BusinessException("动态插件模板不存在") { StatusCode = 404 };
        }

        await EnsureKgBindingValidAsync(request, cancellationToken);

        var existing = await _databaseContext.PluginDynamics
            .FirstOrDefaultAsync(x => x.PluginKey == request.InstanceKey && x.IsDeleted == 0, cancellationToken);

        if (existing == null)
        {
            await EnsureTeamInstanceKeyUniqueAsync(request.TeamId, request.InstanceKey, cancellationToken);

            var newDynamic = new PluginDynamicEntity
            {
                PluginKey = request.InstanceKey,
                TempleteKey = request.TempleteKey,
                Config = request.Config,
            };

            _databaseContext.PluginDynamics.Add(newDynamic);
            await _databaseContext.SaveChangesAsync(cancellationToken);

            var pluginEntity = new PluginEntity
            {
                IsSystem = false,
                TeamId = (int)request.TeamId,
                PluginId = newDynamic.Id,
                PluginName = request.InstanceKey,
                Title = request.Title,
                Description = request.Description,
                Type = (int)PluginType.NativePlugin,
                ClassifyId = request.ClassifyId,
                IsPublic = true,
                Counter = 0,
            };

            _databaseContext.Plugins.Add(pluginEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var pluginEntity = await _databaseContext.Plugins
                .FirstOrDefaultAsync(x => x.PluginId == existing.Id && x.TeamId == request.TeamId && x.IsDeleted == 0, cancellationToken)
                ?? throw new BusinessException("实例 Key 已被使用") { StatusCode = 409 };

            existing.TempleteKey = request.TempleteKey;
            existing.Config = request.Config;

            pluginEntity.Title = request.Title;
            pluginEntity.Description = request.Description;
            pluginEntity.ClassifyId = request.ClassifyId;

            _databaseContext.PluginDynamics.Update(existing);
            _databaseContext.Plugins.Update(pluginEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// kg_cypher_query 实例：校验配置里的 kgId 存在且属于本团队（创建与更新都校验）.
    /// </summary>
    /// <param name="request">保存请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    private async Task EnsureKgBindingValidAsync(SaveTeamDynamicPluginCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.TempleteKey, KgCypherTemplateKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        long kgId;
        try
        {
            using var doc = JsonDocument.Parse(request.Config);
            var config = doc.RootElement;
            if (!config.TryGetProperty("kgId", out var kgElement) && !config.TryGetProperty("KgId", out kgElement))
            {
                throw new BusinessException("kg_cypher_query 配置必须包含 kgId（绑定的知识图谱 id）.") { StatusCode = 400 };
            }

            if (!kgElement.TryGetInt64(out kgId) || kgId <= 0)
            {
                throw new BusinessException("kg_cypher_query 配置的 kgId 必须大于 0.") { StatusCode = 400 };
            }
        }
        catch (JsonException)
        {
            throw new BusinessException("kg_cypher_query 配置必须是合法 JSON.") { StatusCode = 400 };
        }

        var graph = await _databaseContext.KnowledgeGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == kgId, cancellationToken);
        if (graph == null)
        {
            throw new BusinessException("绑定的知识图谱不存在.") { StatusCode = 404 };
        }

        if (graph.TeamId != request.TeamId)
        {
            throw new BusinessException("只能绑定本团队的知识图谱.") { StatusCode = 403 };
        }
    }

    private async Task EnsureManagerAsync(SaveTeamDynamicPluginCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理插件.") { StatusCode = 403 };
        }
    }

    private async Task EnsureTeamInstanceKeyUniqueAsync(long teamId, string instanceKey, CancellationToken cancellationToken)
    {
        var dbExists = await _databaseContext.Plugins
            .AnyAsync(x => x.TeamId == teamId && x.PluginName == instanceKey && x.IsDeleted == 0, cancellationToken);
        if (dbExists)
        {
            throw new BusinessException("实例 Key 已被使用") { StatusCode = 409 };
        }

        if (_registry.Get(instanceKey) != null)
        {
            throw new BusinessException("实例 Key 已被使用") { StatusCode = 409 };
        }
    }
}
