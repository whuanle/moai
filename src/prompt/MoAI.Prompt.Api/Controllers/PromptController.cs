using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Prompt.Models;
using MoAI.Prompt.Queries;
using MoAI.Prompt.Queries.Responses;
using MoAIPrompt.Models;
using MoAIPrompt.Queries;
using MoAIPrompt.Queries.Responses;

namespace MoAI.Prompt.Controllers;

/// <summary>
/// 提示词相关操作接口.
/// </summary>
[ApiController]
[Route("/prompt")]
[EndpointGroupName("prompt")]
public partial class PromptController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptController"/> class.
    /// </summary>
    /// <param name="mediator">用于发送 MediatR 请求的实例.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public PromptController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询提示词分类列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>查询结果, 返回 <see cref="QueryePromptClassCommandResponse"/>.</returns>
    [HttpGet("class_list")]
    public async Task<QueryePromptClassCommandResponse> QueryAsync(CancellationToken ct = default)
    {
        return await _mediator.Send(new QueryePromptClassCommand(), ct);
    }

    /// <summary>
    /// 使用 AI 优化提示词.
    /// </summary>
    /// <param name="req">请求对象, 包含 AiModelId 与 SourcePrompt.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAiOptimizePromptCommandResponse"/>.</returns>
    [HttpPost("ai_optmize_prompt")]
    public async Task<QueryAiOptimizePromptCommandResponse> AiOptimizePrompt([FromBody] Models.AiOptimizePromptCommand req, CancellationToken ct = default)
    {
        var newReq = new Queries.AiOptimizePromptCommand
        {
            AiModelId = req.AiModelId,
            SourcePrompt = req.SourcePrompt,
            UserId = _userContext.UserId
        };

        return await _mediator.Send(newReq, ct);
    }

    /// <summary>
    /// 创建提示词.
    /// </summary>
    /// <param name="req">创建提示词的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleInt"/>.</returns>
    [HttpPost("create_prompt")]
    public async Task<SimpleInt> CreatePrompt([FromBody] CreatePromptCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除提示词.
    /// </summary>
    /// <param name="req">包含要删除 PromptId 的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("delete_prompt")]
    public async Task<EmptyCommandResponse> DeletePrompt([FromBody] DeletePromptCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询提示词列表.
    /// </summary>
    /// <param name="req">查询命令对象，来自请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPromptListCommandResponse"/>.</returns>
    [HttpPost("prompt_list")]
    public async Task<QueryPromptListCommandResponse> QueryPrompList([FromBody] QueryPromptListCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取提示词内容.
    /// </summary>
    /// <param name="req">查询对象，来自查询字符串，包含 PromptId.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="PromptItem"/>.</returns>
    [HttpGet("prompt_content")]
    public async Task<PromptItem> QueryPromptContent([FromQuery] QueryPromptCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改提示词.
    /// </summary>
    /// <param name="req">包含要更新内容的命令对象.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_prompt")]
    public async Task<EmptyCommandResponse> UpdatePrompt([FromBody] UpdatePromptCommand req, CancellationToken ct = default)
    {
        if (req.IsPublic == true)
        {
            var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
            {
                ContextUserId = _userContext.UserId
            });

            if (!isAdmin.IsAdmin)
            {
                throw new BusinessException("只有管理员可以公开提示词.") { StatusCode = 403 };
            }
        }

        return await _mediator.Send(req, ct);
    }
}