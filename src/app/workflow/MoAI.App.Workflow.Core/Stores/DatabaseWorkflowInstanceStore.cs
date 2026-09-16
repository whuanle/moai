using System.Text.Json;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Persistence;
using MoAI.Database;

namespace MoAI.App.Workflow.Stores;

/// <summary>
/// 工作流实例存储的 EF Core 实现：实例全量 JSON（含节点级状态）落 app_workflow_instance，
/// 每次检查点保存都会刷新实例快照，是断点恢复的依据.
/// 引擎 WorkflowInstance.Id 使用无连字符 GUID 字符串，与本存储的实体 Id 一一对应.
/// </summary>
[InjectOnScoped]
public class DatabaseWorkflowInstanceStore : IWorkflowInstanceStore
{
    private readonly DatabaseContext _databaseContext;
    private readonly Services.WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseWorkflowInstanceStore"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域，提供团队/配置/调试标记）.</param>
    public DatabaseWorkflowInstanceStore(DatabaseContext databaseContext, Services.WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(Instance.WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var entity = new Database.Entities.AppWorkflowInstanceEntity
        {
            Id = Guid.ParseExact(instance.Id, "N"),
            AppId = Guid.Parse(instance.DefinitionId),
            TeamId = (int)_executionContext.TeamId,
            WorkflowConfigId = _executionContext.ConfigId,
            Version = _executionContext.IsDebug ? 0 : instance.DefinitionVersion,
            IsDebug = _executionContext.IsDebug,
            Status = (short)instance.Status,
            Input = instance.Input.ToJsonString(),
            InstanceData = JsonSerializer.Serialize(instance, WorkflowJson.Options),
            StartTime = instance.StartedAt,
            EndTime = instance.EndedAt,
        };

        _databaseContext.AppWorkflowInstances.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Instance.WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var id = Guid.ParseExact(instance.Id, "N");
        var entity = await _databaseContext.AppWorkflowInstances
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity == null)
        {
            // 实例可能是在内存中创建（如单元测试），落到存储时补建
            await CreateAsync(instance, cancellationToken);
            return;
        }

        entity.Status = (short)instance.Status;
        entity.Input = instance.Input.ToJsonString();
        entity.Output = instance.Output?.ToJsonString();
        entity.ErrorMessage = instance.ErrorMessage;
        entity.InstanceData = JsonSerializer.Serialize(instance, WorkflowJson.Options);
        entity.StartTime = instance.StartedAt;
        entity.EndTime = instance.EndedAt;
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Instance.WorkflowInstance?> FindInstanceByIdAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(instanceId, cancellationToken);
        return entity == null
            ? null
            : JsonSerializer.Deserialize<Instance.WorkflowInstance>(entity.InstanceData, WorkflowJson.Options);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Instance.WorkflowInstance>> ListInstancesAsync(string? definitionId = null, CancellationToken cancellationToken = default)
    {
        var query = _databaseContext.AppWorkflowInstances.AsQueryable();
        if (definitionId != null)
        {
            var appId = Guid.Parse(definitionId);
            query = query.Where(x => x.AppId == appId);
        }

        var entities = await query
            .OrderByDescending(x => x.CreateTime)
            .Take(200)
            .ToListAsync(cancellationToken);

        return entities
            .Select(x => JsonSerializer.Deserialize<Instance.WorkflowInstance>(x.InstanceData, WorkflowJson.Options)!)
            .Where(x => x != null)
            .ToList();
    }

    /// <summary>
    /// 查询实例实体.
    /// </summary>
    public Task<Database.Entities.AppWorkflowInstanceEntity?> FindEntityAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var id = Guid.ParseExact(instanceId, "N");
        return _databaseContext.AppWorkflowInstances
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
