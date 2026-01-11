using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries;
using MoAI.AiModel.Queries.Respones;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Controllers;

/// <summary>
/// ai 模型.
/// </summary>
[ApiController]
[Route("/aimodel")]
[EndpointGroupName("aimodel")]
public partial class AiModelController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AiModelController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 公开：获取公开可用的 AI 模型列表（用户视图）.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryUserViewAiModelListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserViewAiModelListCommandResponse"/>，包含公开模型列表.</returns>
    [HttpPost("modellist")]
    public async Task<QueryUserViewAiModelListCommandResponse> QueryUserViewAiModelList([FromBody] QueryUserViewAiModelListCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询模型的属性.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryUserViewAiModelListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserViewAiModelListCommandResponse"/>，包含公开模型列表.</returns>
    [HttpPost("model")]
    public async Task<PublicModelInfo> QueryPublicModelList([FromBody] QueryAiModelPublicModelInfoCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }
}