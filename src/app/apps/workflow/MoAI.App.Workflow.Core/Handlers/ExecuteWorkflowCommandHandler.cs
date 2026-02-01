using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Workflow.Commands;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Runtime;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MoAI.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="ExecuteWorkflowCommand"/>
/// </summary>
public class ExecuteWorkflowCommandHandler : IStreamRequestHandler<ExecuteWorkflowCommand, WorkflowProcessingItem>
{
    private readonly WorkflowRuntime _workflowRuntime;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<ExecuteWorkflowCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecuteWorkflowCommandHandler"/> class.
    /// </summary>
    /// <param name="workflowRuntime">工作流运行时.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="logger">日志记录器.</param>
    public ExecuteWorkflowCommandHandler(
        WorkflowRuntime workflowRuntime,
        DatabaseContext databaseContext,
        ILogger<ExecuteWorkflowCommandHandler> logger)
    {
        _workflowRuntime = workflowRuntime;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowProcessingItem> Handle(
        ExecuteWorkflowCommand request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 1. 检索工作流定义
        var workflowDesign = await _databaseContext.AppWorkflowDesigns
            .Where(w => w.Id == request.WorkflowDefinitionId && w.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (workflowDesign == null)
        {
            throw new BusinessException($"工作流定义 {request.WorkflowDefinitionId} 不存在") { StatusCode = 404 };
        }

        // 2. 反序列化工作流定义
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(
            workflowDesign.FunctionDesgin, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);

        if (definition == null)
        {
            throw new BusinessException(400, "工作流定义反序列化失败");
        }

        _logger.LogInformation(
            "开始执行工作流，设计 ID: {WorkflowDesignId}, 节点数: {NodeCount}",
            request.WorkflowDefinitionId,
            definition.Nodes.Count);

        // 3. 生成实例 ID
        var instanceId = Guid.NewGuid().ToString();

        // 4. 执行工作流
        await foreach (var item in _workflowRuntime.ExecuteAsync(
            definition,
            request.StartupParameters,
            instanceId,
            request.SystemVariables,
            cancellationToken))
        {
            _logger.LogDebug(
                "节点执行: {NodeKey}, 类型: {NodeType}, 状态: {State}",
                item.NodeKey,
                item.NodeType,
                item.State);

            yield return item;
        }

        _logger.LogInformation("工作流执行完成，实例 ID: {InstanceId}", instanceId);
    }
}
