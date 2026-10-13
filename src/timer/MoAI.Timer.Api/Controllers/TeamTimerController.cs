using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Timer.Commands;
using MoAI.Timer.Queries;
using MoAI.Timer.Queries.Responses;

namespace MoAI.Timer.Controllers;

/// <summary>
/// 团队定时器接口：绑定团队 Agent/流程应用按 cron 定时无头执行.
/// </summary>
[ApiController]
[Route("/timer")]
public class TeamTimerController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTimerController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者（路由参数构造的命令需显式携带用户 id）.</param>
    public TeamTimerController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 创建定时器，需要团队 Admin 及以上角色；绑定应用必须是本团队已发布的 Agent/流程应用.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回定时器 id.</returns>
    [HttpPost]
    public Task<SimpleGuid> CreateTimer([FromBody] CreateTeamTimerCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新定时器，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">定时器 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public Task<EmptyCommandResponse> UpdateTimer(Guid id, [FromBody] UpdateTeamTimerCommand req, CancellationToken ct)
    {
        return _mediator.Send(new UpdateTeamTimerCommand
        {
            TimerId = id,
            Name = req.Name,
            Description = req.Description,
            AppId = req.AppId,
            CronExpression = req.CronExpression,
            InputContent = req.InputContent,
            IsEnabled = req.IsEnabled,
            MaxRetryCount = req.MaxRetryCount,
            RetryIntervalSeconds = req.RetryIntervalSeconds,
            ContextUserId = _userContextProvider.GetUserContext().UserId,
            ContextUserType = _userContextProvider.GetUserContext().UserType
        }, ct);
    }

    /// <summary>
    /// 删除定时器（软删除并移除调度），需要团队 Admin 及以上角色；执行历史保留.
    /// </summary>
    /// <param name="id">定时器 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> DeleteTimer(Guid id, CancellationToken ct)
    {
        return _mediator.Send(new DeleteTeamTimerCommand
        {
            TimerId = id,
            ContextUserId = _userContextProvider.GetUserContext().UserId
        }, ct);
    }

    /// <summary>
    /// 立即执行一次（不改动调度，异步执行并计入执行历史），需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">定时器 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回执行记录 id.</returns>
    [HttpPost("{id}/run")]
    public Task<SimpleGuid> RunTimer(Guid id, CancellationToken ct)
    {
        return _mediator.Send(new RunTeamTimerCommand
        {
            TimerId = id,
            ContextUserId = _userContextProvider.GetUserContext().UserId
        }, ct);
    }

    /// <summary>
    /// 查询团队定时器列表（含下次调度时间），仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamTimersCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryTeamTimersCommandResponse> QueryTimers([FromQuery] QueryTeamTimersCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询定时器执行历史（按触发时间倒序分页，默认页大小 10），仅团队成员可访问.
    /// </summary>
    /// <param name="id">定时器 id.</param>
    /// <param name="pageNo">页码，从 1 开始.</param>
    /// <param name="pageSize">页大小，默认 10.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamTimerExecutionsCommandResponse"/>.</returns>
    [HttpGet("{id}/executions")]
    public Task<QueryTeamTimerExecutionsCommandResponse> QueryExecutions(Guid id, [FromQuery] int pageNo = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        return _mediator.Send(new QueryTeamTimerExecutionsCommand
        {
            TimerId = id,
            PageNo = pageNo,
            PageSize = pageSize
        }, ct);
    }
}
