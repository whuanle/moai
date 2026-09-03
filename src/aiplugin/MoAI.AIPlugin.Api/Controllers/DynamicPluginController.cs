using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.AIPlugin.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.AIPlugin.Controllers;

/// <summary>
/// 动态插件管理接口（仅管理员）—— 动态插件实例的新建/更新/删除.
/// </summary>
[ApiController]
[Route("/ai/plugin/dynamic")]
public class DynamicPluginController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicPluginController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public DynamicPluginController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 新建/更新动态插件实例.
    /// </summary>
    /// <param name="req">保存请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("save")]
    public async Task<EmptyCommandResponse> Save([FromBody] SaveDynamicPluginCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除动态插件实例.
    /// </summary>
    /// <param name="req">删除请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete]
    public async Task<EmptyCommandResponse> Delete([FromBody] DeleteDynamicPluginCommand req, CancellationToken ct)
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
