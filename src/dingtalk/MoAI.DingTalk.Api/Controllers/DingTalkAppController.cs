using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.DingTalk.Commands;
using MoAI.DingTalk.Queries;
using MoAI.DingTalk.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.DingTalk.Controllers;

/// <summary>
/// 钉钉应用连接接口；钉钉应用是团队下的产物，创建/更新/删除需要团队 Admin 及以上角色，查看仅需团队成员.
/// 应用渠道接入走「创建即绑定」（创建命令携带渠道信息）；删除即彻底移除.
/// </summary>
[ApiController]
[Route("/dingtalk_app")]
public class DingTalkAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkAppController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public DingTalkAppController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队下的钉钉应用连接列表（含绑定渠道与在线状态），不回显 AppSecret；仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryDingTalkAppsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryDingTalkAppsCommandResponse> QueryDingTalkApps([FromQuery] QueryDingTalkAppsCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 创建钉钉应用连接，需要团队 Admin 及以上角色；AppKey 全局唯一，创建后立即建立 Stream 长连接；
    /// 携带渠道信息（channelType=app + channelId=应用 id）时创建即绑定，连接与绑定同一事务落库.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回钉钉应用记录 <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public Task<SimpleGuid> CreateDingTalkApp([FromBody] CreateDingTalkAppCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新钉钉应用连接（名称、AppKey、AppSecret、禁用），需要团队 Admin 及以上角色；AppKey/AppSecret 为空表示保持不变.
    /// </summary>
    /// <param name="id">钉钉应用记录 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}")]
    public async Task<EmptyCommandResponse> UpdateDingTalkApp([FromRoute] Guid id, [FromBody] UpdateDingTalkAppCommand req, CancellationToken ct)
    {
        var cmd = new UpdateDingTalkAppCommand
        {
            DingTalkAppId = id,
            Name = req.Name,
            Description = req.Description,
            AppKey = req.AppKey,
            AppSecret = req.AppSecret,
            IsDisable = req.IsDisable
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除钉钉应用连接，需要团队 Admin 及以上角色；删除即彻底移除（同时解除全部绑定并断开长连接），不保留可复用的连接.
    /// </summary>
    /// <param name="id">钉钉应用记录 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<EmptyCommandResponse> DeleteDingTalkApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new DeleteDingTalkAppCommand { DingTalkAppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
}
