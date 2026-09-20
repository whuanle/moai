using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Commands;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Instance;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.App.Workflow.Stores;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="DebugRunAppWorkflowCommand"/>
/// </summary>
public class DebugRunAppWorkflowCommandHandler : IRequestHandler<DebugRunAppWorkflowCommand, DebugRunAppWorkflowResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly DatabaseWorkflowDefinitionStore _definitionStore;
    private readonly WorkflowEngine _workflowEngine;
    private readonly Services.WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebugRunAppWorkflowCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="definitionStore">工作流定义存储.</param>
    /// <param name="workflowEngine">工作流引擎.</param>
    /// <param name="executionContext">工作流执行上下文.</param>
    public DebugRunAppWorkflowCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        DatabaseWorkflowDefinitionStore definitionStore,
        WorkflowEngine workflowEngine,
        Services.WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _definitionStore = definitionStore;
        _workflowEngine = workflowEngine;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task<DebugRunAppWorkflowResponse> Handle(DebugRunAppWorkflowCommand request, CancellationToken cancellationToken)
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
            throw new BusinessException("只有团队管理员可以调试流程编排.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("只有流程应用支持调试执行.") { StatusCode = 400 };
        }

        _executionContext.TeamId = app.TeamId;
        _executionContext.AppId = request.AppId;
        _executionContext.UserId = request.ContextUserId;
        _executionContext.IsDebug = true;

        // 随调试隐式保存最新草稿（设计器"边改边试"），或使用已保存草稿
        WorkflowDefinition definition;
        if (!string.IsNullOrWhiteSpace(request.Definition))
        {
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
            await _definitionStore.SaveDefinitionAsync(definition, cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.EditorData))
            {
                await _definitionStore.SaveEditorDataAsync(request.AppId, request.EditorData, cancellationToken);
            }
        }
        else
        {
            definition = await _definitionStore.FindDefinitionByIdAsync(request.AppId.ToString(), cancellationToken)
                ?? throw new BusinessException("请先保存流程编排草稿再调试.") { StatusCode = 404 };
        }

        JsonObject input;
        try
        {
            input = JsonNode.Parse(request.InputJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            throw new BusinessException($"启动参数 JSON 无效：{ex.Message}") { StatusCode = 400 };
        }

        // 旧编排兼容：与 WorkflowAppChatInvoker 一致，question 存在时补 query 镜像，绑定 start.query 的历史流程在调试中同样可解析
        if (input.ContainsKey("question") && !input.ContainsKey("query"))
        {
            input["query"] = input["question"]!.DeepClone();
        }

        JsonObject systemVariables;
        try
        {
            systemVariables = JsonNode.Parse(request.SystemJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            throw new BusinessException($"全局变量 JSON 无效：{ex.Message}") { StatusCode = 400 };
        }

        // 知识库检索节点引用的知识库必须属于本团队（调试执行直接以当前定义跑，保存/发布之外的唯一入口）
        await Services.KnowledgeSearchWikiGuard.EnsureWikisBelongToTeamAsync(_databaseContext, app.TeamId, definition, cancellationToken);

        // Agent 应用节点不得与当前流程构成循环嵌套（运行前拦截，运行期节点调用另有防线）
        await Services.AgentWorkflowCycleGuard.EnsureNoCycleAsync(_databaseContext, request.AppId, Services.AgentWorkflowCycleGuard.CollectAgentAppIds(definition), cancellationToken);

        // 调试注入 sys.* 对话上下文：用户/应用为当前真实值，对话维度无会话故留空，保证设计器引用可解析
        var systemContext = new JsonObject
        {
            ["userId"] = request.ContextUserId.ToString(),
            ["appId"] = request.AppId.ToString(),
            ["conversationId"] = string.Empty,
            ["messageId"] = string.Empty,
            ["history"] = new JsonArray(),
        };

        // 同步执行到终态；节点失败时实例为挂起态并携带错误信息，不抛异常
        // 根流程标记：Agent 节点嵌套调用流程工具时环检测以此为准
        WorkflowInstance instance;
        using (Services.WorkflowRootContext.Begin(request.AppId))
        {
            instance = await _workflowEngine.StartWithDefinitionAsync(
                definition,
                input,
                systemVariables,
                systemContext,
                Guid.CreateVersion7().ToString("N"),
                cancellationToken);
        }

        try
        {
            return MapResponse(instance);
        }
        finally
        {
            _executionContext.IsDebug = false;
        }
    }

    private static DebugRunAppWorkflowResponse MapResponse(WorkflowInstance instance)
    {
        return new DebugRunAppWorkflowResponse
        {
            InstanceId = Guid.ParseExact(instance.Id, "N"),
            Status = instance.Status.ToString().ToLowerInvariant(),
            DefinitionVersion = instance.DefinitionVersion,
            Output = instance.Output?.ToJsonString(),
            ErrorMessage = instance.ErrorMessage,
            StartedAt = instance.StartedAt,
            EndedAt = instance.EndedAt,
            Nodes = instance.NodeStates.Values
                .OrderBy(n => n.StartedAt ?? DateTimeOffset.MaxValue)
                .Select(n => new WorkflowNodeExecution
                {
                    NodeKey = n.NodeKey,
                    NodeType = n.NodeType,
                    NodeName = n.NodeName,
                    State = n.State.ToString().ToLowerInvariant(),
                    Input = n.Input?.ToJsonString(),
                    Output = n.Output?.ToJsonString(),
                    ErrorMessage = n.ErrorMessage,
                    Attempts = n.Attempts,
                    StartedAt = n.StartedAt,
                    EndedAt = n.EndedAt,
                })
                .ToList(),
        };
    }
}
