using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Models;
using MoAI.Team.Queries;
using MoAI.Workflow.Commands;
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
    /// 查询节点定义.
    /// 用于获取指定节点类型的定义信息，包括输入输出字段、参数要求等.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点定义信息.</returns>
    [HttpPost("query_define")]
    public async Task<QueryNodeDefineCommandResponse> QueryNodeDefine([FromBody] QueryNodeDefineCommand req, CancellationToken ct = default)
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
