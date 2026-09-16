using System.Text.Json.Nodes;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Persistence;
using MoAI.Database;

namespace MoAI.App.Workflow.Stores;

/// <summary>
/// 工作流定义存储的 EF Core 实现：一个流程应用对应一份编排配置（app_workflow_config，1:1 app），
/// 定义 id 即应用 id 字符串. 草稿可反复保存，发布生成不可变快照并递增版本.
/// </summary>
[InjectOnScoped]
public class DatabaseWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    private readonly DatabaseContext _databaseContext;
    private readonly Services.WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseWorkflowDefinitionStore"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域，保存时提供团队/应用归属）.</param>
    public DatabaseWorkflowDefinitionStore(DatabaseContext databaseContext, Services.WorkflowExecutionContext executionContext)
    {
        _databaseContext = databaseContext;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public async Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var appId = ParseAppId(definition.Id);

        var config = await _databaseContext.AppWorkflowConfigs
            .FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken);

        if (config == null)
        {
            config = new Database.Entities.AppWorkflowConfigEntity
            {
                Id = Guid.CreateVersion7(),
                AppId = appId,
                TeamId = (int)_executionContext.TeamId,
            };
            _databaseContext.AppWorkflowConfigs.Add(config);
        }

        config.DraftDefinition = WorkflowJson.SerializeDefinition(definition);
        // 草稿发生变更后，当前草稿不再与已发布版本一致
        config.Status = 0;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        _executionContext.ConfigId = config.Id;
    }

    /// <summary>
    /// 保存编辑器画布原始 JSON（与草稿定义同事务语义，仅处理器调用）.
    /// </summary>
    public async Task SaveEditorDataAsync(Guid appId, string editorData, CancellationToken cancellationToken = default)
    {
        var config = await FindConfigEntityAsync(appId, cancellationToken);
        if (config != null)
        {
            config.DraftEditorData = editorData;
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<WorkflowDefinition?> FindDefinitionByIdAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var config = await FindConfigEntityAsync(ParseAppId(definitionId), cancellationToken);
        return config?.DraftDefinition == null
            ? null
            : WorkflowJson.DeserializeDefinition(config.DraftDefinition);
    }

    /// <inheritdoc/>
    public async Task<WorkflowDefinition?> FindPublishedDefinitionByIdAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var config = await FindConfigEntityAsync(ParseAppId(definitionId), cancellationToken);
        return config?.PublishedDefinition == null
            ? null
            : WorkflowJson.DeserializeDefinition(config.PublishedDefinition);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _databaseContext.AppWorkflowConfigs
            .Where(x => x.DraftDefinition != null)
            .ToListAsync(cancellationToken);

        return configs
            .Where(x => !string.IsNullOrWhiteSpace(x.DraftDefinition))
            .Select(x => WorkflowJson.DeserializeDefinition(x.DraftDefinition!))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task PublishAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var appId = ParseAppId(definitionId);
        var config = await FindConfigEntityAsync(appId, cancellationToken)
            ?? throw new WorkflowException($"工作流配置不存在：{definitionId}");

        // 发布生成不可变快照：版本递增，草稿与已发布版本一致
        config.PublishedDefinition = config.DraftDefinition;
        config.Version += 1;
        config.Status = 1;
        config.PublishTime = DateTimeOffset.Now;
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 查询编排配置实体.
    /// </summary>
    public Task<Database.Entities.AppWorkflowConfigEntity?> FindConfigEntityAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        return _databaseContext.AppWorkflowConfigs
            .FirstOrDefaultAsync(x => x.AppId == appId, cancellationToken);
    }

    private static Guid ParseAppId(string definitionId)
    {
        if (!Guid.TryParse(definitionId, out var appId))
        {
            throw new WorkflowException($"工作流定义 id 必须为应用 id（GUID）：{definitionId}");
        }

        return appId;
    }
}
