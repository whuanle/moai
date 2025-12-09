using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.AiModel.Commands;
using MoAI.AiModel.Queries;
using MoAI.AiModel.Queries.Respones;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Controllers;

/// <summary>
/// AI 模型管理.
/// </summary>
[ApiController]
[Route("/admin/aimodel")]
public partial class AiModelManagerController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelManagerController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AiModelManagerController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 管理员：添加 AI 模型.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="AddAiModelCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleInt"/>，包含新建模型的 Id.</returns>
    [HttpPost("add_aimodel")]
    public async Task<SimpleInt> AddAiModel([FromBody] AddAiModelCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 管理员：删除 AI 模型.
    /// </summary>
    /// <param name="req">查询参数，见 <see cref="DeleteAiModelCommand"/>（使用 Query 传参）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("delete_model")]
    public async Task<EmptyCommandResponse> DeleteModel([FromQuery] DeleteAiModelCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 管理员：查询 AI 模型列表（根据条件过滤，可包含 isPublic/provider/type）.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryAiModelListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAiModelListCommandResponse"/>，包含模型列表.</returns>
    [HttpPost("modellist")]
    public async Task<QueryAiModelListCommandResponse> QueryModelList([FromBody] QueryAiModelListCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 管理员：查询已添加的模型供应商与每类模型数量.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAiModelProviderListResponse"/>，包含供应商与类型统计.</returns>
    [HttpGet("providerlist")]
    public async Task<QueryAiModelProviderListResponse> QueryProviderList(CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(new QueryAiModelProviderListCommand(), ct);
    }

    /// <summary>
    /// 管理员：更新 AI 模型信息.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="UpdateAiModelCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_aimodel")]
    public async Task<EmptyCommandResponse> UpdateAiModel([FromBody] UpdateAiModelCommand req, CancellationToken ct = default)
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
