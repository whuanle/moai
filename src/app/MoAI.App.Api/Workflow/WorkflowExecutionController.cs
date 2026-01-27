using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Workflow.Commands;
using MoAI.Workflow.Models;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Controllers;

/// <summary>
/// 工作流执行相关接口.
/// 提供工作流执行和执行历史查询功能.
/// </summary>
[ApiController]
[Route("/api/workflow")]
[EndpointGroupName("workflow")]
public partial class WorkflowExecutionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;
    private readonly ILogger<WorkflowExecutionController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowExecutionController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者.</param>
    /// <param name="userContext">用户上下文.</param>
    /// <param name="logger">日志记录器.</param>
    public WorkflowExecutionController(
        IMediator mediator,
        UserContext userContext,
        ILogger<WorkflowExecutionController> logger)
    {
        _mediator = mediator;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// 执行工作流.
    /// 执行指定的工作流定义，通过服务器发送事件（SSE）流式传输实时返回节点执行结果.
    /// </summary>
    /// <param name="command">执行工作流命令.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>无返回值，通过 SSE 流式传输结果.</returns>
    [HttpPost("execute")]
    [Produces("text/event-stream")]
    [ProducesDefaultResponseType(typeof(WorkflowProcessingItem))]
    public async Task Execute([FromBody] ExecuteWorkflowCommand command, CancellationToken cancellationToken = default)
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
                "Workflow execution cancelled. WorkflowDefinitionId: {WorkflowDefinitionId}",
                command.WorkflowDefinitionId);
            await WriteErrorAsync("工作流执行已被中止", cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(
                ex,
                "Business failure in workflow execution. WorkflowDefinitionId: {WorkflowDefinitionId}",
                command.WorkflowDefinitionId);
            await WriteErrorAsync(ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected workflow execution failure. WorkflowDefinitionId: {WorkflowDefinitionId}",
                command.WorkflowDefinitionId);
            await WriteErrorAsync("工作流执行失败，请稍后再试。", cancellationToken);
        }
    }

    /// <summary>
    /// 获取工作流实例.
    /// 检索单个工作流实例的执行信息，包括执行状态、参数和结果.
    /// </summary>
    /// <param name="id">工作流实例 ID（执行历史记录 ID）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回工作流实例详情.</returns>
    [HttpGet("instance/{id}")]
    public async Task<QueryWorkflowInstanceCommandResponse> GetInstance(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        return await _mediator.Send(
            new QueryWorkflowInstanceCommand
            {
                Id = id
            },
            ct);
    }

    /// <summary>
    /// 获取工作流实例列表.
    /// 检索工作流实例的分页列表，支持按工作流定义、团队、状态等条件过滤.
    /// </summary>
    /// <param name="workflowDesignId">工作流定义 ID（可选）.</param>
    /// <param name="teamId">团队 ID（可选）.</param>
    /// <param name="state">执行状态（可选）.</param>
    /// <param name="pageIndex">页码，从 1 开始.</param>
    /// <param name="pageSize">每页数量.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回工作流实例分页列表.</returns>
    [HttpGet("instance")]
    public async Task<QueryWorkflowInstanceListCommandResponse> ListInstances(
        [FromQuery] Guid? workflowDesignId = null,
        [FromQuery] int? teamId = null,
        [FromQuery] int? state = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _mediator.Send(
            new QueryWorkflowInstanceListCommand
            {
                WorkflowDesignId = workflowDesignId,
                TeamId = teamId,
                State = state,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            ct);
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
            State = Enums.NodeState.Failed,
            ErrorMessage = message,
            ExecutedTime = DateTimeOffset.UtcNow
        };

        return WriteSseDataAsync(errorPayload, cancellationToken);
    }
}
