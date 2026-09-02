using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.AiPlugin.Commands;
using MoAI.AiPlugin.Models;
using MoAI.AiPlugin.Queries;
using MoAI.AiPlugin.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;

namespace MoAI.AiPlugin.Controllers;

/// <summary>
/// 插件引擎管理接口（仅管理员）.
/// </summary>
[ApiController]
[Route("/ai/plugin")]
public class PluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public PluginController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询已发现插件列表（仅管理员）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginListCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryPluginListCommandResponse> QueryAll(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryPluginListCommand(), ct);
    }

    /// <summary>
    /// 执行插件（仅管理员）.
    /// </summary>
    /// <param name="req">执行请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PluginRunResult"/>.</returns>
    [HttpPost("run")]
    public async Task<PluginRunResult> Run([FromBody] RunPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理插件") { StatusCode = 403 };
        }
    }
}
