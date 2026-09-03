using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;

namespace MoAI.AIPlugin.Controllers;

/// <summary>
/// 插件管理接口（仅管理员）——插件列表.
/// </summary>
[ApiController]
[Route("/ai/plugin/manage")]
public class PluginManageController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginManageController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public PluginManageController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询插件管理列表（DB 来源，含分类信息）.
    /// </summary>
    /// <param name="kind">插件种类：custom|dynamic|static，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginManageListCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryPluginManageListCommandResponse> QueryList([FromQuery] string? kind, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryPluginManageListCommand { Kind = kind }, ct);
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
