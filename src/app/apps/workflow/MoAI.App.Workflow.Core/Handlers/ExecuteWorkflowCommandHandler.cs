//using System.Runtime.CompilerServices;
//using System.Text.Json;
//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using MoAI.Database;
//using MoAI.Database.Entities;
//using MoAI.Infra.Exceptions;
//using MoAI.Workflow.Commands;
//using MoAI.Workflow.Enums;
//using MoAI.Workflow.Models;
//using MoAI.Workflow.Runtime;

//namespace MoAI.Workflow.Handlers;

///// <summary>
///// <inheritdoc cref="ExecuteWorkflowCommand"/>
///// </summary>
//public class ExecuteWorkflowCommandHandler : IStreamRequestHandler<ExecuteWorkflowCommand, WorkflowProcessingItem>
//{
//    private readonly WorkflowRuntime _workflowRuntime;
//    private readonly DatabaseContext _databaseContext;
//    private readonly ILogger<ExecuteWorkflowCommandHandler> _logger;

//    /// <summary>
//    /// Initializes a new instance of the <see cref="ExecuteWorkflowCommandHandler"/> class.
//    /// </summary>
//    /// <param name="workflowRuntime">工作流运行时.</param>
//    /// <param name="databaseContext">数据库上下文.</param>
//    /// <param name="logger">日志记录器.</param>
//    public ExecuteWorkflowCommandHandler(
//        WorkflowRuntime workflowRuntime,
//        DatabaseContext databaseContext,
//        ILogger<ExecuteWorkflowCommandHandler> logger)
//    {
//        _workflowRuntime = workflowRuntime;
//        _databaseContext = databaseContext;
//        _logger = logger;
//    }

//    /// <inheritdoc/>
//    public async IAsyncEnumerable<WorkflowProcessingItem> Handle(
//        ExecuteWorkflowCommand request,
//        [EnumeratorCancellation] CancellationToken cancellationToken)
//    {
//        // 1. 检索工作流定义
//        var workflowDesign = await _databaseContext.WorkflowDesigns
//            .Where(w => w.Id == request.WorkflowDefinitionId && w.IsDeleted == 0)
//            .FirstOrDefaultAsync(cancellationToken);

//        if (workflowDesign == null)
//        {
//            throw new BusinessException($"工作流定义 {request.WorkflowDefinitionId} 不存在") { StatusCode = 404 };
//        }

//        // 2. 反序列化工作流定义
//        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(
//            workflowDesign.FunctionDesgin,
//            new JsonSerializerOptions
//            {
//                PropertyNameCaseInsensitive = true
//            });

//        if (definition == null)
//        {
//            throw new BusinessException(400, "工作流定义反序列化失败");
//        }

//        // 3. 创建工作流实例记录
//        var instanceId = Guid.NewGuid();
//        var workflowHistory = new WorkflowHistoryEntity
//        {
//            Id = instanceId,
//            TeamId = request.TeamId,
//            WorkflowDesignId = request.WorkflowDefinitionId,
//            State = (int)NodeState.Running,
//            SystemParamters = JsonSerializer.Serialize(request.SystemVariables ?? new Dictionary<string, object>()),
//            RunParamters = JsonSerializer.Serialize(request.StartupParameters),
//            Data = string.Empty // 初始为空，执行完成后更新
//        };

//        _databaseContext.WorkflowHistories.Add(workflowHistory);
//        await _databaseContext.SaveChangesAsync(cancellationToken);

//        _logger.LogInformation(
//            "创建工作流实例，实例 ID: {InstanceId}, 设计 ID: {WorkflowDesignId}, 团队 ID: {TeamId}",
//            instanceId,
//            request.WorkflowDefinitionId,
//            request.TeamId);

//        // 4. 执行工作流并收集执行历史
//        var executionHistory = new List<WorkflowProcessingItem>();
//        NodeState finalState = NodeState.Completed;
//        string? finalErrorMessage = null;

//        _logger.LogInformation(
//            "开始执行工作流，实例 ID: {InstanceId}, 设计 ID: {WorkflowDesignId}",
//            instanceId,
//            request.WorkflowDefinitionId);

//        // 流式执行工作流
//        await foreach (var item in ExecuteWorkflowAsync(
//            definition,
//            request.StartupParameters,
//            instanceId,
//            request.WorkflowDefinitionId,
//            request.SystemVariables,
//            cancellationToken))
//        {
//            // 收集执行历史
//            executionHistory.Add(item);

//            // 检查是否有失败的节点
//            if (item.State == NodeState.Failed)
//            {
//                finalState = NodeState.Failed;
//                finalErrorMessage = item.ErrorMessage;
//            }

//            yield return item;
//        }

//        _logger.LogInformation(
//            "工作流执行完成，实例 ID: {InstanceId}, 共执行 {Count} 个节点",
//            instanceId,
//            executionHistory.Count);

//        // 5. 更新执行历史到数据库
//        await UpdateExecutionHistoryAsync(
//            instanceId,
//            executionHistory,
//            finalState,
//            finalErrorMessage,
//            cancellationToken);
//    }

//    /// <summary>
//    /// 执行工作流并流式传输结果.
//    /// </summary>
//    private async IAsyncEnumerable<WorkflowProcessingItem> ExecuteWorkflowAsync(
//        WorkflowDefinition workflowDefinition,
//        Dictionary<string, object> startupParameters,
//        Guid instanceId,
//        Guid workflowDesignId,
//        Dictionary<string, object>? systemVariables,
//        [EnumeratorCancellation] CancellationToken cancellationToken)
//    {
//        var items = new List<WorkflowProcessingItem>();
//        WorkflowProcessingItem? errorItem = null;

//        try
//        {
//            await foreach (var item in _workflowRuntime.ExecuteAsync(
//                workflowDefinition,
//                startupParameters,
//                instanceId.ToString(),
//                workflowDesignId.ToString(),
//                systemVariables,
//                cancellationToken))
//            {
//                items.Add(item);

//                _logger.LogDebug(
//                    "节点执行完成: {NodeKey}, 类型: {NodeType}, 状态: {State}",
//                    item.NodeKey,
//                    item.NodeType,
//                    item.State);
//            }
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(
//                ex,
//                "工作流执行异常，实例 ID: {InstanceId}, 错误: {Message}",
//                instanceId,
//                ex.Message);

//            // 创建错误处理项
//            errorItem = new WorkflowProcessingItem
//            {
//                NodeType = "Error",
//                NodeKey = "workflow_error",
//                Input = new Dictionary<string, object>(),
//                Output = new Dictionary<string, object>(),
//                State = NodeState.Failed,
//                ErrorMessage = $"工作流执行异常: {ex.Message}",
//                ExecutedTime = DateTimeOffset.UtcNow
//            };
//        }

//        // 流式传输所有收集的项
//        foreach (var item in items)
//        {
//            yield return item;
//        }

//        // 如果有错误项，在最后返回
//        if (errorItem != null)
//        {
//            yield return errorItem;
//        }
//    }

//    /// <summary>
//    /// 更新工作流执行历史到数据库.
//    /// </summary>
//    /// <param name="instanceId">工作流实例 ID.</param>
//    /// <param name="executionHistory">执行历史列表.</param>
//    /// <param name="finalState">最终状态.</param>
//    /// <param name="finalErrorMessage">最终错误消息（可选）.</param>
//    /// <param name="cancellationToken">取消令牌.</param>
//    private async Task UpdateExecutionHistoryAsync(
//        Guid instanceId,
//        List<WorkflowProcessingItem> executionHistory,
//        NodeState finalState,
//        string? finalErrorMessage,
//        CancellationToken cancellationToken)
//    {
//        try
//        {
//            // 查找工作流历史记录
//            var workflowHistory = await _databaseContext.WorkflowHistories
//                .Where(w => w.Id == instanceId)
//                .FirstOrDefaultAsync(cancellationToken);

//            if (workflowHistory != null)
//            {
//                // 更新执行历史数据
//                workflowHistory.State = (int)finalState;
//                workflowHistory.Data = JsonSerializer.Serialize(new
//                {
//                    ExecutionHistory = executionHistory,
//                    FinalState = finalState.ToString(),
//                    FinalErrorMessage = finalErrorMessage ?? string.Empty,
//                    CompletedTime = DateTimeOffset.UtcNow
//                });

//                await _databaseContext.SaveChangesAsync(cancellationToken);

//                _logger.LogInformation(
//                    "更新工作流执行历史，实例 ID: {InstanceId}, 最终状态: {FinalState}, 节点数: {Count}",
//                    instanceId,
//                    finalState,
//                    executionHistory.Count);
//            }
//        }
//        catch (Exception ex)
//        {
//            // 记录错误但不抛出异常，避免影响工作流执行结果的返回
//            _logger.LogError(
//                ex,
//                "更新工作流执行历史失败，实例 ID: {InstanceId}, 错误: {Message}",
//                instanceId,
//                ex.Message);
//        }
//    }
//}
