# 知识图谱外部开放接口设计（2026-09-15）

> 状态：待评审。实现计划由 writing-plans 阶段产出。

## 1. 背景与目标

外部系统已可通过 access key（`access_app.Key`，`moai-ac-` 前缀）在 `POST /api/external/token` 换取应用 token（JWT，`UserType.ExternalApp`，携带 `TeamId/AppIds` claims）。知识图谱（managed 模式）目前仅支持平台内部用户经 `/api/knowledge-graph` 访问。

目标：应用 token 可通过外部开放接口对其被授权的知识图谱维护**实体（节点）、关系（边）、模型（实体类型/关系类型）**，含单条增删查改与批量导入。

非目标：

- 不开放图谱目录级操作（新建/删除/修改图谱本身、头像）。
- 不开放 connected 模式图谱的写操作（外部系统直写图库的场景维持轮次 70 的模式，平台只读转接）。
- 不引入细粒度 read/write scope 模型（权限边界 = 接入点绑定的图谱白名单，权限等价团队 Admin 对节点/边/类型的操作）。

## 2. 授权模型

- `AccessAppEntity` 新增 `GraphIds`（`List<Guid>`，nullable），语义与现有 `AppIds` 一致：
  - 管理端 AccessApp 创建/更新接口支持设置；校验规则：图谱必须属于接入点所在团队、且 `mode = managed`、未删除。
  - 应用 token 签发与 refresh 时从库重建 `graphids` claim；接入点解绑图谱后 refresh 即失去对应权限（与 AppIds 同语义，删接入即吊销）。
- `ExternalTokenContext` 增加 `GraphIds` 与 `IsGraphAuthorized(kgId)`。
- 外部图谱授权器 `IExternalKnowledgeGraphAuthorizer`（knowledgegraph 模块内）：
  - `AuthorizeAsync(kgId)`：图谱存在且属 token 的 `TeamId`、`kgId ∈ GraphIds`、mode=managed。未绑定/跨团队 → 403（对外语义）或 404（不存在）；connected 写 → 409 只读。
- Handler 身份来源：现有节点/边/类型写 Handler 的用户依赖按 cqrs-conventions 走 `IUserIdContext`（Command 已有则复用），外部路径由 Controller 组装 Command 时填充外部应用身份标识（accessAppId），禁止 Handler 注入 `IUserContextProvider`。

## 3. 外部接口清单

统一挂 `/api/external/knowledge-graph`，走 `ExternalAuthenticationMiddleware`（`/api/external` 前缀已拦截）+ `[ExternalAuthorize]`，仅应用 token（用户 token 不适用于本组接口）。错误体沿用外部接口 `{error:{message,type,code}}`。

| 端点 | 方法 | 说明 |
|---|---|---|
| `/list` | POST | 授权图谱列表（按 GraphIds 过滤，含名称/模式/统计） |
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

请求/响应模型放 `MoAI.KnowledgeGraph.Shared`，实现 `IModelValidator<T>`；单条接口模型与内部接口字段对齐（但不直接复用内部 Command 类型，外部 DTO 独立定义以避免内外耦合）。

批量导入语义：

- 请求体内数组逐条跑与单条相同的校验（类型存在、名称非空、边端点存在等）；任一失败整批拒绝（图库单事务提交），响应返回失败条目的序号与原因。
- 幂等性本期不做 upsert；外部系统按返回结果重试。

## 4. 模块与数据流

```
外部系统 --(access key)--> POST /external/token --> 应用 token(GraphIds claim)
外部系统 --(Bearer)--> ExternalKnowledgeGraphController (KnowledgeGraph.Api)
   --> ExternalTokenContextAccessor 取 TeamId/GraphIds
   --> IExternalKnowledgeGraphAuthorizer.AuthorizeAsync(kgId)
   --> 复用 Core/Handlers 的 Command（IUserIdContext 携带外部身份）
       - 节点/边: IKnowledgeGraphStore (openCypher, kgId 隔离)
       - 类型: EF --> kg_entity_type / kg_relation_type (PG)
批量: 新增 Batch Handler，循环复用校验逻辑 + Store 单事务
```

涉及改动：

- `src/database`：`AccessAppEntity.GraphIds` + Fluent 配置迁移。
- `src/app`：AccessApp CRUD 支持图谱绑定与校验；`ExternalTokenProvider`/`ExternalTokenContext`/refresh 增补 GraphIds。
- `src/knowledgegraph`：`Shared` 外部 DTO/Command、`Core` 外部授权器 + 批量 Handler、`Api` 外部 Controller。
- `ui`：管理端接入点编辑表单增加"可访问图谱"多选（仅 managed、本团队）。
- 文档：`docs/knowledgegraph/bdd.md` 新增场景（新编号不复用）、sdd/tdd/sop 同步，`docs/rounds-log.md` 记录轮次。

## 5. 错误与边界

- 未绑定图谱 / 跨团队：403；图谱不存在：404；connected 写：409 只读。
- token 过期：沿用外部体系 401 + refresh。
- 名称冲突、类型不存在等业务错误：与内部接口同语义，`BusinessException` 显式 StatusCode。
- 批量超 200 条：400，提示上限。

## 6. 测试与验收

新增 `local-dev/kg-external-e2e.mjs`：

1. key 换应用 token；`/list` 仅返回绑定图谱。
2. 未绑定图谱操作 → 403；不存在 → 404。
3. 绑定后：类型/节点/边全量 CRUD + 分页 + 邻接 + schema 全绿。
4. 批量导入：成功批次落库；含非法条目整批回滚且返回逐条原因；超限 400。
5. connected 图谱写 → 409。
6. 管理端解绑图谱 → refresh token 后该图谱操作 403。
7. 用户 token（非应用 token）访问 → 403。

提交前：`dotnet build` 0 error、`ui` typecheck/lint/test 全绿、现有 e2e（kg/feishu 等）不回归。
