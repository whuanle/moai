using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Prompt.Commands;
using MoAI.Prompt.Queries;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Controllers;

/// <summary>
/// 提示词接口；个人提示词仅本人管理，团队提示词仅团队 Admin 及以上可管理，上架到市场后所有人可见.
/// </summary>
[ApiController]
[Route("/prompt")]
public class PromptController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public PromptController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 创建提示词；TeamId 为 0 创建个人提示词，TeamId 大于 0 创建团队提示词，需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回提示词 <see cref="SimpleInt"/>.</returns>
    [HttpPost]
    public Task<SimpleInt> CreatePrompt([FromBody] CreatePromptCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新提示词；个人提示词仅创建人可改，团队提示词需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">提示词 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public Task<EmptyCommandResponse> UpdatePrompt(int id, [FromBody] UpdatePromptCommand req, CancellationToken ct)
    {
        var cmd = new UpdatePromptCommand
        {
            PromptId = id,
            Name = req.Name,
            Description = req.Description,
            Content = req.Content,
            PromptClassId = req.PromptClassId,
            AvatarPath = req.AvatarPath,
        };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除提示词；个人提示词仅创建人可删，团队提示词需要团队 Admin 及以上角色；同步移除该提示词待审核的上架申请.
    /// </summary>
    /// <param name="id">提示词 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> DeletePrompt(int id, CancellationToken ct)
    {
        var cmd = new DeletePromptCommand { PromptId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 设置提示词头像；个人提示词仅创建人可设置，团队提示词需要团队 Admin 及以上角色；objectKey 需为已完成上传并登记的文件.
    /// </summary>
    /// <param name="id">提示词 id.</param>
    /// <param name="req">头像请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id}/avatar")]
    public Task<EmptyCommandResponse> UpdatePromptAvatar(int id, [FromBody] UpdatePromptAvatarCommand req, CancellationToken ct)
    {
        var cmd = new UpdatePromptAvatarCommand { PromptId = id, ObjectKey = req.ObjectKey };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询我的个人提示词列表.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPromptListCommandResponse"/>.</returns>
    [HttpGet("my_list")]
    public Task<QueryPromptListCommandResponse> QueryMyList([FromQuery] QueryUserPromptListCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队提示词列表，仅团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPromptListCommandResponse"/>.</returns>
    [HttpGet("team_list")]
    public Task<QueryPromptListCommandResponse> QueryTeamList([FromQuery] QueryTeamPromptListCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询提示词详情（含内容）；个人提示词仅创建人、团队提示词仅团队成员可看，已上架市场的所有人可看.
    /// </summary>
    /// <param name="id">提示词 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPromptCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public Task<QueryPromptCommandResponse> QueryPrompt(int id, CancellationToken ct)
    {
        var cmd = new QueryPromptCommand { PromptId = id };
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询提示词市场列表（已上架的提示词），所有登录用户可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPromptListCommandResponse"/>.</returns>
    [HttpGet("market_list")]
    public Task<QueryPromptListCommandResponse> QueryMarketList([FromQuery] QueryPromptMarketListCommand req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }
}
