using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.App.Workflow.Queries;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppWorkflowInstancesCommand"/>
/// </summary>
public class QueryAppWorkflowInstancesCommandHandler : IRequestHandler<QueryAppWorkflowInstancesCommand, QueryAppWorkflowInstancesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppWorkflowInstancesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryAppWorkflowInstancesCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppWorkflowInstancesCommandResponse> Handle(QueryAppWorkflowInstancesCommand request, CancellationToken cancellationToken)
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
            throw new BusinessException("需要团队管理员才能查看运行记录.") { StatusCode = 403 };
        }

        var query = _databaseContext.AppWorkflowInstances.Where(x => x.AppId == app.Id);

        if (request.Status != null)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.CreateTime)
            .ThenByDescending(x => x.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new
            {
                x.Id,
                x.AppId,
                x.Status,
                x.IsDebug,
                x.Version,
                x.Input,
                x.Output,
                x.ErrorMessage,
                x.StartTime,
                x.EndTime,
                x.CreateUserId,
                x.CreateTime,
                x.UpdateUserId,
                x.UpdateTime
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new AppWorkflowInstanceItem
            {
                InstanceId = x.Id,
                AppId = x.AppId,
                Status = x.Status,
                IsDebug = x.IsDebug,
                Version = x.Version,
                Input = x.Input,
                Output = x.Output,
                ErrorMessage = x.ErrorMessage,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                CreateUserId = (int)x.CreateUserId,
                CreateTime = x.CreateTime,
                UpdateUserId = (int)x.UpdateUserId,
                UpdateTime = x.UpdateTime,
            })
            .ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryAppWorkflowInstancesCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
        };
    }
}
