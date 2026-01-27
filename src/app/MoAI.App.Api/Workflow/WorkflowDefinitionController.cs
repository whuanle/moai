using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Workflow.Commands;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Controllers;

/// <summary>
/// 工作流定义相关接口.
/// 提供工作流定义的创建、查询、更新和删除功能.
/// </summary>
[ApiController]
[Route("/api/workflow/definition")]
public partial class WorkflowDefinitionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowDefinitionController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者.</param>
    /// <param name="userContext">用户上下文.</param>
    public WorkflowDefinitionController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 创建工作流定义.
    /// 创建新的工作流定义，包含所有节点配置和连接信息.
    /// </summary>
    /// <param name="req">创建工作流定义命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回创建的工作流定义 ID.</returns>
    [HttpPost("create")]
    public async Task<SimpleGuid> CreateWorkflowDefinition([FromBody] CreateWorkflowDefinitionCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取工作流定义.
    /// 检索单个工作流定义的完整信息，包括所有节点配置和连接.
    /// </summary>
    /// <param name="id">工作流定义 ID.</param>
    /// <param name="includeDraft">是否包含草稿数据.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回工作流定义详情.</returns>
    [HttpGet("get")]
    public async Task<QueryWorkflowDefinitionCommandResponse> GetWorkflowDefinition(
        [FromQuery] Guid id,
        [FromQuery] bool includeDraft = false,
        CancellationToken ct = default)
    {
        return await _mediator.Send(
            new QueryWorkflowDefinitionCommand
            {
                Id = id,
                IncludeDraft = includeDraft
            },
            ct);
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
    public async Task<EmptyCommandResponse> UpdateWorkflowDefinition(
        [FromBody] UpdateWorkflowDefinitionCommand req,
        CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 发布工作流定义.
    /// 将草稿版本（UiDesignDraft、FunctionDesignDraft）发布为正式版本（UiDesign、FunctionDesign）.
    /// 发布时会验证工作流定义的有效性，并创建版本快照以维护历史记录.
    /// </summary>
    /// <param name="id">工作流定义 ID.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回空响应.</returns>
    [HttpPost("publish")]
    public async Task<EmptyCommandResponse> PublishWorkflowDefinition([FromQuery] Guid id, CancellationToken ct = default)
    {
        return await _mediator.Send(
            new PublishWorkflowDefinitionCommand
            {
                Id = id
            },
            ct);
    }

    /// <summary>
    /// 删除工作流定义.
    /// 执行软删除操作，保留工作流定义和执行历史以供审计.
    /// </summary>
    /// <param name="id">工作流定义 ID.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回空响应.</returns>
    [HttpDelete("delete")]
    public async Task<EmptyCommandResponse> DeleteWorkflowDefinition([FromQuery] Guid id, CancellationToken ct = default)
    {
        return await _mediator.Send(
            new DeleteWorkflowDefinitionCommand
            {
                Id = id
            },
            ct);
    }

    /// <summary>
    /// 获取工作流定义列表.
    /// 检索工作流定义的分页列表，支持按团队、名称等条件过滤.
    /// </summary>
    /// <param name="teamId">团队 ID（可选）.</param>
    /// <param name="keyword">名称关键字搜索（可选）.</param>
    /// <param name="pageIndex">页码，从 1 开始.</param>
    /// <param name="pageSize">每页数量.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回工作流定义分页列表.</returns>
    [HttpGet("list")]
    public async Task<QueryWorkflowDefinitionListCommandResponse> GetWorkflowDefinitionList(
        [FromQuery] int? teamId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return await _mediator.Send(
            new QueryWorkflowDefinitionListCommand
            {
                TeamId = teamId,
                Keyword = keyword,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            ct);
    }
}
