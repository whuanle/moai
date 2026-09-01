using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Settings.Commands;
using MoAI.Settings.Queries;
using MoAI.Settings.Queries.Responses;

namespace MoAI.Settings.Controllers;

/// <summary>
/// 系统设置相关接口.
/// </summary>
[ApiController]
[Route("/settings")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider"></param>
    public SettingsController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询全部设置项（仅管理员可访问）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySettingsCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QuerySettingsCommandResponse> QuerySettings(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以访问设置项") { StatusCode = 403 };
        }

        return await _mediator.Send(new QuerySettingsCommand(), ct);
    }

    /// <summary>
    /// 保存设置项（仅超级管理员可修改）.
    /// </summary>
    /// <param name="req">保存设置请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut]
    public async Task<EmptyCommandResponse> SaveSetting([FromBody] SaveSettingCommand req, CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsRoot)
        {
            throw new BusinessException("只有超级管理员可以修改设置项") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}
