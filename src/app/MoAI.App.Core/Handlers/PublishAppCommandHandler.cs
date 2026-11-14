using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.TeamAgent.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="PublishAppCommand"/>
/// </summary>
public class PublishAppCommandHandler : IRequestHandler<PublishAppCommand, EmptyCommandResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public PublishAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(PublishAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以发布应用.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持发布使用.") { StatusCode = 400 };
        }

        // 版本号只能在发布时设置：携带 aaa.bb.cc 文本则把当前版本更新为该值（编码为 int 落库），格式已由命令校验、此处防御性再验；
        // 版本号必须严格递增（int 编码直接比较），相同或低于当前版本一律拒绝；不携带时保持原值（快捷重发语义）
        if (!string.IsNullOrWhiteSpace(request.Version))
        {
            if (!AppVersionHelper.TryParse(request.Version, out var version))
            {
                throw new BusinessException("应用版本号格式不正确，应为 aaa.bb.cc（如 1.0.3）.") { StatusCode = 400 };
            }

            if (version <= app.Version)
            {
                throw new BusinessException($"应用版本号必须大于当前版本 {AppVersionHelper.Format(app.Version)}，请递增后再发布.") { StatusCode = 400 };
            }

            app.Version = version;
        }

        // 发布即快照当前配置：正式会话按快照执行，之后的保存只落草稿，直到下次发布
        var config = await _databaseContext.AppAgentConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        if (config == null)
        {
            // 配置行尚未创建（应用建后未保存过配置）时以默认值建行，保证已发布应用必有快照
            config = new AppAgentConfigEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = app.TeamId,
                AppId = app.Id,
                Prompt = string.Empty,
                WikiIds = "[]",
                GraphIds = "[]",
                Plugins = "[]",
                Skills = "[]",
                Prompts = "[]",
                QuickInputs = "[]",
                WorkflowApps = "[]",
                AgentApps = "[]",
                ExecutionSettings = "{}",
                OpeningStatement = string.Empty,
            };
            _databaseContext.AppAgentConfigs.Add(config);
        }

        // 团队智能体内嵌定义：按当前绑定把智能体定义（主提示词/偏好模型/子智能体）冻结进快照，
        // 发布后编辑或删除团队智能体不影响已发布应用；停用/删除的绑定在发布时兜底拒绝
        await FreezeTeamAgentDefsAsync(app, config, cancellationToken);

        config.PublishedConfig = AppAgentConfigSnapshot.Serialize(config);
        config.Status = 1;

        app.PublishStatus = 1;
        app.PublishTime = DateTimeOffset.Now;
        app.PublishVersion = app.Version;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 发布前冻结团队智能体定义到 <c>team_agent_defs</c>（随快照序列化）：
    /// 绑定列表为空时清空；存在已删除/停用/非本团队的绑定时拒绝发布.
    /// </summary>
    private async Task FreezeTeamAgentDefsAsync(AppEntity app, AppAgentConfigEntity config, CancellationToken cancellationToken)
    {
        var ids = AppAgentConfigJson.ParseGuidList(config.TeamAgentIds);
        if (ids.Count == 0)
        {
            config.TeamAgentDefs = string.Empty;
            return;
        }

        var rows = await _databaseContext.TeamAgents
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (rows.Count != ids.Count || rows.Any(x => !x.Enabled || x.TeamId != app.TeamId))
        {
            throw new BusinessException("绑定的团队智能体已被删除或停用，无法发布，请先调整应用配置.") { StatusCode = 400 };
        }

        var rowMap = rows.ToDictionary(x => x.Id);
        var defs = new List<SnapshotTeamAgent>();
        foreach (var id in ids)
        {
            if (!rowMap.TryGetValue(id, out var row))
            {
                continue;
            }

            defs.Add(new SnapshotTeamAgent
            {
                Id = row.Id,
                Key = row.Key ?? string.Empty,
                Name = row.Name,
                Description = row.Description ?? string.Empty,
                AvatarObjectKey = row.AvatarObjectKey ?? string.Empty,
                MainPrompt = row.MainPrompt ?? string.Empty,
                MainPluginIds = AppAgentConfigJson.ParseGuidList(row.MainPluginIds),
                MainWikiIds = AppAgentConfigJson.ParseLongList(row.MainWikiIds),
                MainGraphIds = AppAgentConfigJson.ParseLongList(row.MainGraphIds),
                Subs = ParseSubs(row.Subs),
            });
        }

        config.TeamAgentDefs = SnapshotTeamAgent.SerializeDefs(defs);
    }

    /// <summary>解析 team_agent.subs JSON（TeamAgentSubItem 数组，camelCase 写入）并映射为快照子智能体定义；非法内容按空列表兜底.</summary>
    private static List<SnapshotTeamAgentSub> ParseSubs(string? subsJson)
    {
        if (string.IsNullOrWhiteSpace(subsJson))
        {
            return [];
        }

        try
        {
            // team_agent.subs 由 TeamAgentJson 按 camelCase（Web 默认）序列化，反序列化须同口径
            var subs = JsonSerializer.Deserialize<List<TeamAgentSubItem>>(subsJson, JsonOptions) ?? [];
            return subs.Select(x => new SnapshotTeamAgentSub
            {
                Key = x.Key ?? string.Empty,
                Name = x.Name ?? string.Empty,
                Description = x.Description ?? string.Empty,
                Prompt = x.Prompt ?? string.Empty,
                PluginIds = x.PluginIds?.ToList() ?? [],
                WikiIds = x.WikiIds?.ToList() ?? [],
                GraphIds = x.GraphIds?.ToList() ?? [],
            }).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
