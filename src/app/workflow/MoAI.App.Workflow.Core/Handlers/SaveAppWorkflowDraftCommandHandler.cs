using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Commands;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Stores;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppWorkflowDraftCommand"/>
/// </summary>
public class SaveAppWorkflowDraftCommandHandler : IRequestHandler<SaveAppWorkflowDraftCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly DatabaseWorkflowDefinitionStore _definitionStore;
    private readonly Services.WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppWorkflowDraftCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="definitionStore">工作流定义存储.</param>
    /// <param name="executionContext">工作流执行上下文.</param>
    public SaveAppWorkflowDraftCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        DatabaseWorkflowDefinitionStore definitionStore,
        Services.WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _definitionStore = definitionStore;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveAppWorkflowDraftCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以编辑流程编排.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("只有流程应用支持编排设计.") { StatusCode = 400 };
        }

        // 定义契约校验 + 归属校正：定义 id 即应用 id
        WorkflowDefinition definition;
        try
        {
            definition = WorkflowJson.DeserializeDefinition(request.Definition);
        }
        catch (JsonException ex)
        {
            throw new BusinessException($"流程定义 JSON 无效：{ex.Message}") { StatusCode = 400 };
        }

        definition.Id = request.AppId.ToString();
        definition.Status = DefinitionStatus.Draft;

        // 核心节点不变量：开始节点恰好一个、结束节点至少一个（设计器加载侧会自愈补齐，这里在保存入口拦住违反契约的定义）
        if (definition.Nodes.Count == 0)
        {
            throw new BusinessException("流程必须包含至少一个节点.") { StatusCode = 400 };
        }

        var startCount = definition.Nodes.Count(n => n.Type == NodeTypes.Start);
        if (startCount != 1)
        {
            throw new BusinessException($"流程必须恰好包含一个开始节点，当前 {startCount} 个.") { StatusCode = 400 };
        }

        if (definition.Nodes.All(n => n.Type != NodeTypes.End))
        {
            throw new BusinessException("流程必须包含至少一个结束节点.") { StatusCode = 400 };
        }

        // 知识库检索节点引用的知识库必须属于本团队
        await Services.KnowledgeSearchWikiGuard.EnsureWikisBelongToTeamAsync(_databaseContext, app.TeamId, definition, cancellationToken);

        // 知识图谱检索节点引用的知识图谱必须属于本团队（托管）
        await Services.KnowledgeGraphSearchGuard.EnsureGraphsBelongToTeamAsync(_databaseContext, app.TeamId, definition, cancellationToken);

        // Agent 应用节点不得与当前流程构成循环嵌套（流程 → Agent 节点 → Agent 流程工具 → 本流程）
        await Services.AgentWorkflowCycleGuard.EnsureNoCycleAsync(_databaseContext, request.AppId, Services.AgentWorkflowCycleGuard.CollectAgentAppIds(definition), cancellationToken);

        _executionContext.TeamId = app.TeamId;
        await _definitionStore.SaveDefinitionAsync(definition, cancellationToken);
        await _definitionStore.SaveEditorDataAsync(request.AppId, request.EditorData, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
