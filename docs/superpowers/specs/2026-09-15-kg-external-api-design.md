# 外部开放接口设计：应用接入团队级资源访问（2026-09-15）

> 状态：已确认。实现计划由 writing-plans 阶段产出。

## 1. 背景与目标

外部系统通过 access key（`access_app.Key`，`moai-ac-` 前缀）在 `POST /api/external/token` 换取应用 token（JWT，`UserType.ExternalApp`，携带 `TeamId`）。

**授权模型变更（用户已定）**：接入点不再做应用/资源白名单——`AccessAppEntity.AppIds` 已从实体删除，新建应用接入即代表**对该接入点所属团队的资源**拥有操作权。应用 token 对团队资源的权限等价团队 Admin（本设计覆盖：知识图谱、知识库）。

目标：

1. 修复 AppIds 残留（当前 build 5 个错误），外部 token 体系整体切换为团队级访问。
2. 应用 token 可对**其团队的全部 managed 知识图谱**维护实体（节点）、关系（边）、模型（实体类型/关系类型），含单条增删查改与批量导入。
3. 应用 token 可操作**其团队的知识库**：文件上传（预签名三段式）、提取/切割、向量化触发与状态查询、文档管理。

非目标：

- 不开放图谱/知识库目录级破坏操作（图谱本身不开放创建/删除/修改；知识库开放列表/详情/向量化配置，但不开放创建/删除知识库）。
- 不开放 connected 模式图谱的写操作（外部系统直写图库的场景维持轮次 70 的模式，平台只读转接）。
- 不引入图谱白名单或细粒度 scope 模型。

## 2. 授权模型

- **前置清理（必须，当前 build 已坏）**：完成 AppIds 残留清理——`ExternalTokenProvider`（claim 签发/解析）、`ExternalTokenContext.AppIds/IsAppAuthorized`、`ExternalTokenCommandHandler`、`RefreshExternalTokenCommandHandler`、`ExternalAuthenticationMiddleware`、`AccessAppAuthorizedAppsValidator`、AccessApp CRUD 的 DTO/前端多选、`QueryExternalAuthorizedAppsCommandHandler` 等中所有 AppIds 引用；用户 token 流程按"团队内任意 is_external 应用"重定义，chat 端点的 `appId ∈ AppIds` 校验改为"appId 属于 token 团队且外部可用"（复用 `ExternalAppAccessValidator.EnsureUsable` 加团队归属校验）。
- 外部图谱授权器 `IExternalKnowledgeGraphAuthorizer`（knowledgegraph 模块内）：
  - `AuthorizeAsync(kgId)`：图谱存在且 `TeamId == token.TeamId`、mode=managed。跨团队或不存在 → 404（对外不泄露存在性）；connected 写 → 409 只读。
- Handler 身份来源：现有节点/边/类型写 Handler 的用户依赖按 cqrs-conventions 走 `IUserIdContext`，外部路径由 Controller 组装 Command 时填充外部应用身份标识（accessAppId），禁止 Handler 注入 `IUserContextProvider`。

## 3. 外部接口清单

统一挂 `/api/external/knowledge-graph`，走 `ExternalAuthenticationMiddleware`（`/api/external` 前缀已拦截）+ `[ExternalAuthorize]`，仅应用 token（用户 token 不适用于本组接口）。错误体沿用外部接口 `{error:{message,type,code}}`。

| 端点 | 方法 | 说明 |
|---|---|---|
| `/list` | POST | 本团队 managed 图谱列表（含名称/统计） |
| `/{kgId}/schema` | GET | schema（实体类型+关系类型+计数） |
| `/{kgId}/entity-types`、`/{typeId}` | POST/PUT/DELETE | 模型：实体类型增删改 |
| `/{kgId}/relation-types`、`/{typeId}` | POST/PUT/DELETE | 模型：关系类型增删改 |
| `/{kgId}/nodes`、`/{nodeId}` | POST/GET/PUT/DELETE | 实体增删查改 |
| `/{kgId}/nodes/list` | POST | 实体分页列表（类型/关键词过滤） |
| `/{kgId}/nodes/{nodeId}/neighbors` | GET | 邻接查询 |
| `/{kgId}/edges`、`/{edgeId}` | POST/GET/PUT/DELETE | 关系增删查改 |
| `/{kgId}/edges/list` | POST | 关系分页列表 |
| `/{kgId}/nodes/batch` | POST | 批量建实体，≤200 条/次 |
| `/{kgId}/edges/batch` | POST | 批量建关系，≤200 条/次 |

请求/响应模型放 `MoAI.KnowledgeGraph.Shared`，实现 `IModelValidator<T>`；外部 DTO 独立定义，不直接复用内部 Command 类型（Controller 负责映射，避免内外耦合）。

### 3.1 知识库外部接口（`/api/external/wiki`，仅应用 token）

复用 wiki 模块现有 Handler（`Core/Handlers/`），文档上传走现有预签名三段式（外部客户端拿 `UploadUrl` 直传 MinIO，不经平台转发文件流）：

| 端点 | 方法 | 说明 |
|---|---|---|
| `/list` | POST | 本团队知识库列表 |
| `/{wikiId}` | GET | 详情（含向量化模型配置） |
| `/{wikiId}/embedding-config` | PUT | 配置向量化模型+维度（锁定后 409，同内部语义） |
| `/{wikiId}/documents/list` | POST | 文档分页列表（类型/向量化状态过滤） |
| `/{wikiId}/documents/preupload` | POST | 预上传：`{FileName,ContentType,FileSize,SHA256}` → `{FileId,IsExist,UploadUrl,Expiration}` |
| `/{wikiId}/documents/complete` | POST | 完成上传落库 `{IsSuccess,FileId,FileName}` |
| `/{wikiId}/documents` | DELETE | 批量删除文档（body DocumentIds） |
| `/{wikiId}/documents/{documentId}/content` | GET | 提取后的 markdown 内容 |
| `/{wikiId}/documents/{documentId}/rename` | PUT | 重命名 |
| `/{wikiId}/documents/{documentId}/extract` | POST | 触发内容提取 |
| `/{wikiId}/documents/{documentId}/partition` | POST | 普通切割（ChunkSize/Overlap 等） |
| `/{wikiId}/documents/{documentId}/ai-partition` | POST | AI 语义切割（需传 AiModelId，须为团队已授权模型） |
| `/{wikiId}/documents/{documentId}/embedding` | POST | 触发向量化（异步队列，返回 TaskId） |
| `/{wikiId}/documents/{documentId}/embedding` | GET | 向量化状态/进度（TaskState、embeddingCount），供轮询 |

不做：知识库创建/删除/更新、头像、rerank、元数据生成（metadata 依赖交互式策略选择，后续按需加）。外部 wiki 授权器 `IExternalWikiAuthorizer`：知识库存在且 `TeamId == token.TeamId`，否则 404；写操作 Handler 身份走 `IUserIdContext` 填充 accessAppId。向量化链路本身（WorkerTask + RabbitMQ 消费 + pgvector + AI 渠道）零改动，外部接口只是新的触发入口。

批量导入语义：

- 请求体内数组逐条跑与单条相同的校验（类型存在、名称非空、边端点存在等）；任一失败整批拒绝（图库单事务提交），响应返回失败条目的序号与原因。
- 幂等性本期不做 upsert；外部系统按返回结果重试。

## 4. 模块与数据流

```
外部系统 --(access key)--> POST /external/token --> 应用 token(TeamId claim)
外部系统 --(Bearer)--> ExternalKnowledgeGraphController (KnowledgeGraph.Api)
   --> ExternalTokenContextAccessor 取 TeamId
   --> IExternalKnowledgeGraphAuthorizer.AuthorizeAsync(kgId)
   --> 复用 Core/Handlers 的 Command（IUserIdContext 携带外部身份）
       - 节点/边: IKnowledgeGraphStore (openCypher, kgId 隔离)
       - 类型: EF --> kg_entity_type / kg_relation_type (PG)
批量: 新增 Batch Handler，循环复用校验逻辑 + Store 单事务
```

涉及改动：

- `src/app`：AppIds 残留清理（含用户 token / chat / 应用列表的团队级重定义）。
- `src/knowledgegraph`：`Shared` 外部 DTO/Command、`Core` 外部授权器 + 批量 Handler、`Api` 外部 Controller。
- `src/wiki`：`Shared` 外部 DTO、`Core` 外部授权器、`Api` 外部 Controller（复用现有 Handler）。
- `ui`：移除接入点表单中的应用多选及其展示（若有）。
- 文档：`docs/knowledgegraph/bdd.md`、`docs/wiki/bdd.md` 新增场景（新编号不复用）、各模块 sdd/tdd/sop 同步，app/外部接入相关文档同步 AppIds 移除，`docs/rounds-log.md` 记录轮次。

## 5. 错误与边界

- 跨团队/不存在：404；connected 写：409 只读。
- token 过期：沿用外部体系 401 + refresh。
- 名称冲突、类型不存在等业务错误：与内部接口同语义，`BusinessException` 显式 StatusCode。
- 批量超 200 条：400，提示上限。

## 6. 测试与验收

新增 `local-dev/kg-external-e2e.mjs`：

1. key 换应用 token；`/list` 返回本团队 managed 图谱（不含其他团队、不含 connected）。
2. 其他团队图谱操作 → 404；不存在 → 404。
3. 实体/关系/模型全量 CRUD + 分页 + 邻接 + schema 全绿。
4. 批量导入：成功批次落库；含非法条目整批回滚且返回逐条原因；超限 400。
5. connected 图谱写 → 409。
6. 用户 token（非应用 token）访问 → 403。

新增 `local-dev/wiki-external-e2e.mjs`（模板：wiki-embedding-e2e.mjs + external-app-e2e.mjs）：

1. `/list`、详情、embedding-config 全绿（其他团队 wiki → 404）。
2. preupload → PUT 预签名 URL → complete 全链路；秒传（IsExist）分支；非法格式 400。
3. 文档列表/重命名/删除/内容读取。
4. extract → partition → 触发 embedding → 轮询状态直到 embeddingCount>0（需真实存储与 AI 渠道，环境同 wiki-embedding-e2e）。
5. 未提取直接 embedding → 业务错误；进行中重复触发 → 409。

回归：现有外部 token / chat / 会话 / kg / wiki / feishu 等 e2e 在 AppIds 清理后仍全绿。

提交前：`dotnet build` 0 error（当前 AppIds 残留导致的 5 错误须清零）、`ui` typecheck/lint/test 全绿。
