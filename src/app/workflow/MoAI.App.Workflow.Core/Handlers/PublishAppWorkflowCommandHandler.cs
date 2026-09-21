using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Commands;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Stores;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="PublishAppWorkflowCommand"/>
/// </summary>
public class PublishAppWorkflowCommandHandler : IRequestHandler<PublishAppWorkflowCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly DatabaseWorkflowDefinitionStore _definitionStore;
    private readonly WorkflowCompiler _workflowCompiler;
    private readonly Services.WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishAppWorkflowCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="definitionStore">工作流定义存储.</param>
    /// <param name="workflowCompiler">工作流编译器（含图结构全量校验）.</param>
    /// <param name="executionContext">工作流执行上下文.</param>
    public PublishAppWorkflowCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        DatabaseWorkflowDefinitionStore definitionStore,
        WorkflowCompiler workflowCompiler,
        Services.WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _definitionStore = definitionStore;
        _workflowCompiler = workflowCompiler;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(PublishAppWorkflowCommand request, CancellationToken cancellationToken)
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
            throw new BusinessException("只有团队管理员可以发布流程编排.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("只有流程应用支持发布编排.") { StatusCode = 400 };
        }

        var config = await _definitionStore.FindConfigEntityAsync(request.AppId, cancellationToken);
        if (config == null || string.IsNullOrWhiteSpace(config.DraftDefinition))
        {
            throw new BusinessException("请先保存流程编排草稿再发布.") { StatusCode = 404 };
        }

        var definition = WorkflowJson.DeserializeDefinition(config.DraftDefinition);
        definition.Id = request.AppId.ToString();

        // 发布前全量校验（单 Start/连通性/无环/条件分支完整/变量仅引用上游），失败即拒绝发布
        try
        {
            _workflowCompiler.Compile(definition);
        }
        catch (WorkflowValidationException ex)
        {
            throw new BusinessException(ex.Message) { StatusCode = 400 };
        }

        // 知识库检索节点引用的知识库必须属于本团队
        await Services.KnowledgeSearchWikiGuard.EnsureWikisBelongToTeamAsync(_databaseContext, app.TeamId, definition, cancellationToken);

        // Agent 应用节点不得与当前流程构成循环嵌套（按发布后的工具闭包判定）
        await Services.AgentWorkflowCycleGuard.EnsureNoCycleAsync(_databaseContext, request.AppId, Services.AgentWorkflowCycleGuard.CollectAgentAppIds(definition), cancellationToken);

        _executionContext.TeamId = app.TeamId;
        await _definitionStore.PublishAsync(request.AppId.ToString(), cancellationToken);

        // 开场白随发布快照：流程应用的开场白存于 app_agent_config，正式会话/详情按发布快照下发
        var agentConfig = await _databaseContext.AppAgentConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        if (agentConfig == null)
        {
            agentConfig = new AppAgentConfigEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = app.TeamId,
                AppId = app.Id,
                Prompt = string.Empty,
                WikiIds = "[]",
                Plugins = "[]",
                Skills = "[]",
                ExecutionSettings = "{}",
                OpeningStatement = string.Empty,
            };
            _databaseContext.AppAgentConfigs.Add(agentConfig);
        }

        agentConfig.PublishedConfig = AppAgentConfigSnapshot.Serialize(agentConfig);
        agentConfig.Status = 1;

        if (app.PublishStatus != 1)
        {
            app.PublishStatus = 1;
        }

        app.PublishTime = DateTimeOffset.Now;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
