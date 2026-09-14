using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Feishu.Commands;
using MoAI.Feishu.Queries;
using MoAI.Feishu.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Feishu.Controllers;

/// <summary>
/// 飞书应用连接接口；飞书应用是团队下的产物，创建/更新/删除/绑定需要团队 Admin 及以上角色，查看仅需团队成员.
/// </summary>
[ApiController]
[Route("/feishu_app")]
public class FeishuAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuAppController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public FeishuAppController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询团队下的飞书应用连接列表（含绑定渠道与在线状态），不回显 AppSecret；仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryFeishuAppsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public Task<QueryFeishuAppsCommandResponse> QueryFeishuApps([FromQuery] QueryFeishuAppsCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 创建飞书应用连接，需要团队 Admin 及以上角色；AppID 全局唯一，创建后立即建立长连接.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回飞书应用记录 <see cref="SimpleGuid"/>.</returns>
    [HttpPost]
    public Task<SimpleGuid> CreateFeishuApp([FromBody] CreateFeishuAppCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新飞书应用连接（名称、描述、AppSecret、域名、禁用），需要团队 Admin 及以上角色；AppSecret 为空表示保持不变.
    /// </summary>
    /// <param name="id">飞书应用记录 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id:guid}")]
    public async Task<EmptyCommandResponse> UpdateFeishuApp([FromRoute] Guid id, [FromBody] UpdateFeishuAppCommand req, CancellationToken ct)
    {
        var cmd = new UpdateFeishuAppCommand
        {
            FeishuAppId = id,
            Name = req.Name,
            Description = req.Description,
            AppSecret = req.AppSecret,
            Domain = req.Domain,
            IsDisable = req.IsDisable
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除飞书应用连接，需要团队 Admin 及以上角色；删除时同时解除全部绑定并断开长连接.
    /// </summary>
    /// <param name="id">飞书应用记录 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<EmptyCommandResponse> DeleteFeishuApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new DeleteFeishuAppCommand { FeishuAppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 绑定飞书应用到渠道（应用/知识库等），需要团队 Admin 及以上角色；同一飞书应用同时只能绑定一个渠道.
    /// </summary>
    /// <param name="id">飞书应用记录 id.</param>
    /// <param name="req">绑定请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id:guid}/bind")]
    public async Task<EmptyCommandResponse> BindFeishuApp([FromRoute] Guid id, [FromBody] BindFeishuAppCommand req, CancellationToken ct)
    {
        var cmd = new BindFeishuAppCommand
        {
            FeishuAppId = id,
            ChannelType = req.ChannelType,
            ChannelId = req.ChannelId
        };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 解除飞书应用绑定，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">飞书应用记录 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id:guid}/unbind")]
    public async Task<EmptyCommandResponse> UnbindFeishuApp([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new UnbindFeishuAppCommand { FeishuAppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
}
