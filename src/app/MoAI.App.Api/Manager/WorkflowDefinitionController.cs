using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;
using MoAI.Workflow.Commands;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.App.Manager;

/// <summary>
/// 工作流定义相关接口.
/// 提供工作流定义的创建、查询、更新和删除功能.
/// </summary>
[ApiController]
[Route("/api/team/workflowapp")]
[EndpointGroupName("manage_workflowapp")]
public partial class WorkflowDefinitionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;
    private readonly ILogger<WorkflowDefinitionController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowDefinitionController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者.</param>
    /// <param name="userContext">用户上下文.</param>
    /// <param name="logger">日志记录器.</param>
    public WorkflowDefinitionController(
        IMediator mediator,
        UserContext userContext,
        ILogger<WorkflowDefinitionController> logger)
    {
        _mediator = mediator;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// 获取工作流定义.
    /// 检索单个工作流定义的完整信息，包括所有节点配置和连接.
    /// </summary>
    /// <param name="req">工作流定义.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回工作流定义详情.</returns>
    [HttpPost("config")]
    public async Task<QueryWorkflowDefinitionCommandResponse> GetWorkflowDefinition([FromBody] QueryWorkflowDefinitionCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新工作流定义.
    /// 更新现有工作流定义的配置信息，包括节点、连接和元数据.
    /// 更新时会创建版本快照以维护历史记录.
    /// </summary>
    /// <param name="req">更新工作流定义命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回空响应.</returns>
    [HttpPut("update")]
    public async Task<EmptyCommandResponse> UpdateWorkflowDefinition([FromBody] UpdateWorkflowDefinitionCommand req, CancellationToken ct = default)
    {
        await CheckIsAdminAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    ///// <summary>
    ///// 发布工作流定义.
    ///// 将草稿版本（UiDesignDraft、FunctionDesignDraft）发布为正式版本（UiDesign、FunctionDesign）.
    ///// 发布时会验证工作流定义的有效性，并创建版本快照以维护历史记录.
    ///// </summary>
    ///// <param name="id">工作流定义 ID.</param>
    ///// <param name="ct">取消令牌.</param>
    ///// <returns>返回空响应.</returns>
    //[HttpPost("publish")]
    //public async Task<EmptyCommandResponse> PublishWorkflowDefinition([FromQuery] Guid id, CancellationToken ct = default)
    //{
    //    return await _mediator.Send(
    //        new PublishWorkflowDefinitionCommand
    //        {
    //            Id = id
    //        },
    //        ct);
    //}

    /// <summary>
    /// 获取工作流定义列表.
    /// 检索工作流定义的分页列表，支持按团队、名称等条件过滤.
    /// </summary>
    /// <param name="req">.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回工作流定义分页列表.</returns>
    [HttpPost("list")]
    public async Task<QueryWorkflowDefinitionListCommandResponse> GetWorkflowDefinitionList([FromBody] QueryWorkflowDefinitionListCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 调试执行工作流.
    /// 在设计工作流时执行调试，通过服务器发送事件（SSE）流式传输实时返回节点执行结果.
    /// </summary>
    /// <param name="command">执行工作流命令.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>无返回值，通过 SSE 流式传输结果.</returns>
    [HttpPost("debug")]
    [Produces("text/event-stream")]
    [ProducesDefaultResponseType(typeof(WorkflowProcessingItem))]
    public async Task DebugExecute([FromBody] ExecuteWorkflowCommand command, CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var item in _mediator.CreateStream(command, cancellationToken))
            {
                await WriteSseDataAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Workflow debug execution cancelled. WorkflowDefinitionId: {WorkflowDefinitionId}",
                command.WorkflowDefinitionId);
            await WriteErrorAsync("工作流调试执行已被中止", cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(
                ex,
                "Business failure in workflow debug execution. WorkflowDefinitionId: {WorkflowDefinitionId}",
                command.WorkflowDefinitionId);
            await WriteErrorAsync(ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected workflow debug execution failure. WorkflowDefinitionId: {WorkflowDefinitionId}",
                command.WorkflowDefinitionId);
            await WriteErrorAsync("工作流调试执行失败，请稍后再试。", cancellationToken);
        }
    }

    private async Task WriteSseDataAsync(object payload, CancellationToken cancellationToken)
    {
        if (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        await HttpContext.Response
            .WriteAsync($"data: {payload.ToJsonString()}\n\n", cancellationToken);

        await Response.Body.FlushAsync(cancellationToken);
    }

    private Task WriteErrorAsync(string message, CancellationToken cancellationToken)
    {
        var errorPayload = new WorkflowProcessingItem
        {
            NodeType = "Error",
            NodeKey = "error",
            State = NodeState.Failed,
            ErrorMessage = message,
            ExecutedTime = DateTimeOffset.Now
        };

        return WriteSseDataAsync(errorPayload, cancellationToken);
    }

    private async Task CheckIsAdminAsync(int teamId, CancellationToken ct)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = teamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < TeamRole.Admin)
        {
            throw new BusinessException("没有操作权限");
        }
    }
}
