using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.AIAssistant.Commands;
using MoAI.App.AIAssistant.Commands.Responses;
using MoAI.App.AIAssistant.Handlers;
using MoAI.App.AIAssistant.Queries;
using MoAI.App.AIAssistant.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAIChat.Core.Handlers;

namespace MoAI.App.AIAssistant.Controllers;

/// <summary>
/// 聊天.
/// </summary>
[ApiController]
[Route("/app/assistant")]
public partial class AiAssistantController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiAssistantController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public AiAssistantController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 发起新的聊天，检查用户是否有知识库、插件等权限，如果检查通过，返回聊天 id
    /// </summary>
    /// <param name="req">聊天对象，包含模型、提示、插件、标题等信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="CreateAiAssistantChatCommandResponse"/>，包含新建聊天的 Id 等信息</returns>
    [HttpPost("create_chat")]
    public async Task<CreateAiAssistantChatCommandResponse> CreateChat([FromBody] CreateAiAssistantChatCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除对话
    /// </summary>
    /// <param name="req">包含要删除的 ChatId 的命令对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/></returns>
    [HttpDelete("delete_chat")]
    public async Task<EmptyCommandResponse> DeleteChat([FromBody] DeleteAiAssistantChatCommand req, CancellationToken ct = default)
    {
        var creatorId = await _mediator.Send(
            new QueryAiAssistantCreatorCommand
            {
                ChatId = req.ChatId
            },
            ct);

        if (creatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到对话") { StatusCode = 404 };
        }

        return await _mediator.Send(
            new DeleteAiAssistantChatCommand
            {
                ChatId = req.ChatId
            },
            ct);
    }

    /// <summary>
    /// 删除对话中的一条记录
    /// </summary>
    /// <param name="req">包含 ChatId 与 RecordId 的命令对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/></returns>
    [HttpDelete("delete_chat_record")]
    public async Task<EmptyCommandResponse> DeleteChatRecord([FromBody] DeleteAiAssistantChatOneRecordCommand req, CancellationToken ct = default)
    {
        var creatorId = await _mediator.Send(
            new QueryAiAssistantCreatorCommand
            {
                ChatId = req.ChatId
            },
            ct);

        if (creatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到对话") { StatusCode = 404 };
        }

        return await _mediator.Send(
            new DeleteAiAssistantChatOneRecordCommand
            {
                ChatId = req.ChatId,
                RecordId = req.RecordId
            },
            ct);
    }

    /// <summary>
    /// 获取话题详细内容，即对话历史记录
    /// </summary>
    /// <param name="req">包含 ChatId 的查询命令对象（来自查询字符串）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="QueryAiAssistantChatHistoryCommandResponse"/>，包含话题历史记录明细</returns>
    [HttpGet("chat_history")]
    public async Task<QueryAiAssistantChatHistoryCommandResponse> QueryChatHistory([FromQuery] QueryUserViewAiAssistantChatHistoryCommand req, CancellationToken ct = default)
    {
        var creatorId = await _mediator.Send(
            new QueryAiAssistantCreatorCommand
            {
                ChatId = req.ChatId
            },
            ct);

        if (creatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到对话") { StatusCode = 404 };
        }

        return await _mediator.Send(
            new QueryUserViewAiAssistantChatHistoryCommand
            {
                ChatId = req.ChatId
            },
            ct);
    }

    /// <summary>
    /// 获取用户所有话题记录
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="QueryAiAssistantChatTopicListCommandResponse"/>，包含用户所有话题列表</returns>
    [HttpGet("topic_list")]
    public async Task<QueryAiAssistantChatTopicListCommandResponse> QueryTopicList(CancellationToken ct = default)
    {
        return await _mediator.Send(
            new QueryUserViewAiAssistantChatTopicListCommand
            {
            },
            ct);
    }

    /// <summary>
    /// 更新聊天参数
    /// </summary>
    /// <param name="req">包含 ChatId 与要更新的配置的命令对象</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/></returns>
    [HttpPost("update_chat")]
    public async Task<EmptyCommandResponse> UpdateChatConfig([FromBody] UpdateAiAssistanChatConfigCommand req, CancellationToken ct = default)
    {
        var creatorId = await _mediator.Send(
            new QueryAiAssistantCreatorCommand
            {
                ChatId = req.ChatId
            },
            ct);

        if (creatorId != _userContext.UserId)
        {
            throw new BusinessException("未找到对话") { StatusCode = 404 };
        }

        return await _mediator.Send(req, ct);
    }
}