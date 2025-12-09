using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Prompt.Queries;
using MoAI.Prompt.Queries.Responses;
using MoAIPrompt.Api;

namespace MoAI.Prompt.Controllers;

/// <summary>
/// 提示词分类.
/// </summary>
[Route("/admin/promptclassify")]
[ApiController]
public class PromptClassController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptClassController"/> class.
    /// 初始化新的 <see cref="PromptClassController"/> 实例.
    /// </summary>
    /// <param name="mediator">MediatR 发行者.</param>
    /// <param name="userContext">当前用户上下文.</param>
    public PromptClassController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 创建提示词分类.
    /// </summary>
    /// <param name="req">创建提示词分类命令请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>创建结果, 返回 <see cref="SimpleInt"/>.</returns>
    [HttpPost("create_class")]
    public async Task<SimpleInt> CreateAsync(CreatePromptClassifyCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除提示词分类.
    /// </summary>
    /// <param name="req">删除提示词命令请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>删除结果, 返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("delete_class")]
    public async Task<EmptyCommandResponse> DeleteAsync(DeletePromptCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新提示词分类.
    /// </summary>
    /// <param name="req">更新提示词分类命令请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>更新结果, 返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("update_class")]
    public async Task<EmptyCommandResponse> UpdateAsync(UpdatePromptClassifyCommand req, CancellationToken ct = default)
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