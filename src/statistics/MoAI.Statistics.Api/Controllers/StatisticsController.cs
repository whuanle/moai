using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Statistics.Queries;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Controllers;

/// <summary>
/// 资源使用统计接口（仅管理员）——统一统计表查询.
/// </summary>
[ApiController]
[Route("/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatisticsController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public StatisticsController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 分页查询资源使用统计（每日），可按资源类型名与日期区间过滤.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryResourceUsageDailyCommandResponse"/>.</returns>
    [HttpGet("usage/daily")]
    public async Task<QueryResourceUsageDailyCommandResponse> QueryDaily([FromQuery] QueryResourceUsageDailyCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 分页查询资源使用统计（总量），可按资源类型名过滤.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryResourceUsageTotalCommandResponse"/>.</returns>
    [HttpGet("usage/total")]
    public async Task<QueryResourceUsageTotalCommandResponse> QueryTotal([FromQuery] QueryResourceUsageTotalCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询已有统计的资源类型名清单.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryResourceUsageNamesCommandResponse"/>.</returns>
    [HttpGet("usage/names")]
    public async Task<QueryResourceUsageNamesCommandResponse> QueryNames(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryResourceUsageNamesCommand(), ct);
    }

    /// <summary>
    /// 按日聚合查询资源使用趋势（数据分析），区间内无数据的日期补 0.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryResourceUsageTrendCommandResponse"/>.</returns>
    [HttpGet("usage/trend")]
    public async Task<QueryResourceUsageTrendCommandResponse> QueryTrend([FromQuery] QueryResourceUsageTrendCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以查看资源使用统计") { StatusCode = 403 };
        }
    }
}
