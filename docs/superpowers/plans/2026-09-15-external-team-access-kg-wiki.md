# 外部团队级资源访问（AppIds 清理 + 知识图谱/知识库外部接口）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 AppIds 残留并切换外部 token 为团队级授权；新增 `/api/external/knowledge-graph` 与 `/api/external/wiki` 外部接口（应用 token），支持图谱实体/关系/模型增删查改与批量导入、知识库文件上传与向量化全链路。

**Architecture:** 方案 A 薄适配层——外部 Controller 从 `HttpContext.Items` 取 `ExternalTokenContext`（TeamId），各模块 `Core` 层新增外部授权器（校验资源属 token 团队）与外部 Command/Handler（复用 `IKnowledgeGraphStore`、`IStorageService`、WorkerTask/MQ 向量化管线），模块间仅新增 `*.Api → MoAI.App.Shared` 项目引用。

**Tech Stack:** .NET 10 模块化单体（Maomi + MediatR + EF Core + openCypher/Memgraph + pgvector + RabbitMQ），外部 JWT（`ExternalJwt` scheme，`/api/external` 前缀由 `ExternalAuthenticationMiddleware` 统一认证）。

**规范前置：** 动手前读 `docs/cqrs-conventions.md`；请求模型 `IModelValidator<T>`；`BusinessException` 必须显式 `StatusCode`；Handler 禁止注入 `IUserContextProvider`；DI 用 `[InjectOnScoped]`（attribute 类存在时按模块惯例）；新代码时间 `DateTimeOffset`。

**设计真源：** `docs/superpowers/specs/2026-09-15-kg-external-api-design.md`

---

## Task 1: AppIds 残留清理（先让 build 回绿）

**Files:**
- Modify: `src/app/MoAI.App.Shared/Models/ExternalTokenContext.cs`（删 `AppIds`/`IsAppAuthorized`）
- Modify: `src/app/MoAI.App.Core/Services/ExternalTokenProvider.cs`（删 appids claim 签发/解析）
- Modify: `src/app/MoAI.App.Core/Handlers/ExternalTokenCommandHandler.cs`（用户 token 改为"appId 属接入点团队的外部应用"）
- Modify: `src/app/MoAI.App.Core/Handlers/RefreshExternalTokenCommandHandler.cs`、`QueryAccessAppsCommandHandler.cs`、`QueryExternalAuthorizedAppsCommandHandler.cs`
- Delete: `src/app/MoAI.App.Core/Handlers/AccessAppAuthorizedAppsValidator.cs`
- Modify: `src/app/MoAI.App.Shared` 的 AccessApp Create/Update Command/Response DTO（删 AppIds 字段）及 `CreateAccessAppCommandHandler`/`UpdateAccessAppCommandHandler`
- Modify: `src/app/MoAI.App.Api/ExternalAuthenticationMiddleware.cs`（chat 校验改团队级）
- Modify: `local-dev/` 下引用 appIds 的 e2e 脚本（`grep -rln "appIds\|AppIds" local-dev/`）
- Modify: `ui/src` 中接入点表单的应用多选（`grep -rln "appIds" ui/src`）

- [ ] **Step 1: 全局摸底**

```bash
grep -rn "AppIds" src/ --include="*.cs" | grep -v obj
grep -rln "appIds" local-dev/ ui/src
dotnet build src/MoAI/MoAI.csproj 2>&1 | tail -20   # 当前 5 error，作为清理清单
```

- [ ] **Step 2: 清理 Shared 与 token 体系**

`ExternalTokenContext.cs`：删除 `AppIds` 属性与 `IsAppAuthorized` 方法；类注释中"授权应用范围"段落删除。
`ExternalTokenProvider.cs`：删除 `ClaimAppIds` 常量（若仅此处用）及签发处 `accessClaims.Add(... ClaimAppIds ...)`、解析处 `appIds` 相关代码（约 L55、L127、L148、L220 附近）。

- [ ] **Step 3: 用户 token / chat 校验改团队级**

`ExternalTokenCommandHandler.cs` L95 附近：原 `!accessApp.AppIds.Contains(request.AppId.Value)` 改为查询应用归属团队：

```csharp
var appBelongsToTeam = await _databaseContext.Apps
    .AnyAsync(x => x.Id == request.AppId.Value && x.TeamId == accessApp.TeamId && x.IsExternal, cancellationToken);
if (!appBelongsToTeam)
{
    throw new BusinessException("应用不属于该接入点所在团队.") { StatusCode = 403 };
}
```

（`Apps` 集 合名/`IsExternal` 字段以 `ExternalAppAccessValidator.EnsureUsable` 中的实际用法为准，保持一致。）
`ExternalAuthenticationMiddleware.cs`：chat 端点原 `tokenContext.IsAppAuthorized(appId)` 分支改为注入 `DatabaseContext` 查 `appId 属 token.TeamId 且应用可用（复用 ExternalAppAccessValidator.EnsureUsable 语义）`。用户 token 同理按团队校验。
`QueryExternalAuthorizedAppsCommandHandler.cs`：改为返回"该团队全部已发布未禁用的外部应用"（查 `Apps where TeamId == context.TeamId`），响应模型不变。

- [ ] **Step 4: AccessApp CRUD 与 e2e/UI 清理**

删除 `AccessAppAuthorizedAppsValidator.cs`；Create/Update Handler 及 DTO 删 AppIds；`QueryAccessAppsCommandHandler.cs` L49 的 `AppIds` 投影删除。
e2e 脚本：把"绑定 appIds → 403"类断言改为团队级语义（任意团队内外部应用均可）。
UI：删除接入点表单应用多选与展示列；文案 `t()` zh/en 同步删。

- [ ] **Step 5: 验证**

```bash
dotnet build src/MoAI/MoAI.csproj    # 0 error
cd ui && npm run typecheck && npm run lint
# 后端运行中：
node local-dev/external-app-e2e.mjs   # 若存在；否则跑 feishu-e2e.mjs 冒烟
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "refactor(external): 移除接入点 AppIds 白名单，外部授权切换为团队级资源访问"
```

---

## Task 2: 知识图谱 Shared 层外部 Command/DTO

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/External/ExternalKnowledgeGraphCommands.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/External/ExternalKnowledgeGraphQueries.cs`

外部 Command 统一携带团队身份，不依赖 `MoAI.App.Shared`：

```csharp
namespace MoAI.KnowledgeGraph.External;

/// <summary>
/// 外部调用方身份（应用 token 解析后由 Controller 填充）.
/// </summary>
public class ExternalGraphCaller
{
    public long TeamId { get; init; }
    public Guid AccessAppId { get; init; }
}
```

- [ ] **Step 1: 定义命令**

同文件内定义（均 `IRequest<T>` + `IModelValidator<T>`，校验规则复制自对应内部 Command 的 `Validate`）：

- `QueryExternalGraphsCommand(caller) : IRequest<QueryExternalGraphsResponse>`（响应 `List<ExternalGraphItem>{ Id, Name, Description, Mode, NodeCount, EdgeCount }`——计数可先返回 0/省略，与 `QueryKnowledgeGraphsCommandHandler` 返回结构对齐取舍）
- `QueryExternalGraphSchemaCommand(caller, KnowledgeGraphId)`
- `QueryExternalNodesCommand(caller, KnowledgeGraphId, EntityTypeId?, Keyword?, PageNo=1, PageSize=20)`、`QueryExternalNodeCommand(caller, KnowledgeGraphId, NodeId)`、`QueryExternalNodeNeighborsCommand(caller, KnowledgeGraphId, NodeId, Limit=50)`
- `QueryExternalEdgesCommand(caller, KnowledgeGraphId, RelationTypeId?, NodeId?, PageNo, PageSize)`、`QueryExternalEdgeCommand(...)`
- `CreateExternalEntityTypeCommand(caller, KnowledgeGraphId, Name, Color, Description?)`、`UpdateExternalEntityTypeCommand(caller, KnowledgeGraphId, TypeId, Name?, Color?, Description?)`、`DeleteExternalEntityTypeCommand(caller, KnowledgeGraphId, TypeId)`
- `CreateExternalRelationTypeCommand(caller, KnowledgeGraphId, Name, SourceTypeId, TargetTypeId, Direction, Description?)`、`Update/DeleteExternalRelationTypeCommand`（字段以内部 `CreateKnowledgeGraphRelationTypeCommand` 为准照抄）
- `CreateExternalNodeCommand(caller, KnowledgeGraphId, EntityTypeId, Name, Description?) : IRequest<SimpleString>`
- `UpdateExternalNodeCommand(caller, KnowledgeGraphId, NodeId, EntityTypeId, Name, Description?)`、`DeleteExternalNodeCommand(caller, KnowledgeGraphId, NodeId)`
- `CreateExternalEdgeCommand(caller, KnowledgeGraphId, RelationTypeId, SourceNodeId, TargetNodeId) : IRequest<SimpleString>`、`UpdateExternalEdgeCommand(...)`、`DeleteExternalEdgeCommand(...)`
- `CreateExternalNodesBatchCommand(caller, KnowledgeGraphId, Items: List<ExternalNodeInput{EntityTypeId,Name,Description?}>)`——Validate: `Items` 非空且 `Count <= 200`
- `CreateExternalEdgesBatchCommand(caller, KnowledgeGraphId, Items: List<ExternalEdgeInput{RelationTypeId,SourceNodeId,TargetNodeId}>)`——同上限 200
- 响应 `ExternalBatchResponse { SuccessCount, FailedCount, Results: List<ExternalBatchItemResult{ Index, Ok, Message }> }`

- [ ] **Step 2: build 通过后 Commit**

```bash
dotnet build src/MoAI/MoAI.csproj && git add -A && git commit -m "feat(kg): 外部接口 Shared 层命令与 DTO"
```

---

## Task 3: KG Core 外部授权器 + Store 批量方法 + 外部 Handlers

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/IExternalKnowledgeGraphAuthorizer.cs`（+ 实现 `ExternalKnowledgeGraphAuthorizer`）
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/IKnowledgeGraphStore.cs`、`CypherKnowledgeGraphStore.cs`（新增批量方法）
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Handlers/External/`（按命令分文件，类名 = 命令名 + Handler）
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/KnowledgeGraphCoreModule.cs`（DI 注册）

- [ ] **Step 1: 外部授权器**

```csharp
public interface IExternalKnowledgeGraphAuthorizer
{
    /// <summary>校验图谱属于外部调用方团队；不存在或跨团队 404；connected 且需写时 409.</summary>
    Task<KnowledgeGraphEntity> AuthorizeAsync(long knowledgeGraphId, long teamId, bool write, CancellationToken ct);
}

public class ExternalKnowledgeGraphAuthorizer : IExternalKnowledgeGraphAuthorizer
{
    // 注入 DatabaseContext；查 KnowledgeGraphs（软删自动过滤）
    // 不存在或 TeamId != teamId -> new BusinessException("知识图谱不存在.") { StatusCode = 404 }
    // write && Mode == connected -> new BusinessException("接入模式图谱为只读.") { StatusCode = 409 }
}
```

DI 注册方式照抄 `KnowledgeGraphCoreModule.cs` 中 `IKnowledgeGraphAuthorizer` 的注册写法。

- [ ] **Step 2: Store 批量方法（UNWIND 单事务）**

`IKnowledgeGraphStore` 新增：

```csharp
Task<int> CreateNodesBatchAsync(long kgId, IReadOnlyList<KnowledgeGraphNodeInput> nodes, CancellationToken ct);
Task<int> CreateEdgesBatchAsync(long kgId, IReadOnlyList<KnowledgeGraphEdgeInput> edges, CancellationToken ct);
```

（`KnowledgeGraphNodeInput{EntityTypeId,Name,Description}`、`KnowledgeGraphEdgeInput{RelationTypeId,SourceNodeId,TargetNodeId}` 定义在 Store 同文件或 Models。）
`CypherKnowledgeGraphStore` 实现：参考现有 `CreateNodeAsync/CreateEdgeAsync` 的参数写法（kgId 属性、`$` 参数），用 `UNWIND $items AS item CREATE (n:KG_Node {kgId:$kgId, id: randomuuid, ...})` 单语句批量插入，返回插入条数；edge 需 `MATCH` 两端节点（限 `kgId`）后 `CREATE (s)-[r]->(t)`，端点缺失条目自然不创建——Handler 在调用前先做端点存在预检（见 Step 3），Store 返回实际条数用于一致性断言。

- [ ] **Step 3: 外部 Handlers**

每个 Handler 模式（以节点为例，其余照此 + 各自业务校验）：

```csharp
public class CreateExternalNodeCommandHandler : IRequestHandler<CreateExternalNodeCommand, SimpleString>
{
    // 注入 DatabaseContext / IExternalKnowledgeGraphAuthorizer / IKnowledgeGraphStore
    public async Task<SimpleString> Handle(CreateExternalNodeCommand request, CancellationToken ct)
    {
        await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, ct);
        var typeExists = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.Id == request.EntityTypeId && x.KnowledgeGraphId == request.KnowledgeGraphId, ct);
        if (!typeExists) throw new BusinessException("实体类型不存在.") { StatusCode = 400 };
        var node = await _store.CreateNodeAsync(request.KnowledgeGraphId, request.EntityTypeId, request.Name, request.Description ?? string.Empty, ct);
        return new SimpleString { Value = node.Id };
    }
}
```

- 查询类 Handler：授权（write:false）后直接调 Store 对应方法；schema 查询参考 `QueryKnowledgeGraphSchemaCommandHandler` 的 EF+计数逻辑。
- 类型 CRUD：参考 `CreateKnowledgeGraphEntityTypeCommandHandler` 等（名称同图谱内唯一校验、删除时计数清零校验照抄，成员校验替换为外部授权器）。
- 批量 Handler：先逐条校验（类型存在、Name 非空≤200、边端点节点存在于该图谱——用 Store `ListNodesAsync` 或逐个 `GetNodeAsync`；实体重复名不校验唯一），任一失败 → `BusinessException` 400，消息含首个失败序号与原因，不调 Store（整批拒绝语义）；全部通过 → 调批量方法并返回 `ExternalBatchResponse{ SuccessCount = n }`。
- `QueryExternalGraphsCommandHandler`：`KnowledgeGraphs.Where(TeamId == caller.TeamId && Mode == managed)`，connected 图谱不对外暴露。

- [ ] **Step 4: build + Commit**

```bash
dotnet build src/MoAI/MoAI.csproj && git add -A && git commit -m "feat(kg): 外部授权器、批量写入与外部 Handler"
```

---

## Task 4: KG Api 外部 Controller

**Files:**
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Api/MoAI.KnowledgeGraph.Api.csproj`（新增 `ProjectReference` → `..\..\app\MoAI.App.Shared\MoAI.App.Shared.csproj`）
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Api/Controllers/ExternalKnowledgeGraphController.cs`

- [ ] **Step 1: Controller**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.App.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.External;

/// <summary>
/// 知识图谱外部开放接口：应用 token（团队级授权）维护实体/关系/模型.
/// 认证由 ExternalAuthenticationMiddleware 统一处理（/api/external 前缀）.
/// </summary>
[ApiController]
[Route("/external/knowledge-graph")]
[AllowAnonymous]
public class ExternalKnowledgeGraphController : ControllerBase
{
    private readonly IMediator _mediator;
    public ExternalKnowledgeGraphController(IMediator mediator) => _mediator = mediator;

    private ExternalGraphCaller RequireCaller()
    {
        var ctx = HttpContext.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var v) ? v as ExternalTokenContext : null;
        if (ctx == null) throw new BusinessException("外部 token 无效.") { StatusCode = 401 };
        if (ctx.SubjectType != UserType.ExternalApp) throw new BusinessException("该接口仅支持应用 token.") { StatusCode = 403 };
        return new ExternalGraphCaller { TeamId = ctx.TeamId, AccessAppId = ctx.AccessAppId!.Value };
    }

    // 端点（均先 RequireCaller() 再 _mediator.Send）：
    [HttpPost("list")]                                 // QueryExternalGraphsCommand
    [HttpGet("{kgId:long}/schema")]                    // QueryExternalGraphSchemaCommand
    [HttpPost("{kgId:long}/nodes/list")]               // QueryExternalNodesCommand
    [HttpGet("{kgId:long}/nodes/{nodeId}")]            // QueryExternalNodeCommand
    [HttpGet("{kgId:long}/nodes/{nodeId}/neighbors")]  // QueryExternalNodeNeighborsCommand
    [HttpPost("{kgId:long}/nodes")]                    // CreateExternalNodeCommand
    [HttpPut("{kgId:long}/nodes/{nodeId}")]            // UpdateExternalNodeCommand
    [HttpDelete("{kgId:long}/nodes/{nodeId}")]         // DeleteExternalNodeCommand
    [HttpPost("{kgId:long}/nodes/batch")]              // CreateExternalNodesBatchCommand
    [HttpPost("{kgId:long}/edges/list")]               // QueryExternalEdgesCommand
    [HttpGet("{kgId:long}/edges/{edgeId}")]            // QueryExternalEdgeCommand
    [HttpPost("{kgId:long}/edges")]                    // CreateExternalEdgeCommand
    [HttpPut("{kgId:long}/edges/{edgeId}")]            // UpdateExternalEdgeCommand
    [HttpDelete("{kgId:long}/edges/{edgeId}")]         // DeleteExternalEdgeCommand
    [HttpPost("{kgId:long}/edges/batch")]              // CreateExternalEdgesBatchCommand
    [HttpPost("{kgId:long}/entity-types")]             // CreateExternalEntityTypeCommand
    [HttpPut("{kgId:long}/entity-types/{typeId:long}")]// UpdateExternalEntityTypeCommand
    [HttpDelete("{kgId:long}/entity-types/{typeId:long}")] // DeleteExternalEntityTypeCommand
    [HttpPost("{kgId:long}/relation-types")]           // CreateExternalRelationTypeCommand
    [HttpPut("{kgId:long}/relation-types/{typeId:long}")]
    [HttpDelete("{kgId:long}/relation-types/{typeId:long}")]
}
```

`ExternalAuthDefaults.TokenContextItemKey` 以 `MoAI.App.Shared/Services/ExternalAuthDefaults.cs` 实际常量名为准；若 middleware 的 Items key 是硬编码，则把常量提为 public 后引用。请求体 DTO 用 Task 2 的输入部分（`Items`/字段直接作为 body），路由参数回填 `KnowledgeGraphId`。确认 Controller 被模块扫描（Maomi 模块注册参照 `KnowledgeGraphController` 所在模块的接入方式，无需额外配置则跳过）。

- [ ] **Step 2: build + 手工冒烟 + Commit**

```bash
dotnet build src/MoAI/MoAI.csproj
git add -A && git commit -m "feat(kg): /api/external/knowledge-graph 外部接口"
```

---

## Task 5: kg-external-e2e.mjs

**Files:**
- Create: `local-dev/kg-external-e2e.mjs`

- [ ] **Step 1: 编写脚本**

复制 `local-dev/external-app-e2e.mjs`（若无则取 feishu-e2e.mjs）的登录/建团队/建接入点/key 换应用 token 骨架；KG 部分参照 `kg-e2e.mjs` 的内部 API（管理员登录创建 managed 图谱 + connected 图谱，供外部脚本消费）。断言：

1. `/external/knowledge-graph/list` 只含本团队 managed 图谱。
2. 对另一团队图谱任意写 → 404；随机 id → 404。
3. 实体类型/关系类型 CRUD → 节点 CRUD/分页/邻接 → 边 CRUD/分页 → schema 全 200 且数据正确。
4. `nodes/batch` 3 条合法 → 成功落库；含 1 条非法类型 → 400 且已存在的批次不变（查 list 计数不变）；201 条 → 400。
5. connected 图谱写 → 409。
6. 用内部用户 token（admin 登录 token）直接调外部接口 → 401/403。

- [ ] **Step 2: 运行至全绿（后端运行中）**

```bash
node local-dev/kg-external-e2e.mjs
```

- [ ] **Step 3: Commit**

```bash
git add local-dev/kg-external-e2e.mjs && git commit -m "test(e2e): 知识图谱外部接口 e2e"
```

---

## Task 6: Wiki 外部接口（Shared + Core + Api）

**Files:**
- Create: `src/wiki/MoAI.Wiki.Shared/External/ExternalWikiCommands.cs`
- Create: `src/wiki/MoAI.Wiki.Core/Services/IExternalWikiAuthorizer.cs`（+实现）
- Create: `src/wiki/MoAI.Wiki.Core/Handlers/External/`（分文件）
- Modify: `src/wiki/MoAI.Wiki.Api/MoAI.Wiki.Api.csproj`（引用 App.Shared）+ Wiki Core Module DI
- Create: `src/wiki/MoAI.Wiki.Api/Controllers/ExternalWikiController.cs`

- [ ] **Step 1: Shared 命令**（同 `ExternalGraphCaller` 模式定义 `ExternalWikiCaller{TeamId,AccessAppId}`；校验规则照抄内部 Command）
  - `QueryExternalWikisCommand`、`QueryExternalWikiCommand(WikiId)`
  - `UpdateExternalWikiEmbeddingCommand(WikiId, EmbeddingModelId, EmbeddingDimensions)`
  - `QueryExternalWikiDocumentsCommand(WikiId, PageNo, PageSize, Query?, IsEmbedding?, IncludeFileTypes?/ExcludeFileTypes?)`
  - `PreUploadExternalWikiDocumentCommand(WikiId, FileName, ContentType, FileSize, SHA256)` / `CompleteExternalWikiDocumentCommand(WikiId, IsSuccess, FileId, FileName)` / `DeleteExternalWikiDocumentsCommand(WikiId, DocumentIds)` / `RenameExternalWikiDocumentCommand(WikiId, DocumentId, FileName)`
  - `GetExternalWikiDocumentContentCommand(WikiId, DocumentId)`
  - `ExtractExternalDocumentCommand(WikiId, DocumentId)`、`PartitionExternalDocumentCommand(WikiId, DocumentId, SplitMode, ChunkSize, ChunkOverlap, SizeUnit?, OverlapUnit?, TokenEncodingOrModel?)`、`AiPartitionExternalDocumentCommand(WikiId, DocumentId, AiModelId, PromptTemplate?)`
  - `EmbedExternalDocumentCommand(WikiId, DocumentId, IsEmbedSourceText, IsEmbedMetadata) : IRequest<SimpleString(TaskId)>`、`QueryExternalDocumentEmbeddingCommand(WikiId, DocumentId)`
- [ ] **Step 2: 授权器**

```csharp
public class ExternalWikiAuthorizer : IExternalWikiAuthorizer
{
    // 查 Wikis：不存在或 TeamId != caller.TeamId -> 404；返回实体
}
```

- [ ] **Step 3: 外部 Handlers**——逐个打开内部同名 Handler，复制 `Handle` 主体，仅两处改法：`EnsureMemberAsync`/ITeamService 角色校验 → `_externalWikiAuthorizer.AuthorizeAsync(...)`；`_userContextProvider` 的 UserId 引用（审计字段等）→ 用 `request.Caller.AccessAppId` 派生或删除该分支（框架自动审计字段不手赋）。涉及：`PreUploadWikiDocumentCommandHandler`（IStorageService 预签名 + FileStoreHelper）、`CompleteWikiDocumentCommandHandler`、`EmbeddingDocumentCommandHandler`（WorkerTask + `IMessagePublisher`，注意模型授权校验 `UpdateWikiEmbeddingCommandHandler` L79-93 的模式）、`ExtractDocumentContentCommandHandler`、`PartitionDocumentCommandHandler`、`AiPartitionDocumentCommandHandler`、查询类。
- [ ] **Step 4: Controller**——路由 `/external/wiki`，`RequireCaller()` 同 Task 4；端点映射按 spec 3.1 表（14 个）。
- [ ] **Step 5: build + Commit**

```bash
dotnet build src/MoAI/MoAI.csproj && git add -A && git commit -m "feat(wiki): /api/external/wiki 外部接口（上传/提取/切割/向量化）"
```

---

## Task 7: wiki-external-e2e.mjs

**Files:**
- Create: `local-dev/wiki-external-e2e.mjs`

- [ ] **Step 1: 编写**——骨架取 `wiki-embedding-e2e.mjs`（建库/绑模型/上传三段式/轮询），认证换成 external-app-e2e 的应用 token。断言：list/详情/embedding-config；其他团队 wiki → 404；preupload→PUT→complete（含 IsExist 秒传分支、非法格式 400）；列表/重命名/删除/内容；extract→partition→embedding 触发→轮询 `TaskState=Successful` 且 `embeddingCount>0`；未提取直接 embedding → 400；重复触发进行中 → 409。
- [ ] **Step 2: 运行至全绿**（需 storage/AI 渠道环境同 wiki-embedding-e2e）→ Commit。

```bash
node local-dev/wiki-external-e2e.mjs
git add local-dev/wiki-external-e2e.mjs && git commit -m "test(e2e): 知识库外部接口 e2e"
```

---

## Task 8: 文档同步与全量回归

**Files:**
- Modify: `docs/knowledgegraph/bdd.md`（新增场景，编号顺延不复用）、`sdd.md`、`tdd.md`、`sop.md`
- Modify: `docs/wiki/bdd.md` 等（同上）
- Modify: app/外部接入相关文档（AppIds 移除、团队级授权）与 `docs/README.md` 地图
- Modify: `docs/rounds-log.md`（新轮次记录与证据）
- 遵守 `docs/DOC-STANDARD.md`：先读再写、分层链接不重复。

- [ ] **Step 1: 更新文档四件套 + rounds-log。
- [ ] **Step 2: 全量回归**

```bash
dotnet build src/MoAI/MoAI.csproj
cd ui && npm run typecheck && npm run lint && npm run test
node local-dev/kg-e2e.mjs && node local-dev/wiki-e2e.mjs && node local-dev/kg-external-e2e.mjs && node local-dev/wiki-external-e2e.mjs && node local-dev/feishu-e2e.mjs
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "docs: 外部团队级资源访问（KG/Wiki 外部接口）四件套与轮次记录"
```

---

## Self-Review 结论

- spec 覆盖：§2 授权模型→Task 1/3/6；§3 接口清单→Task 2-4；§3.1→Task 6；§4 数据流→Task 3/6；§5 错误→授权器与批量 Handler；§6 验收→Task 5/7/8。无缺口。
- 占位符：无 TBD；"照抄内部 Handler"均给出精确文件与行为差异点，属代码库引用而非计划内引用。
- 类型一致性：`ExternalGraphCaller/ExternalWikiCaller`、`ExternalBatchResponse`、Store 批量签名在 Task 2/3/4 间一致。
