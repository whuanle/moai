using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppSecurityEventsCommand"/>
/// </summary>
public class QueryAppSecurityEventsCommandHandler : IRequestHandler<QueryAppSecurityEventsCommand, QueryAppSecurityEventsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppSecurityEventsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppSecurityEventsCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppSecurityEventsCommandResponse> Handle(QueryAppSecurityEventsCommand request, CancellationToken cancellationToken)
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

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以查看应用安全事件.") { StatusCode = 403 };
        }

        var query = _databaseContext.AppSecurityEvents
            .AsNoTracking()
            .Where(x => x.AppId == app.Id);

        if (!string.IsNullOrEmpty(request.Scope))
        {
            query = query.Where(x => x.Scope == request.Scope);
        }

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            // 内置规则（手机号/证件等）rule_name 为空串：除规则名外同时匹配规则类型代码（大小写不敏感），
            // 并把常见中文类型别名归一到代码，保证搜「手机号」能命中 rule_type=phone
            var keyword = request.Keyword.Trim();
            var typeAlias = keyword switch
            {
                "手机号" or "手机" => "phone",
                "身份证号" or "身份证" => "idcard",
                "邮箱" or "email" => "email",
                "银行卡号" or "银行卡" => "bankcard",
                "自定义正则" => "custom",
                "提示词注入" or "注入" => "injection",
                _ => null,
            };
            query = query.Where(x =>
                x.RuleName.Contains(keyword)
                || x.RuleType.ToLower().Contains(keyword.ToLower())
                || (typeAlias != null && x.RuleType.ToLower() == typeAlias));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new AppSecurityEventItem
            {
                Id = x.Id,
                SessionId = x.SessionId,
                UserId = x.UserId,
                Scope = x.Scope,
                RuleName = x.RuleName,
                RuleType = x.RuleType,
                Strategy = x.Strategy,
                Action = x.Action,
                HitCount = x.HitCount,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryAppSecurityEventsCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
        };
    }
}
