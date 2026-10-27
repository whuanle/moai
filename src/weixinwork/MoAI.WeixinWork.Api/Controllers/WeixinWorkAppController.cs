using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.WeixinWork.Commands;
using MoAI.WeixinWork.Queries;
using MoAI.WeixinWork.Queries.Responses;

namespace MoAI.WeixinWork.Controllers;

/// <summary>
/// 企业微信应用连接接口；企业微信应用是团队下的产物，创建/更新/删除需要团队 Admin 及以上角色，查看仅需团队成员.
/// 应用渠道接入走「创建即绑定」（创建命令携带渠道信息）；删除即彻底移除.
/// </summary>
[ApiController]
[Route("/weixinwork_app")]
public class WeixinWorkAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinWorkAppController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public WeixinWorkAppController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队下的企业微信应用连接列表（含绑定渠道与状态），不回显 Secret/Token/EncodingAESKey；仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryWeixinWorkAppsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryWeixinWorkAppsCommandResponse> QueryWeixinWorkApps([FromQuery] QueryWeixinWorkAppsCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 创建企业微信应用连接，需要团队 Admin 及以上角色；CorpId + AgentId 全局唯一；
    /// 携带渠道信息（channelType=app + channelId=应用 id）时创建即绑定，连接与绑定同一事务落库.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回企业微信应用记录 <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public Task<SimpleGuid> CreateWeixinWorkApp([FromBody] CreateWeixinWorkAppCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新企业微信应用连接（名称、企业 ID、AgentId、Secret、回调 Token/EncodingAESKey、API 地址、禁用），
    /// 需要团队 Admin 及以上角色；Secret/Token/EncodingAESKey 为空表示保持不变.
    /// </summary>
    /// <param name="id">企业微信应用记录 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}")]
    public async Task<EmptyCommandResponse> UpdateWeixinWorkApp([FromRoute] Guid id, [FromBody] UpdateWeixinWorkAppCommand req, CancellationToken ct)
    {
        var cmd = new UpdateWeixinWorkAppCommand
        {
            WeixinWorkAppId = id,
            Name = req.Name,
            Description = req.Description,
            CorpId = req.CorpId,
            AgentId = req.AgentId,
            CorpSecret = req.CorpSecret,
            Token = req.Token,
            EncodingAesKey = req.EncodingAesKey,
            ApiEndpoint = req.ApiEndpoint,
            IsDisable = req.IsDisable
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除企业微信应用连接，需要团队 Admin 及以上角色；删除即彻底移除（同时解除全部绑定），不保留可复用的连接.
    /// </summary>
    /// <param name="id">企业微信应用记录 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<EmptyCommandResponse> DeleteWeixinWorkApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new DeleteWeixinWorkAppCommand { WeixinWorkAppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
}
