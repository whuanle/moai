# 知识图谱图检索消费层 SP-A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 托管图实体向量化（pgvector）+ Agent 应用绑定 GraphIds + 对话 `search_knowledge_graph` 工具 + 工作流 `kgSearch` 节点，实现 GraphRAG local search 轻量版（向量 topK 实体 → 一跳扩展 → 子图文本化）。

**Architecture:** 全线镜像 wiki 已验证模式——`__kg_{id}` pgvector 动态集合表（CommunityToolkit.VectorData）、Maomi.MQ delta 消息幂等消费（一期不建 WorkerTask）、`IAppToolProvider` 工具注入、`GraphIds` 绑定四件套（保存校验/发布快照/双装配点解析/构建上下文）、工作流三件套（NodeTypes/Executor/Client+Guard）。检索服务接口在 `KnowledgeGraph.Shared`（被 AI.Core 与 Workflow.Core 消费），实现与向量基建在 `KnowledgeGraph.Core`。

**Tech Stack:** .NET 10 / Maomi（DI+MQ）/ CommunityToolkit.VectorData.PgVector（HNSW+Cosine）/ MediatR / React 19 + Kiota。

**设计文档:** `docs/superpowers/specs/2026-09-22-kg-graph-search-design.md`（已按代码事实修正：EmbeddingModelId 为 Guid?；向量化走纯 MQ delta）

**硬约束提醒:** 工作区可能有并行 WIP——`git add` 只加指定文件，动 docs/AGENTS.md 前重读最新；`BusinessException` 显式 StatusCode；模型面向 AI 用 `[Description]`；后端运行中锁主工程时分工程编译；**Task 9 需要 syncapi（须先重启带新构建的后端），执行到该任务时先与用户确认**。

---

## File Structure（总览）

| # | 任务 | 主要文件 |
|---|---|---|
| 1 | DB 列 + embedding 配置端点 | KnowledgeGraphEntity/Configuration、asserts SQL、UpdateKnowledgeGraphEmbeddingConfig 命令链、model-options 扩桶 |
| 2 | 向量存储三件套 | KgEmbeddingVectorRecord、IKgEmbeddingVectorStore、PgVectorKgEmbeddingVectorStore、模块注册 |
| 3 | 向量化服务 + MQ | KgEmbeddingService、delta 消息、KgNodeEmbeddingConsumer、CRUD 触发点、删图清理 |
| 4 | 检索服务 + 单测 | IGraphSearchService(KG.Shared)、GraphSearchService(KG.Core)、GraphSearchServiceTests |
| 5 | 团队检索 API | QueryKnowledgeGraphSearchCommand 链 + Controller |
| 6 | 应用绑定 GraphIds | 实体列+asserts、保存校验、快照、双装配点、BuildContext |
| 7 | 对话工具 | GraphAppToolProvider + AI.Core 引用 KG.Shared |
| 8 | 工作流节点 | NodeTypes/Ports/Client/Guard/Executor/注册 |
| 9 | 前端 | 设置页 embedding、应用配置多选、工作流面板、i18n、syncapi |
| 10 | E2E | local-dev/kg-search-e2e.mjs（KGS-S1~S9，本地桩渠道零 SKIP） |
| 11 | 文档 | 四件套 KGS 场景 + AGENTS.md + rounds-log |

任务顺序即依赖顺序：1→2→3→4→5（KG 内聚）→6→7（消费）→8（工作流）→9（前端，依赖 syncapi）→10→11。

---

### Task 1: 数据层——embedding 两列 + 配置端点 + 模型选项扩桶

**Files:**
- Modify: `src/database/MoAI.Database.Shared/Entities/KnowledgeGraphEntity.cs`
- Modify: `src/database/MoAI.Database.Postgres/Data/KnowledgeGraphConfiguration.cs`
- Create: `asserts/knowledge_graph_embedding.sql`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Commands/UpdateKnowledgeGraphEmbeddingConfigCommand.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Handlers/UpdateKnowledgeGraphEmbeddingConfigCommandHandler.cs`
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Api/Controllers/KnowledgeGraphController.cs`（新增端点）
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Handlers/QueryKnowledgeGraphModelOptionsCommandHandler.cs` + 其响应模型（KG.Shared）
- Test: `tests/MoAI.KnowledgeGraph.Tests/UpdateKnowledgeGraphEmbeddingConfigCommandHandlerTests.cs`

- [ ] **Step 1: 实体加两列**（`KnowledgeGraphEntity.cs`，`AvatarPath` 之后追加）

```csharp
    /// <summary>
    /// 向量化模型的 id（ai_model.id）；为空表示未配置、图谱不参与向量检索.
    /// </summary>
    public Guid? EmbeddingModelId { get; set; }

    /// <summary>
    /// 向量维度（1-2000，建 hnsw 索引的硬上限）.
    /// </summary>
    public int EmbeddingDimensions { get; set; }
```

- [ ] **Step 2: EF 配置**（`KnowledgeGraphConfiguration.cs`，`OnConfigurePartial` 之前按列名字母序插入）

```csharp
        entity.Property(e => e.EmbeddingDimensions)
            .HasDefaultValue(1024)
            .HasComment("知识图谱向量维度（1-2000，建 hnsw 索引的硬上限）")
            .HasColumnName("embedding_dimensions");
        entity.Property(e => e.EmbeddingModelId)
            .HasComment("向量化模型的id；为空表示未配置、图谱不参与向量检索")
            .HasColumnName("embedding_model_id");
```

- [ ] **Step 3: 增量 SQL**（`asserts/knowledge_graph_embedding.sql`，惯例照 `asserts/knowledge_graph.sql` 的 v2.2 范本）

```sql
-- 知识图谱图检索（SP-A）：knowledge_graph 表新增向量化模型与维度（存量库补列，幂等）
-- 无 EF Migration：EnsureCreated 只对空库生效，存量库需手动执行本文件，
-- 并保持与 src/database 下 KnowledgeGraphEntity 及其 Configuration 一致
ALTER TABLE knowledge_graph
    ADD COLUMN IF NOT EXISTS embedding_model_id uuid NULL;

COMMENT ON COLUMN knowledge_graph.embedding_model_id IS '向量化模型的id；为空表示未配置、图谱不参与向量检索';

ALTER TABLE knowledge_graph
    ADD COLUMN IF NOT EXISTS embedding_dimensions integer DEFAULT 1024;

COMMENT ON COLUMN knowledge_graph.embedding_dimensions IS '知识图谱向量维度（1-2000，建 hnsw 索引的硬上限）';
```

- [ ] **Step 4: 配置命令**（`UpdateKnowledgeGraphEmbeddingConfigCommand.cs`，风格照 `UpdateKnowledgeGraphCommand`）

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 配置知识图谱的向量化模型与维度（管理员）；配置后图谱参与向量检索.
/// </summary>
public class UpdateKnowledgeGraphEmbeddingConfigCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphEmbeddingConfigCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 向量化模型 id（需已启用且公开或已授权给该团队）.
    /// </summary>
    public Guid EmbeddingModelId { get; init; }

    /// <summary>
    /// 向量维度（1-2000）.
    /// </summary>
    public int EmbeddingDimensions { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphEmbeddingConfigCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，此处不校验。
        validate.RuleFor(x => x.EmbeddingModelId).NotEmpty().WithMessage("向量化模型不能为空.");
        validate.RuleFor(x => x.EmbeddingDimensions).InclusiveBetween(1, 2000).WithMessage("向量维度取值 1-2000.");
    }
}
```

- [ ] **Step 5: Handler**（模型可用性校验镜像 `QueryWikiModelOptionsCommandHandler.cs:48-57` 的授权查询）

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Core.Handlers;

/// <summary>
/// 配置知识图谱向量化模型.
/// </summary>
public class UpdateKnowledgeGraphEmbeddingConfigCommandHandler : IRequestHandler<UpdateKnowledgeGraphEmbeddingConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphEmbeddingConfigCommandHandler"/> class.
    /// </summary>
    public UpdateKnowledgeGraphEmbeddingConfigCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphEmbeddingConfigCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        if (!string.Equals(graph.Mode, MoAI.KnowledgeGraph.Models.KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱不支持向量检索配置.") { StatusCode = 409 };
        }

        var authorizedModelIds = await _databaseContext.AiModelAuthorizations
            .Where(x => x.TeamId == graph.TeamId)
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        var modelEnabled = await (from m in _databaseContext.AiModels
                                  join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                                  where m.Id == request.EmbeddingModelId && m.Enabled && c.Enabled
                                      && (m.IsPublic || authorizedModelIds.Contains(m.Id))
                                  select m.Id).AnyAsync(cancellationToken);
        if (!modelEnabled)
        {
            throw new BusinessException("向量化模型不存在、未启用或未授权给该团队.") { StatusCode = 400 };
        }

        graph.EmbeddingModelId = request.EmbeddingModelId;
        graph.EmbeddingDimensions = request.EmbeddingDimensions;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
```

（`AiModelAuthorizations` 的元素字段名/命名空间以 `QueryWikiModelOptionsCommandHandler` 实际代码为准，照抄其查询。）

- [ ] **Step 6: Controller 端点**（`KnowledgeGraphController.cs`，`Update` 端点之后）

```csharp
    /// <summary>
    /// 配置知识图谱向量化模型.
    /// </summary>
    [HttpPut("{id}/embedding-config")]
    public Task<EmptyCommandResponse> UpdateEmbeddingConfig(long id, [FromBody] UpdateKnowledgeGraphEmbeddingConfigCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphEmbeddingConfigCommand { KnowledgeGraphId = id, EmbeddingModelId = req.EmbeddingModelId, EmbeddingDimensions = req.EmbeddingDimensions }, ct);
```

- [ ] **Step 7: model-options 扩 embedding 桶**——在 `QueryKnowledgeGraphModelOptionsCommandHandler` 里镜像 `QueryWikiModelOptionsCommandHandler.cs:48-72`：同一条授权 join 查询取全部模型后，按 `ModelKind == "embedding"`（OrdinalIgnoreCase）新增 `EmbeddingModels` 桶；响应模型（KG.Shared 的 `QueryKnowledgeGraphModelOptionsCommandResponse`）加 `public IReadOnlyList<KnowledgeGraphModelOptionItem> EmbeddingModels { get; init; } = [];`（item 类型复用现有）。

- [ ] **Step 8: 单测**（`tests/MoAI.KnowledgeGraph.Tests/UpdateKnowledgeGraphEmbeddingConfigCommandHandlerTests.cs`，骨架照 `UpdateKnowledgeGraphAvatarCommandHandlerTests`：TestSqliteContext + mock authorizer；AiModelAuthorizations/AiModels/AiChannels 表用 sqlite 直插造数）

用例：`Handle_NonManagedGraph_Throws409`；`Handle_ModelNotAuthorized_Throws400`；`Handle_ValidModel_SavesConfig`（断言实体两字段落库）。

- [ ] **Step 9: 验证 + 提交**

Run: `dotnet build src/knowledgegraph/MoAI.KnowledgeGraph.Core/MoAI.KnowledgeGraph.Core.csproj && dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj`
Expected: 0 error；测试全过

```bash
git add src/database/MoAI.Database.Shared/Entities/KnowledgeGraphEntity.cs src/database/MoAI.Database.Postgres/Data/KnowledgeGraphConfiguration.cs asserts/knowledge_graph_embedding.sql src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Commands/UpdateKnowledgeGraphEmbeddingConfigCommand.cs src/knowledgegraph/MoAI.KnowledgeGraph.Core/Handlers/UpdateKnowledgeGraphEmbeddingConfigCommandHandler.cs src/knowledgegraph/MoAI.KnowledgeGraph.Api/Controllers/KnowledgeGraphController.cs tests/MoAI.KnowledgeGraph.Tests/UpdateKnowledgeGraphEmbeddingConfigCommandHandlerTests.cs
git commit -m "feat(knowledgegraph): 图谱向量化模型配置——embedding 两列 + 配置端点 + model-options 扩桶"
```
（git add 前逐个 `git status --short <path>` 核对；响应模型文件若为独立文件一并加入。）

---

### Task 2: 向量存储三件套

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KgEmbeddingVectorRecord.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/IKgEmbeddingVectorStore.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/PgVectorKgEmbeddingVectorStore.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KgEmbeddingSearchResult.cs`
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/KnowledgeGraphCoreModule.cs`
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/MoAI.KnowledgeGraph.Core.csproj`

- [ ] **Step 1: csproj 加包**——打开 `src/wiki/MoAI.Wiki.Core/MoAI.Wiki.Core.csproj`，找到 CommunityToolkit.VectorData 相关的 `<PackageReference>`（约 :23），**原样复制**到 KG.Core csproj（版本走 Directory.Packages.props，不写 Version）。

- [ ] **Step 2: 记录与结果类型**（镜像 `WikiEmbeddingVectorRecord.cs` 风格）

```csharp
using System;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱向量记录（每个图谱一个 pgvector 集合 __kg_{id} 中的一行）.
/// </summary>
public class KgEmbeddingVectorRecord
{
    /// <summary>
    /// 主键.
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public int KgId { get; set; }

    /// <summary>
    /// 节点 id（KgNode.id，召回溯源的真源）.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; set; }

    /// <summary>
    /// 节点名.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 被向量化的文本（名称 + 描述）.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 向量.
    /// </summary>
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

`KgEmbeddingSearchResult.cs`：

```csharp
using System;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 向量召回结果.
/// </summary>
public class KgEmbeddingSearchResult
{
    /// <summary>
    /// 命中记录.
    /// </summary>
    public required KgEmbeddingVectorRecord Record { get; init; }

    /// <summary>
    /// 相似度得分（Cosine Similarity）.
    /// </summary>
    public double Score { get; init; }
}
```

- [ ] **Step 3: 接口**（镜像 `IWikiEmbeddingVectorStore`，文档粒度照抄）

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱向量存储抽象：每个图谱一个集合（表），由 pgvector 提供者自动建表.
/// </summary>
public interface IKgEmbeddingVectorStore
{
    /// <summary>
    /// 确保图谱向量集合存在（不存在则按维度创建表与 hnsw 索引）.
    /// </summary>
    Task EnsureCollectionAsync(int kgId, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用新记录整体替换某节点的向量（先删后写）.
    /// </summary>
    Task ReplaceNodeVectorsAsync(int kgId, string nodeId, IReadOnlyList<KgEmbeddingVectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除某节点的全部向量.
    /// </summary>
    Task DeleteNodeVectorsAsync(int kgId, string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除图谱的整个向量集合（删图时调用）.
    /// </summary>
    Task DeleteGraphVectorsAsync(int kgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按查询向量召回最相似的记录.
    /// </summary>
    Task<IReadOnlyList<KgEmbeddingSearchResult>> SearchAsync(int kgId, ReadOnlyMemory<float> queryVector, int top, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: PgVector 实现**——镜像 `PgVectorWikiEmbeddingVectorStore.cs` 全文逐段替换：集合名 `GetCollectionName(int kgId) => $"__kg_{kgId}"`；`BuildDefinition` 的 Data 属性换为 `KgId(int, IsIndexed)`/`NodeId(string, IsIndexed)`/`EntityTypeId(long, IsIndexed)`/`Name(string)`/`Content(string)`，Vector 属性不变（CosineSimilarity+Hnsw）；`ReplaceByDocument` 改为 `DeleteByNodeAsync(collection, string nodeId)`（过滤 `x => x.NodeId == nodeId`）；新增 `DeleteGraphVectorsAsync`（`if (await collection.CollectionExistsAsync(ct)) await collection.DeleteCollectionAsync(ct);`——方法名以编译器提示为准，VectorData 1.0.1 的集合删除方法名照 wiki 工程内可查用法或 IDE 提示）；`ResolveDimensionsAsync` 查 `KnowledgeGraphs` 的 `EmbeddingDimensions`。DI 注册在 `KnowledgeGraphCoreModule.ConfigureServices` 末尾追加（照 `WikiCoreModule.cs:25-26`）：

```csharp
        context.Services.AddSingleton(sp => new PostgresVectorStore(sp.GetRequiredService<SystemOptions>().Database));
        context.Services.AddScoped<IKgEmbeddingVectorStore, PgVectorKgEmbeddingVectorStore>();
```

（`PostgresVectorStore`/`SystemOptions` 的 using 照 `WikiCoreModule.cs` 文件头抄。）

- [ ] **Step 5: 验证 + 提交**

Run: `dotnet build src/knowledgegraph/MoAI.KnowledgeGraph.Core/MoAI.KnowledgeGraph.Core.csproj`
Expected: 0 error

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Core
git commit -m "feat(knowledgegraph): __kg_{id} pgvector 向量存储三件套（镜像 wiki）"
```

---

### Task 3: 向量化服务 + MQ delta 消费

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Consumers/Events/KgNodeEmbeddingDeltaMessage.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Consumers/KgNodeEmbeddingConsumer.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/IKgEmbeddingService.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KgEmbeddingService.cs`
- Modify: KG.Core 内节点写路径 Handler（grep 决定清单，见 Step 4）+ `DeleteKnowledgeGraphCommandHandler.cs`
- 可能 Modify: `IKnowledgeGraphStore.cs` + `CypherKnowledgeGraphStore.cs`（若缺单节点读取，见 Step 2）

- [ ] **Step 1: 消息**（镜像 `EmbeddingDocumentTaskMessage` 的 `[RouterKey]` 风格）

```csharp
using System;
using System.Collections.Generic;
using Maomi.MQ;

namespace MoAI.KnowledgeGraph.Consumers.Events;

/// <summary>
/// 知识图谱节点向量增量消息：upsert 读 PG/图库最新状态幂等处理，多次 delta 自然合并.
/// </summary>
[RouterKey("kg.node.embedding")]
public class KgNodeEmbeddingDeltaMessage
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 需要重建向量的节点 id.
    /// </summary>
    public List<string> UpsertNodeIds { get; init; } = [];

    /// <summary>
    /// 需要删除向量的节点 id.
    /// </summary>
    public List<string> DeleteNodeIds { get; init; } = [];
}
```

- [ ] **Step 2: 单节点读取缺口**——`grep -n "GetNodeAsync\|Task<KnowledgeGraphNodeRecord" src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/IKnowledgeGraphStore.cs`：若接口已有按 id 取单节点的方法则直接用；**若没有**，给 `IKnowledgeGraphStore` 加 `Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken);` 并在 `CypherKnowledgeGraphStore` 实现（`MATCH (n:KgNode {kgId: $kgId, id: $nodeId}) RETURN {NodeReturn}` + `MapNode`，未命中返回 null；风格照 `QueryCanvasAsync` 的 `ReadAsync` 私有辅助），并在接口方法补 /// 注释。

- [ ] **Step 3: 向量化服务**——`IKgEmbeddingService`（`Task ProcessDeltaAsync(KgNodeEmbeddingDeltaMessage message, CancellationToken cancellationToken);`）+ `KgEmbeddingService`（`[InjectOnScoped]`）实现要点：
  1. 读图谱实体：不存在 / `Mode != managed` / `EmbeddingModelId` 为空 / `EmbeddingDimensions <= 0` → 直接 return（自动触发场景静默跳过，不抛错）
  2. 解析模型：镜像 `WikiEmbeddingService.ResolveModelAsync`（join AiModels/AiChannels，Enabled + IsDeleted == 0 + IsPublic 或 AiModelAuthorizations 授权 graph.TeamId），不可用 → return；`IEmbeddingGeneratorProvider.GetEmbeddingGeneratorAsync(model, channel, ct)` 取 generator
  3. deletes：逐个 `DeleteNodeVectorsAsync(kgId, nodeId)`
  4. upserts：逐个 `GetNodeAsync(kgId, nodeId)`，null → 跳过；name 与 description 均空 → 仅 `DeleteNodeVectorsAsync`；否则 `Content = $"{name}\n{description}"`，`generator.GenerateAsync([content], new EmbeddingGenerationOptions { Dimensions = graph.EmbeddingDimensions }, ct)`（空向量抛 `InvalidOperationException`，镜像 `WikiEmbeddingService.GenerateEmbeddingVectorAsync` 的两个校验），构造 `KgEmbeddingVectorRecord { Key = Guid.CreateVersion7(), ... }` → `ReplaceNodeVectorsAsync`
  5. 每节点独立 try/catch：单节点失败记日志（`ILogger`）继续其余节点，最后若无一成功且原本有 upsert → rethrow 最后异常（触发 MQ 重投）
  6. 私有 `ResolveModelAsync(Guid modelId, int teamId, ct)` 注释注明「与 WikiEmbeddingService.ResolveModelAsync 保持语义同步」

- [ ] **Step 4: Consumer**（镜像 `EmbeddingDocumentConsumer` 的声明/FaildAsync/FallbackAsync 骨架；无 WorkerTask，ExecuteAsync 直调服务）

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Microsoft.Extensions.Logging;
using MoAI.KnowledgeGraph.Consumers.Events;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Consumers;

/// <summary>
/// 知识图谱节点向量增量消费者：幂等处理（upsert 读最新状态），失败由 MQ 重投.
/// </summary>
[Consumer("kg.node.embedding", Qos = 1)]
public class KgNodeEmbeddingConsumer : IConsumer<KgNodeEmbeddingDeltaMessage>
{
    private readonly IKgEmbeddingService _embeddingService;
    private readonly ILogger<KgNodeEmbeddingConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KgNodeEmbeddingConsumer"/> class.
    /// </summary>
    public KgNodeEmbeddingConsumer(IKgEmbeddingService embeddingService, ILogger<KgNodeEmbeddingConsumer> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(MessageHeader messageHeader, KgNodeEmbeddingDeltaMessage message)
    {
        await _embeddingService.ProcessDeltaAsync(message, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, KgNodeEmbeddingDeltaMessage message)
    {
        _logger.LogError(ex, "kg embedding delta failed. KgId={KgId}, RetryCount={RetryCount}", message.KgId, retryCount);
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, KgNodeEmbeddingDeltaMessage? message, Exception? ex)
    {
        // 重试耗尽：Ack 放弃（检索侧向量缺失仅表现为不命中，可由后续节点操作再次触发），不阻塞队列
        _logger.LogError(ex, "kg embedding delta dropped after retries. KgId={KgId}", message?.KgId);
        return ConsumerState.Ack;
    }
}
```

（Consumer 注册零配置：`InfraCoreModule` 的 `AddMaomiMQ` 扫描全部模块程序集。）

- [ ] **Step 5: 触发点接线**——`grep -rn "CreateNodeAsync\|UpdateNodeAsync\|DeleteNodeAsync\|CreateNodesBatchAsync" src/knowledgegraph --include="*Handler*.cs"` 枚举托管图节点写路径的 MediatR Handler（内部 CRUD + 批量导入 + AI 导入 + External 处理器）：在 `SaveChangesAsync` 成功之后注入并发布 `IMessagePublisher.AutoPublishAsync(new KgNodeEmbeddingDeltaMessage { ... })`（upsert 带新/改节点 id；delete 带 nodeIds；批量导入一条消息带全部 nodeIds）。发布失败不回滚业务（catch 记日志）。外部 API 处理器只覆盖托管图路径。清单在报告里列出（grep 结果 + 每处一行说明）。
  另在 `DeleteKnowledgeGraphCommandHandler` 删图成功后直调 `_vectorStore.DeleteGraphVectorsAsync((int)graph.Id, ct)`（注入 `IKgEmbeddingVectorStore`；同步清理，修 wiki 缺口）。

- [ ] **Step 6: 单测**（`tests/MoAI.KnowledgeGraph.Tests/KgEmbeddingServiceTests.cs`，镜像 `tests/MoAI.UsageCounters.Tests/WikiEmbeddingServiceTests.cs` 的 mock 手法：`Mock<IKgEmbeddingVectorStore>` + `Mock<IEmbeddingGenerator<string, Embedding<float>>>` + `Mock<IEmbeddingGeneratorProvider>` + sqlite Context 造图谱/模型/渠道/授权行）

用例：未配模型静默返回（store 零调用）；正常 upsert 走 Replace 且 Content 为「名称\n描述」；delete 走 Delete；单节点异常不影响其余节点；模型未授权静默返回。

- [ ] **Step 7: 验证 + 提交**

Run: `dotnet build src/knowledgegraph/MoAI.KnowledgeGraph.Core/MoAI.KnowledgeGraph.Core.csproj && dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj`
Expected: 0 error；全过

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Core
git commit -m "feat(knowledgegraph): 节点向量增量同步——MQ delta 幂等消费 + CRUD/AI 导入触发 + 删图清集合"
```

---

### Task 4: 检索服务 + 单测

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models/GraphSearchModels.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Services/IGraphSearchService.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/GraphSearchService.cs`
- Test: `tests/MoAI.KnowledgeGraph.Tests/GraphSearchServiceTests.cs`

- [ ] **Step 1: KG.Shared 模型与接口**（record 带 ///，镜像 `KgCypherAccessModels.cs` 风格）

```csharp
using System.Collections.Generic;

namespace MoAI.KnowledgeGraph.Models;

/// <summary>邻居关系摘要.</summary>
/// <param name="RelationName">关系类型名（类型未定义时为 null）.</param>
/// <param name="Direction">out（出边）/ in（入边）.</param>
/// <param name="Name">邻居节点名.</param>
/// <param name="Description">邻居描述.</param>
public sealed record GraphNeighbor(string? RelationName, string Direction, string Name, string Description);

/// <summary>图检索命中.</summary>
/// <param name="KgId">图谱 id.</param>
/// <param name="NodeId">节点 id.</param>
/// <param name="Name">节点名.</param>
/// <param name="Description">节点描述.</param>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="EntityTypeName">实体类型名.</param>
/// <param name="Score">相似度得分.</param>
/// <param name="Neighbors">一跳邻居.</param>
public sealed record GraphSearchHit(
    long KgId,
    string NodeId,
    string Name,
    string Description,
    long EntityTypeId,
    string? EntityTypeName,
    double Score,
    IReadOnlyList<GraphNeighbor> Neighbors);

/// <summary>图检索结果.</summary>
/// <param name="Hits">命中列表（按得分降序）.</param>
/// <param name="Contents">命中节点文本列表（与 Hits 同序）.</param>
/// <param name="Text">文本化拼接结果（供 LLM 上下文，超长截断）.</param>
/// <param name="SkippedHints">被跳过的图谱及原因（未配置向量化/模型不可用等）.</param>
public sealed record GraphSearchResult(
    IReadOnlyList<GraphSearchHit> Hits,
    IReadOnlyList<string> Contents,
    string Text,
    IReadOnlyList<string> SkippedHints);
```

接口（`IGraphSearchService.cs`）：

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱向量检索服务：查询向量 → top 实体 → 一跳关系扩展 → 子图文本化（GraphRAG local search 轻量版）.
/// 团队归属由调用方保证（应用绑定保存时校验 / 工作流 Client 守卫 / 检索 API Authorizer）.
/// </summary>
public interface IGraphSearchService
{
    /// <summary>
    /// 在多张图谱中做语义检索.
    /// </summary>
    /// <param name="graphIds">图谱 id 集（去重去零）.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="topPerGraph">每图召回条数.</param>
    /// <param name="minScore">相似度阈值（null 不过滤）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>检索结果（含跳过提示）.</returns>
    Task<GraphSearchResult> SearchAsync(IReadOnlyCollection<long> graphIds, string query, int topPerGraph = 5, double? minScore = null, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: 实现**（`[InjectOnScoped]`，流程镜像 `WikiSearchService.SearchAsync` 的 per-graph 循环）：
  1. 入参清洗（ids 去重去非正、query 空返回空结果）；批量读 `KnowledgeGraphs`（过滤 `Mode == managed`，connected 计入 SkippedHints「接入图谱不支持向量检索」）
  2. 每图：`EmbeddingModelId` 空/维度非法 → SkippedHints 跳过；`ResolveModelAsync`（与 Task 3 同款语义，注释同步声明）不可用 → SkippedHints；`GetEmbeddingGeneratorAsync` + 查询向量（`GenerateAsync([query], options Dimensions=graph.EmbeddingDimensions, ct)`；空向量跳过）
  3. `_vectorStore.SearchAsync(kgId, vector, topPerGraph, ct)`；`minScore` 过滤（`result.Score >= minScore`）
  4. 命中实体逐个 `GetNeighborsAsync(kgId, nodeId, 10, ct)`（Truncated 忽略），边按 `relationTypeId` 批量查 `KnowledgeGraphRelationTypes` 补 `RelationName`（方向：`edge.SourceNodeId == nodeId ? "out" : "in"`，邻居取对端）；实体类型名批量查 `KnowledgeGraphEntityTypes`
  5. 汇总：全图命中合并按 Score 降序取 `topPerGraph * ids.Count`；`Contents` = 每命中 `Name + "：" + Description`；`Text` = 每命中一段「【名称（类型名）】描述\n关联：关系名(out)→邻居名（描述）；…」，总长超 8192 字符截断加 `…(已截断)`
- [ ] **Step 3: 单测**（`GraphSearchServiceTests.cs`：sqlite 造图谱/类型行 + mock `IKgEmbeddingVectorStore`/`IKnowledgeGraphStore`/embedding provider，手法照 Task 3 Step 6）

用例：多图归并按分排序取 topK*N；未配模型图进 SkippedHints 且不影响其他图；connected 图进 SkippedHints；minScore 过滤；邻居关系名/方向正确；空 query 返回空；Text 超 8192 截断。

- [ ] **Step 4: 验证 + 提交**

Run: `dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj`
Expected: 全过（含 Task 1/3 用例）

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models/GraphSearchModels.cs src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Services/IGraphSearchService.cs src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/GraphSearchService.cs tests/MoAI.KnowledgeGraph.Tests/GraphSearchServiceTests.cs
git commit -m "feat(knowledgegraph): GraphSearchService——向量 topK + 一跳扩展 + 子图文本化"
```

---

### Task 5: 团队检索 API

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Commands/QueryKnowledgeGraphSearchCommand.cs`（含响应嵌套类型或独立响应模型）
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Handlers/QueryKnowledgeGraphSearchCommandHandler.cs`
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Api/Controllers/KnowledgeGraphController.cs`

- [ ] **Step 1: 命令**（`{KnowledgeGraphId, Query, TopK=5, MinScore?}`；Validate：Query NotEmpty、TopK 1-50；响应 DTO 镜像 `GraphSearchResult` 字段（KG.Shared 模型直接复用作为响应属性））

- [ ] **Step 2: Handler**（`AuthorizeAsync(adminOnly: false)` → graph 非 managed 或未配模型抛 409「该图谱未配置向量化，请先在设置中选择向量化模型.」→ 单图转调 `IGraphSearchService`）

- [ ] **Step 3: Controller**（`UpdateEmbeddingConfig` 之后）：

```csharp
    /// <summary>
    /// 知识图谱语义检索（向量 topK + 一跳扩展）.
    /// </summary>
    [HttpPost("{id}/search")]
    public Task<GraphSearchResult> Search(long id, [FromBody] QueryKnowledgeGraphSearchCommand req, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphSearchCommand { KnowledgeGraphId = id, Query = req.Query, TopK = req.TopK, MinScore = req.MinScore }, ct);
```

- [ ] **Step 4: 验证 + 提交**（build + 既有 KG 测试不回归）

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Commands/QueryKnowledgeGraphSearchCommand.cs src/knowledgegraph/MoAI.KnowledgeGraph.Core/Handlers/QueryKnowledgeGraphSearchCommandHandler.cs src/knowledgegraph/MoAI.KnowledgeGraph.Api/Controllers/KnowledgeGraphController.cs
git commit -m "feat(knowledgegraph): 团队级图谱语义检索 API（POST /{id}/search）"
```

---

### Task 6: 应用绑定 GraphIds

**Files:**
- Modify: `src/database/MoAI.Database.Shared/Entities/AppAgentConfigEntity.cs` + `src/database/MoAI.Database.Postgres/Data/AppAgentConfigConfiguration.cs`
- Create: `asserts/app_agent_config_graph_ids.sql`
- Modify: `src/database/MoAI.Database.Shared/Aggregates/AppAgentConfigSnapshot.cs`
- Modify: `src/app/MoAI.App.Core/Handlers/SaveAppAgentConfigCommandHandler.cs` + 命令模型（`SaveAppAgentConfigCommand`）
- Modify: `src/app/MoAI.App.Core/AppAgentConfigJson.cs`
- Modify: `src/ai/MoAI.AI.Core/Services/AppContextProviderFactory.cs`（BuildContext）、`AppAgentFactory.cs`、`WorkflowNodeAiInvoker.cs`
- Test: `tests/MoAI.App.Tests/`（找到 SaveAppAgentConfig 相关既有测试则扩展，无则新增校验方法级测试；以实际测试工程结构为准）

- [ ] **Step 1: 实体列**——`AppAgentConfigEntity.WikiIds`（:45）之后加 `public string GraphIds { get; set; } = "[]";`（/// 注释照 WikiIds 措辞：JSON 数组文本、元素为 knowledge_graph.id）；EF 配置**照抄同文件里 WikiIds 的 Property 映射**改为 graph_ids；增量 SQL（asserts，幂等）：

```sql
-- Agent 应用绑定知识图谱（SP-A）：app_agent_config 表新增 graph_ids（存量库补列，幂等）
-- 列名以 AppAgentConfigConfiguration 中 WikiIds 的实际 ToTable/HasColumnName 为准（先核对再落笔）
ALTER TABLE public.app_agent_config ADD COLUMN IF NOT EXISTS graph_ids text NOT NULL DEFAULT '[]';
COMMENT ON COLUMN public.app_agent_config.graph_ids IS '绑定的知识图谱ID列表，JSON 数组文本，元素为 knowledge_graph.id（整数），如 ''[1,2]''';
```

（表名先 `grep -n "ToTable" src/database/MoAI.Database.Postgres/Data/AppAgentConfigConfiguration.cs` 核对。）

- [ ] **Step 2: 快照**——`AppAgentConfigSnapshot` 加 `public string GraphIds { get; set; } = "[]";`（注释同 WikiIds「原样透传」）+ `Serialize` 加 `GraphIds = string.IsNullOrWhiteSpace(config.GraphIds) ? "[]" : config.GraphIds,` + `ResolveEffectiveConfig` 克隆行加 `GraphIds = snapshot.GraphIds,` + 检查 `CloneWithExternalRestrictions`（`AppAgentFactory.cs:249-271`）对 WikiIds 的处理并同待遇拷贝 GraphIds。

- [ ] **Step 3: 保存校验**——命令模型加 `IReadOnlyCollection<long>? GraphIds`（照 WikiIds 字段声明与 ///）；`SaveAppAgentConfigCommandHandler` 镜像 `ValidateWikiIdsAsync` 加 `ValidateGraphIdsAsync`：查本团队 `KnowledgeGraphs`（`TeamId == teamId && Mode == managed`）取 ownedSet，invalid → 400「包含不属于该团队或不支持检索的知识图谱，请重新选择.」（显式排除接入图，与前端下拉一致）；create/update 分支与 `PublishedConfig` 兜底建行（`PublishAppCommandHandler.cs:64-83` 的默认行）加 `GraphIds = "[]"`；`AppAgentConfigJson.SerializeGraphIds` 照 `SerializeWikiIds`。

- [ ] **Step 4: 双装配点**——`AppAgentBuildContext` 加 `public IReadOnlyList<long> GraphIds { get; init; } = [];`（/// 注释「绑定的知识图谱 id」）；`AppAgentFactory` 构建上下文加 `GraphIds = ParseGraphIds(effectiveConfig.GraphIds),`（私有 `ParseGraphIds` 照抄 `ParseWikiIds` :228-242 改字段名）；`WorkflowNodeAiInvoker` 同款（:93 与 :149-159 两处）。

- [ ] **Step 5: 测试 + 验证 + 提交**（单测覆盖 ValidateGraphIdsAsync 三分支：空通过/非本团队 400/接入图 400；builder 级测试若 App.Tests 有 SaveAppAgentConfig 既有测试则扩展）

Run: `dotnet build src/app/MoAI.App.Core/MoAI.App.Core.csproj && dotnet build src/ai/MoAI.AI.Core/MoAI.AI.Core.csproj && dotnet test tests/MoAI.App.Tests/MoAI.App.Tests.csproj`

```bash
git add src/database/MoAI.Database.Shared/Entities/AppAgentConfigEntity.cs src/database/MoAI.Database.Postgres/Data/AppAgentConfigConfiguration.cs asserts/app_agent_config_graph_ids.sql src/database/MoAI.Database.Shared/Aggregates/AppAgentConfigSnapshot.cs src/app/MoAI.App.Core tests/MoAI.App.Tests src/ai/MoAI.AI.Core/Services
git commit -m "feat(app): Agent 应用绑定知识图谱——GraphIds 四件套（校验/快照/解析/上下文）"
```
（git add 前逐路径核对实际改动文件。）

---

### Task 7: 对话工具 GraphAppToolProvider

**Files:**
- Modify: `src/ai/MoAI.AI.Core/MoAI.AI.Core.csproj`（引用 `..\..\knowledgegraph\MoAI.KnowledgeGraph.Shared\MoAI.KnowledgeGraph.Shared.csproj`）
- Create: `src/ai/MoAI.AI.Core/Tools/GraphAppToolProvider.cs`
- Modify: `src/ai/MoAI.AI.Core/Tools/AppTool.cs`（:66 Kind 注释加 `|graph`）

- [ ] **Step 1: Provider**——逐行镜像 `WikiAppToolProvider.cs`（105 行），替换点：`ToolName = "search_knowledge_graph"`；`Title = "知识图谱检索"`；`Description = "在应用绑定的知识图谱中检索实体及其一跳关系，适合多跳关联问题（如「A 和 B 什么关系」「有哪些 X」）；需要文档原文片段时改用知识库检索。"`；`Kind = "graph"`；`Order => 21`；守卫 `context.GraphIds.Count == 0` 返回空；入参解析 `query`（必填）+ `topK`（可选数字，`Math.Clamp(topK, 1, 20)` 默认 5）；调用 `_graphSearchService.SearchAsync(context.GraphIds, query, topK)`；payload `JsonSerializer.Serialize(new { query, count, hits = hits.Select(h => new { graphId = h.KgId, nodeId = h.NodeId, name = h.Name, entityType = h.EntityTypeName, description = h.Description, score = h.Score, neighbors = h.Neighbors.Select(n => new { relation = n.RelationName, direction = n.Direction, name = n.Name, description = n.Description }) }), skipped = result.SkippedHints })`。依赖注入 `IGraphSearchService`。

- [ ] **Step 2: 验证 + 提交**（`dotnet build src/ai/MoAI.AI.Core/MoAI.AI.Core.csproj` 0 error；工具经 `IAppToolProvider` 特性自动装配，无手工注册）

```bash
git add src/ai/MoAI.AI.Core
git commit -m "feat(ai): search_knowledge_graph 应用工具——绑定图谱的语义检索注入对话"
```

---

### Task 8: 工作流 kgSearch 节点

**Files:**
- Modify: `src/app/workflow/MoAI.App.Workflow/Definition/NodeTypes.cs`
- Modify: `src/app/workflow/MoAI.App.Workflow/Nodes/WorkflowPorts.cs`（加 `IWorkflowGraphSearchClient` + hit 记录，或独立文件镜像 `WorkflowWikiSearchHit.cs`）
- Create: `src/app/workflow/MoAI.App.Workflow/Nodes/Builtin/KnowledgeGraphSearchNodeExecutor.cs`
- Create: `src/app/workflow/MoAI.App.Workflow.Core/Services/WorkflowGraphSearchClient.cs`
- Create: `src/app/workflow/MoAI.App.Workflow.Core/Services/KnowledgeGraphSearchGuard.cs`
- Modify: `src/app/workflow/MoAI.App.Workflow.Core/MoAI.App.Workflow.Core.csproj`（引用 KG.Shared）、`WorkflowServiceCollectionExtensions.cs`
- Modify: guard 的调用点（先 `grep -rn "KnowledgeSearchWikiGuard" src/app --include="*.cs"` 找到 wiki guard 的调用位置，同点登记 kg guard）

- [ ] **Step 1: 端口 + hit**——镜像 `IWorkflowWikiSearchClient`（`WorkflowPorts.cs:89-99`，注释同款「引擎不绑定图谱实现，由宿主注入（实现方负责团队归属校验与检索）」）：

```csharp
    Task<IReadOnlyList<WorkflowGraphSearchHit>> SearchAsync(IReadOnlyCollection<long> kgIds, string query, int top, CancellationToken ct);
```

`WorkflowGraphSearchHit` 字段：`long KgId / string NodeId / string Name / string? EntityTypeName / string Description / double Score / string Text`（Text 为该命中含邻居的文本化片段，镜像 wiki 的 Content 定位）。

- [ ] **Step 2: Client**（`[InjectOnScoped]`，逐行镜像 `WorkflowWikiSearchClient.cs`：`WorkflowExecutionContext.TeamId` → 查本团队 managed `KnowledgeGraphs` id 集 → 过滤入参 → `_graphSearchService.SearchAsync(validIds, query, top)` → 映射 hit：`Text` 取 result 中该命中对应段落（或直接用 `Contents[i]` + neighbors 拼接，以实现最简为准，注释说明））

- [ ] **Step 3: Executor**（镜像 `KnowledgeSearchNodeExecutor.cs` 逐段）：`NodeType => NodeTypes.KnowledgeGraphSearch`；配置 `{ "graphId": 1, "topK": 5 }`（**v1 仅静态配置，不做 graphId 输入变量绑定**，与 wiki 的差异点在类 remarks 注明）；输入必填 `query`；`topK` `Math.Clamp(1, 50)`；输出 `{query, count, hits: [{kgId, nodeId, name, entityType, description, score}], contents, text}`（JsonObject 组装照 wiki executor 逐行风格）。

- [ ] **Step 4: Guard + 注册**——`KnowledgeGraphSearchGuard.EnsureGraphsBelongToTeamAsync`（镜像 `KnowledgeSearchWikiGuard.cs:18-68`：遍历 `NodeTypes.KnowledgeGraphSearch` 节点收集 `config.graphId`，非本团队 managed → 400「知识图谱检索节点引用了不存在或不属于本团队的知识图谱：…」）；在 wiki guard 的调用点同点登记；`WorkflowServiceCollectionExtensions` 照 :45 加 executor 注册行。

- [ ] **Step 5: 验证 + 提交**（build Workflow.Core 与 MoAI.App.Workflow 两工程；既有 workflow 测试不回归：`dotnet test tests/MoAI.App.Workflow.Tests/MoAI.App.Workflow.Tests.csproj`）

```bash
git add src/app/workflow
git commit -m "feat(workflow): kgSearch 知识图谱检索节点——静态选图 + 向量检索 + 子图文本输出"
```

---

### Task 9: 前端（settings/应用配置/工作流面板/i18n）

**⚠️ 前置：syncapi**——本任务依赖 Kiota 客户端更新（新增 `PUT /{id}/embedding-config`、`POST /{id}/search`、model-options 响应扩展、`SaveAppAgentConfigCommand.GraphIds`、`AppAgentConfigEntity.GraphIds` 回显）。**执行本任务前需重启带新构建的后端**（与用户确认后：`cd src/MoAI && dotnet build && dotnet run` 起开发实例或替换用户实例），然后 `cd ui && npm run syncapi`。

**Files:**
- Modify: `ui/src/api/knowledgeGraph.ts`（模型选项类型加 embeddingModels；`updateKnowledgeGraphEmbeddingConfig(kgId, payload)`；绑定数据源过滤 `mode === 'managed'` 的辅助函数）
- Modify: 应用配置页图谱多选（先 `grep -rn "wikiIds" ui/src/pages/apps ui/src/pages/teams/apps --include="*.tsx" -l` 找 WikiIds 选择器组件镜像，含保存 payload 与回显）
- Modify: `ui/src/pages/knowledgegraph/KnowledgeGraphSettings.tsx`（embedding 配置表单段，镜像 `WikiDetail.tsx:366-422`：模型下拉（`getKnowledgeGraphModelOptions().embeddingModels`）+ 维度 AutoComplete + 保存按钮；Admin+ 才可编辑；member 只读提示）
- Modify: `ui/src/pages/teams/apps/workflow/NodeForm.tsx`（`case 'kgSearch'`：`NodeKeySection` + 图谱下拉（拉 `getKnowledgeGraphs(teamId)` 过滤 managed，AutoComplete 静态值写 `settings.graphId`，镜像 `KnowledgeWikiSelect` 但去掉变量绑定组）+ `KnowledgeQueryBinding` 复用 + `OutputsSection`）；`NodePanel.tsx`（ai 组 types 加 `'kgSearch'`）；`constants.ts`（约束行 + 模板行：icon `'🕸️'`、`name: '知识图谱检索'`、`color: '#722ed1'`、inputs 只 query、outputs 五项、`settings: { graphId: undefined, topK: 5 }`）
- Modify: `ui/src/i18n/locales/zh-CN/common.json`、`en-US/common.json`（workflowDesigner.nodeKnowledgeGraphSearch(+Desc)、knowledgegraph.embedding.* 段、应用配置图谱选择文案——双语同步）

- [ ] **Step 1: syncapi**（前置确认后）Run: `cd ui && npm run syncapi`；确认 `ui/src/api/client/` 生成含新端点。**禁止手改 client 生成物。**
- [ ] **Step 2-5:** 按 Files 顺序实现；每处先读目标文件最新版再改（工作区可能有并行 WIP，目标文件若已有未提交改动→BLOCKED 上报）。
- [ ] **Step 6: 验证**：`cd ui && npm run typecheck && npm run lint && npm run test`（全绿；无关既有失败注明依据）
- [ ] **Step 7: Commit**（git add 逐个核对；一笔提交）

```bash
git commit -m "feat(ui): 图谱向量化配置 + 应用绑定图谱多选 + 工作流知识图谱检索节点面板"
```

---

### Task 10: E2E `local-dev/kg-search-e2e.mjs`（KGS-S1~S9）

**Files:**
- Create: `local-dev/kg-search-e2e.mjs`

- [ ] **Step 1: 写脚本**——骨架照 `kg-e2e.mjs` 最新版（BASE 默认、check/skip、rsa 登录、建团、KG enabled 检测）；**embedding 桩渠道**照 `wiki-recall-e2e.mjs` 的本地 OpenAI 兼容桩手法（先读该脚本提取：桩服务起法、渠道/模型创建 API、模型 id 拿到方式），场景：

| 场景 | 内容 |
|---|---|
| KGS-S1 | 桩渠道+embedding 模型创建 → `PUT /api/knowledge-graph/{id}/embedding-config` 200（维度 1024）；回读图谱详情确认两字段 |
| KGS-S2 | 建节点 → 轮询 `POST /{id}/search`（≤10 次、间隔 500ms）直到命中，断言 hits 含节点名、neighbors 结构正确 |
| KGS-S3 | 改节点名 → 轮询检索：新名命中、旧名不命中（幂等更新生效） |
| KGS-S4 | 删节点 → 轮询检索至不命中 |
| KGS-S5 | minScore=0.999999 → 空结果；minScore 缺省 → 命中 |
| KGS-S6 | 未配模型的第二张图 search → 409 且文案含「向量化」 |
| KGS-S7 | embedding-config 参数校验（维度 0 → 400；模型未授权 → 400） |
| KGS-S8 | 应用绑定：saveAppAgentConfig 带他团队 graphId → 400；带本团队接入图 → 400；合法 → 200 且详情回显 |
| KGS-S9 | 工作流：仿 `workflow-e2e.mjs` WF-18（draft/publish/debug-run），kgSearch 节点 config `{graphId, topK:3}`，断言节点输出 count≥1、text 非空；config 引用他团队 graphId → draft 保存 400 |

收尾：删图/删应用/禁团队；`PASS/FAIL` 计数 + exit code；头注释写明场景映射与前置（新构建 + Memgraph + KG_ENABLED + RabbitMQ）。

- [ ] **Step 2: 静态验证**：`node --check local-dev/kg-search-e2e.mjs`；不可达地址跑确认 SKIP 路径 exit 0
- [ ] **Step 3: 运行验证**（需带新构建的后端 + Memgraph + RabbitMQ；若后端未重启则标注「待运行」并跳过本步）
- [ ] **Step 4: Commit**

```bash
git add local-dev/kg-search-e2e.mjs
git commit -m "test(e2e): KGS 图检索 E2E——向量化同步/检索/过滤/绑定校验/工作流节点（本地桩零 SKIP）"
```

---

### Task 11: 文档四件套 + 登记

**Files:** `docs/knowledgegraph/{bdd,tdd,sdd,sop}.md`、`AGENTS.md`、`docs/rounds-log.md`

- [ ] **Step 1:** **改前重读全部目标文档最新版**（并行会话守则）。bdd 补「Feature: 图检索消费层（KGS-S*）」节（场景从 E2E 反推，未运行的标「E2E 已就绪待运行」）；tdd 补 KT/KGS 映射并执行单测记录实际数；sdd 更新消费端章节（SP-A 一期 ✅ 项）；sop 补「向量检索排障」（未配模型提示、向量缺失=不命中、MQ 死信排查）。
- [ ] **Step 2:** AGENTS.md 验证命令段加 `node local-dev/kg-search-e2e.mjs` 行（KT 行附近）。
- [ ] **Step 3:** rounds-log 补 SP-A 轮次记录（commits 清单 + 审查结论 + E2E 状态）。
- [ ] **Step 4:** 全量验证：`dotnet test tests/MoAI.KnowledgeGraph.Tests/... && dotnet test tests/MoAI.App.Workflow.Tests/... && cd ui && npm run typecheck && npm run lint && npm run test` +（后端已重启时）`node local-dev/kg-search-e2e.mjs`。
- [ ] **Step 5: Commit**

```bash
git add docs/knowledgegraph docs/rounds-log.md AGENTS.md
git commit -m "docs(knowledgegraph): 图检索消费层四件套场景/映射/运维登记"
```

---

## 自审记录

1. **Spec 覆盖**：设计 §4 存储/模型/触发/清理→T1/2/3；§5 检索服务→T4；§5 API→T5；§6 绑定/工具/节点→T6/7/8；§7 前端→T9；§9 测试→T4 单测+T10 E2E；§10.3 文档→T11。无缺口。
2. **占位符扫描**：T2 Step1/T3 Step5/T6/T8/T9 的「照抄/镜像 <文件:行>」均指向仓库内可运行的既有代码并给出逐点差异清单，属完整规格；T10 场景表 + 骨架引用与 KT 计划同规格。
3. **类型一致性**：`IGraphSearchService.SearchAsync(graphIds, query, topPerGraph, minScore, ct)` ↔ T5 Handler/T7 Provider/T8 Client 三处调用；`KgEmbeddingVectorRecord` 字段 ↔ T2 定义/T3 构造；`WorkflowGraphSearchHit(KgId, NodeId, Name, EntityTypeName, Description, Score, Text)` ↔ T7 payload 命名差异（graphId vs KgId）为序列化别名，已在 T7 明确；`GraphIds` 字符串列 ↔ `ParseGraphIds` 双装配点 ↔ 快照三处同步。
4. **已识别风险**：T9 依赖后端重启 + syncapi（执行时与用户确认）；VectorData 集合删除方法名以编译器为准（T2 注明）；节点单读接口可能需补（T3 Step2 有判定与实现指引）。
