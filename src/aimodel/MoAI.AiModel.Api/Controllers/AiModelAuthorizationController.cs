using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.AiModel.Authorization.Commands;
using MoAI.AiModel.Authorization.Queries;
using MoAI.AiModel.Authorization.Queries.Responses;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Controllers;

/// <summary>
/// AI 模型授权管理.
/// </summary>
[ApiController]
[Route("/aimodel/authorization")]
[EndpointGroupName("aimodel")]
public class AiModelAuthorizationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelAuthorizationController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AiModelAuthorizationController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询所有模型及其授权的团队列表.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>模型授权列表.</returns>
    [HttpPost("models")]
    public async Task<QueryModelAuthorizationsCommandResponse> QueryModelAuthorizations(
        [FromBody] QueryModelAuthorizationsCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询所有团队及其授权的模型列表.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>团队授权列表.</returns>
    [HttpPost("teams")]
    public async Task<QueryTeamAuthorizationsCommandResponse> QueryTeamAuthorizations(
        [FromBody] QueryTeamAuthorizationsCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改某个模型的授权团队列表.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("model/update")]
    public async Task<EmptyCommandResponse> UpdateModelAuthorizations(
        [FromBody] UpdateModelAuthorizationsCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量授权模型给某个团队.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("team/authorize")]
    public async Task<EmptyCommandResponse> BatchAuthorizeModelsToTeam(
        [FromBody] BatchAuthorizeModelsToTeamCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量撤销某个团队的模型授权.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("team/revoke")]
    public async Task<EmptyCommandResponse> BatchRevokeModelsFromTeam(
        [FromBody] BatchRevokeModelsFromTeamCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckIsAdminAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);
        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}
