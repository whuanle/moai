using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppSecurityCommand"/>
/// </summary>
public class QueryAppSecurityCommandHandler : IRequestHandler<QueryAppSecurityCommand, QueryAppSecurityCommandResponse>
{
    private static readonly JsonSerializerOptions RuleJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppSecurityCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppSecurityCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppSecurityCommandResponse> Handle(QueryAppSecurityCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        // 配置行可能尚未创建（应用未保存过安全设置），此时返回默认值而非 404
        var config = await _databaseContext.AppSecurityConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        return new QueryAppSecurityCommandResponse
        {
            AppId = app.Id,
            Enabled = config?.Enabled ?? false,
            MaskToolResult = config?.MaskToolResult ?? true,
            MaskToolArgs = config?.MaskToolArgs ?? false,
            MaskModelOutput = config?.MaskModelOutput ?? false,
            Rules = ParseRules(config?.Rules),
            ModelOutputRules = ParseRules(config?.ModelOutputRules),
            MyRole = (int)myRole.Value,
        };
    }

    /// <summary>
    /// 解析规则 JSON；损坏内容静默按空列表返回（页面保存一次即修复）.
    /// </summary>
    internal static List<AppSecurityPolicy.AppSecurityRule> ParseRules(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AppSecurityPolicy.AppSecurityRule>>(rulesJson, RuleJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
