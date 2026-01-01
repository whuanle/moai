using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
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
    /// 使用 AI 优化提示词.
    /// </summary>
    /// <param name="req">请求对象, 包含 AiModelId 与 SourcePrompt.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAiOptimizePromptCommandResponse"/>.</returns>
    [HttpPost("ai_optmize_prompt")]
    public async Task<QueryAiOptimizePromptCommandResponse> AiOptimizePrompt([FromBody] AiOptimizePromptRequest req, CancellationToken ct = default)
    {
        var newReq = new AiOptimizePromptCommand
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
        await CheckIsAdminAsync();

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
        await CheckIsAdminAsync();

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
    public async Task<PromptItem> QueryPromptContent([FromQuery] QueryPromptContentRequest req, CancellationToken ct = default)
    {
        var newReq = new QueryPromptCommand
        {
            PromptId = req.PromptId,
            UserId = _userContext.UserId
        };

        return await _mediator.Send(newReq, ct);
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
        await CheckIsAdminAsync();

        return await _mediator.Send(req, ct);
    }

    private async Task CheckIsAdminAsync()
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}