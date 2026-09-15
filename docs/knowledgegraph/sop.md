# 知识图谱模块操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)

## 1. 前置条件

- 知识图谱挂在团队下：先在「团队」页创建团队（见 [../team/sop.md](../team/sop.md)）；建图 / 接入 / 模型 / 节点 / 边维护均需 Owner/Admin（v2 收紧：Member 全只读）。
- 图谱数据存图数据库（默认 Memgraph，Bolt 协议）；schema 与目录存 PostgreSQL。两者都需可用。
- 入口：一级导航「知识图谱」→ `/knowledge-graph` 卡片墙；团队详情「知识图谱」分区也可建图与维护。

## 2. 开启能力与连接图数据库

1. 以 root 登录 → 「系统设置」→「知识图谱（图数据库）」，把 `KG_ENABLED` 置为 `true`。
2. 配置：`KG_URI`（如 `bolt://127.0.0.1:7687`）、`KG_USERNAME`、`KG_PASSWORD`、`KG_DIALECT`（`memgraph` 默认 / 接外部 Neo4j 选 `neo4j`，影响内省与索引语句）。
3. 设置项无缓存，保存后下一个请求即生效（驱动按连接信息或方言变化自动重建，无需重启）。
4. 未开启或未填地址时：「新建 / 接入」不可用，强行调用返回 409。

### 本地 Memgraph

`docker-compose.yml` 已内置可选 `memgraph` 服务（memgraph-platform：Bolt 7687、MgLab 3000）：

```bash
docker compose up -d memgraph     # 已配 snapshot 持久化（300s 间隔 + 退出快照），数据卷 memgraph_data
```

- 社区版**默认纯内存**，compose 已带 `--storage-snapshot-interval-sec=300 --storage-snapshot-on-exit=true`；切勿移除，否则容器重启丢图。
- MgLab 控制台 <http://127.0.0.1:3000> 可直查 Cypher；系统设置里 `KG_URI` 填 `bolt://127.0.0.1:7687`。

## 3. 日常操作

- **建托管图谱**：团队详情「知识图谱」分区 → 新建 →「平台托管」→ 填名称（≤50，**全平台唯一**，跨团队不得重名）、简介、可选模板（空白 / 运维服务 / 组织人脉 / 事件脉络）。托管图谱零库名：所有托管图共用一个图库 keyspace，经 `kgId` 属性隔离。
- **维护模型**：图谱详情「模型」页增删改实体类型、关系类型；关系类型可选起止实体类型约束；实体类型可配置**属性设置**（属性名 / 类型：文本·数字·布尔·日期 / 是否必填 / 说明，≤50 个）。
- **维护节点 / 边**：仅 Admin+ 可增删改（v1 曾放开 Member，v2 收紧）；节点删除连带其边；边新增 / 修改校验起止约束；实例表单按所属类型的属性定义渲染，必填属性强制填写。
- **维护节点 / 边**：仅 Admin+ 可增删改（v1 曾放开 Member，v2 收紧）；节点删除连带其边；边新增 / 修改校验起止约束。
- **图览画布**：图谱详情默认「图览」——有界子图（上限 500 节点）、实体类型色板过滤、关键字搜索、点节点展开一跳邻接；纯只读，录入仍在实体 / 关系页。
- **接入外部图谱**：新建 →「接入已有」→ 填名称与目标 database（外部实例的库名；Memgraph 社区版单库，填默认库）。接入后只读内省标签 / 关系类型 / 属性键；外部服务直写图库的数据会随内省自动识别（schema 有 5 分钟 Redis 缓存，模型页可点「刷新内省」强制重新识别并对比上次基线显示新增 / 消失项）。
- **接入图图览**：接入图详情也有「图览」——按标签色板过滤 + 关键字搜索 + 点节点一跳展开；外部节点无平台 id 体系，以 `elementId` 定位、`name/title/id` 属性启发式取名（要求 Memgraph ≥ 2.14）。
- **设置图谱头像**：图谱详情「设置」页 → 点头像或「更换头像」上传（支持 JPG/PNG，≤5MB，走存储直传管线）；仅 Owner/Admin；列表卡片同步展示。
- **删除图谱**：托管图 = 软删登记 + 清空该图在图库的节点与边；接入图 = **仅移除平台登记，绝不动外部数据**。

## 4. 常见问题

| 现象 | 处理 |
|---|---|
| 新建 / 接入返回 409「未开启知识图谱能力」 | 系统设置开启 `KG_ENABLED` 并填写 `KG_URI`；仅开启但地址为空同样 409 |
| 建图 / 图操作返回 503「无法连接图数据库」 | Memgraph/Neo4j 不可达或凭据错误；核对 `KG_URI / USERNAME / PASSWORD` 与容器状态 |
| 返回 409「图数据库连接地址无效」 | `KG_URI` 不是合法 URI（须含 scheme，如 `bolt://`） |
| 接入返回 400「数据库不存在或无法访问」 | database 名写错或无权限；先用 MgLab / Neo4j Browser 确认 |
| 接入图新增节点 / 类型返回 409 | 设计如此：接入图只读；图览/邻接查询 v2.1 起已支持接入图 |
| 接入图 schema 看不到刚写入的标签 | 内省结果有 5 分钟缓存；模型页点「刷新内省」立即重新识别 |
| 设置头像返回 404「头像文件不存在或未完成上传」 | objectKey 未登记或直传未完成；重新经设置页上传（存量库需先执行 `asserts/knowledge_graph.sql` 补 `avatar_path` 列） |
| 编辑实体 / 边 / 类型返回 400「id 不正确」 | v1 遗留缺陷（validator 校验路由字段），v2.3 已修复；若复现请更新后端 |
| 删除实体类型 / 关系类型返回 409 | 该类型下仍有数据或被引用；先清理 |
| 同名建图返回 409 | v2 起名称**全平台唯一**；改名或删除旧图后重建 |
| 容器重启后图数据丢失 | compose 的快照持久化参数被移除；恢复 `MEMGRAPH` 环境变量与数据卷 |
| 成员看不到新增 / 编辑 / 删除按钮 | v2 权限收紧：Member 全只读，写操作需 Admin+ |
| Memgraph 3.x 报 `no procedure named 'mg.labels'` 或 `UNWIND must be a list` | v2.1 已修复：memgraph 方言内省改为数据派生查询（3.x 移除了 `mg.*` 过程，且 `labels(n)` 单标签返回字符串）；升级前版本请拉取最新代码 |

## 5. 验收流程

1. `dotnet build src/MoAI/MoAI.csproj` → 0 错误；`dotnet test tests/MoAI.KnowledgeGraph.Tests/` → 40/40。
2. 后端运行且 Memgraph 可达、`KG_ENABLED=true` 后执行 `node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → 覆盖 [@KG-S1](../knowledgegraph/bdd.md#kg-s1)…[@KG-S19](../knowledgegraph/bdd.md#kg-s19)（S15 成员只读由单测覆盖）；无图数据库时脚本 SKIP。
3. 外部开放接口验收：`node local-dev/kg-external-e2e.mjs http://127.0.0.1:5210` → 覆盖 KX-01~KX-08（应用 token 团队级授权、类型/节点/边 CRUD 与批量导入、connected 只读；前置：至少一个团队接入 key，脚本自建）；无图数据库或能力未开启时 SKIP。
4. 浏览器走查：`/knowledge-graph` 与团队「知识图谱」分区建托管图（含模板）→ 图览过滤 / 搜索 / 展开 → 模型 / 节点 / 边维护（Member 只读）→ 删除清库；接入外部库 → 只读内省与刷新 → 接入图图览（标签过滤 / 展开）→ 删除仅移除登记；重启 Memgraph 容器验证快照恢复。

### 验收记录

| 日期 | 结果 |
|---|---|
| 2026-09-10 | 后端构建 0 错误 + 单测 33/33 通过；E2E 未执行（环境无 Neo4j，待补） |
| 2026-09-14 | v2 构建 0 错误 + 单测 39/39；前端 typecheck/lint 0 错误、vitest 265/265；**E2E 40/40 PASS**（真实 Memgraph 3.13.0，覆盖 S1~S16；期间修复 Memgraph 3.x 内省方言与路由回填字段校验 400 两处缺陷，见 tdd 自检记录） |
| 2026-09-15 | v2.1 动态内省 + 接入图画布：单测 **40/40**、**E2E 47/47 PASS**（新增 S17~S19）、前端 typecheck/lint 0 错误、vitest 258/258 |
| 2026-09-15 | v2.2 图谱头像：单测 **42/42**、**E2E 52/52 PASS**（新增 S20，真实存储直传验证）、前端 typecheck/lint 0 错误、vitest 258/258 |
