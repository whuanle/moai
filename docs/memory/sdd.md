> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)、[../ai/sdd.md](../ai/sdd.md) ｜ 证据：[local-dev/agent-memory-e2e.mjs](../../local-dev/agent-memory-e2e.mjs)

# Agent 应用长期记忆（跨会话记忆）

- 日期：2026-10-12
- 状态：已实现（数据库 + 后端 `src/memory` 模块 + AI 模块挂点 + 前端应用「配置」分区长期记忆收缩卡片与对话页记忆偏好，2026-10-27 起原工作台独立「长期记忆」分区并入「配置」收缩卡片）；mem0 v3 式两段写入 + 按需激活
- 领域：`src/memory`（Shared/Core/Api），AI 模块挂点 `src/ai`（工具来源 + 上下文贡献者 + flush 触发），前端 `ui/src/pages/teams/apps`（AppMemorySettingsPanel、AppUserSettings）
- 场景编号前缀：`@MEM-S*`（见 [bdd.md](./bdd.md)）

## 1. 目标

Agent 应用获得跨会话长期记忆：重要信息选择性写入（mem0 v3 式抽取+合并）、相关记忆按需激活（每轮注入），按用户隔离，支持语义/情景/程序三类记忆（类型由 AI 在保存/提取时自行判定，2026-10-27 起配置不再提供类型限制）；应用管理员在应用设置中开启与配置，用户在对话界面调整个人偏好（关闭/仅本应用/清空）。

## 2. 组件与数据

- **向量本体**：`__agent_mem_{appId:N}` 动态表（`PgVectorAgentMemoryVectorStore`，镜像 `__wiki_{id}`/`__kg_{id}`；HNSW+Cosine，维度取应用配置，≤2000），记录 `{Key=记忆id, UserId(索引), MemoryType(0语义/1情景/2程序, 索引), Content, SourceSessionId, CreateTimeMs, UpdateTimeMs, Embedding}`；写入前做集合维度兼容检查——表不存在直接建，已存在但维度不一致时空表重建自愈（换维度后首写）、非空表抛错提示先清空（维度锁定的运行时兜底）；`__` 前缀不进 EF 模型（脚手架黑名单）。
- **分片登记**：`app_memory_shard`（EF 实体，(app_id,user_id) 部分唯一）：`MemoryCount/LastActiveTime`，写入增/删减/清零保留，支撑**跨应用候选发现**（全平台按用户维度）、清空全部与记忆列表定位。
- **用户偏好**：`app_user_config.memory_mode smallint`（0 跟随应用 / 1 关闭 / 2 仅本应用 / 3 跨应用）。
- **配置**：`app_agent_config.execution_settings.memory`（`AgentMemorySettings`，AI.Shared）：`Enabled/EmbeddingModelId/EmbeddingDimensions(1024)/Enable{Semantic,Episodic,Procedural}/AutoExtract(true)/RetrievalTopK(5)/MinScore(0.35)/MaxPerUser(500)`；随发布快照双轨。应用层只有开/关，**跨应用与否是用户自己的设置**。
- **模块**：`MoAI.Memory.Shared`（IMemoryService/命令/事件）→ `MoAI.Memory.Core`（向量存储/MemoryService/MemoryExtractionTrigger/MemoryExtractConsumer）→ `MoAI.Memory.Api`（`/api/app/{id}/memory/*` 控制器）。

## 3. 关键决策

- **D-M1 存储粒度**：每应用一张向量表（同 wiki/KG 先例），应用各自绑定 embedding 模型/维度，**记忆仅应用内检索**。初版的跨应用检索（shard 发现候选表 + 分维度归并）已于 2026-10-12 移除：各应用向量模型与维度不同无法统一检索，且分表天然隔离，跨应用场景收益不抵复杂度；shard 表保留计数职责（容量上限/管理页定位）。
- **D-M2 写入双通道**：AI 工具 `memory_save`（显式）+ 会话 flush 后 MQ 异步提取（`app.memory.extract`，mem0 两段：①事实抽取含重要性过滤与类型分类，②与既有记忆合并决策 ADD/UPDATE/DELETE/NONE）。合并决策天然幂等；Redis 会话锁串行化；消费失败 Ack 放弃（尽力而为，后续轮次再触发）。
- **D-M3 读路径**：每轮 `MemoryContextProviderContributor`（Order=30，压缩之前）按当前用户消息向量检索注入指令块（MAF `InvokingCoreAsync` 不可返回 null，无可注入时透传原 AIContext）；`memory_search/save/forget` 三工具（Order=22，用户关闭时不注册），描述统一追加「记忆管理约定」——应用配置的 `memory.prompt`（空则平台默认 `AgentMemorySettings.DefaultPrompt`，`GET /api/app/agent/memory-defaults` 供前端占位展示，上限 2000 字），AI 在工具列表即看到如何管理与使用记忆；子代理装配（IsSubAgent）两者均退出。
- **D-M4 用户开关（2026-10-12 二次修订）**：用户记忆模式仅 **开启/关闭** 两态（历史值 0/2/3 一律按开启生效），对话面板无「跟随默认」项（避免用户无法判断应用是否开启记忆）、不展示记忆明细（明细管理进独立「我的记忆」页，避免面板过长）；跨应用选项随功能移除。
- **D-M5 边界**：外部应用强制关闭（保存拒绝 + `CloneWithExternalRestrictions` 装配兜底，沿沙箱/技能先例）；外部/调试会话不提取记忆；去重阈值 0.92 合并更新；超容量删最旧；应用配置模型/维度变更守卫见 D-M6（2026-10-13 收紧）。
- **D-M7 记忆管理页**：独立路由 `team/:teamId/app/:appId/memory/mine`（对话页设置面板「管理记忆」按钮进入；原工作台分区入口随分区并入配置卡片而移除），展示当前用户在本应用下的记忆，**只能删除单条或清空本应用，不能编辑**；`/mine/all`（清空全平台）端点随跨应用功能下线。
- **D-M6 保存校验与模型锁定（2026-10-13 修订）**：`SaveAppAgentConfigCommandHandler.ValidateAndNormalizeMemoryAsync` 校验开启时模型为启用中的 embedding 类型且团队可用；**已有用户记忆后模型/维度锁定**——参照系为生效配置（发布快照优先，即向量本体实际写入口径），草稿处于关闭态不解除锁定（堵住「关了再换模型绕过」的洞），不一致即 400，需先清空应用全部记忆（[@MEM-S18~S21](./bdd.md#mem-s18)）；请求未携带模型/维度时沿用生效配置补齐落库（关闭/未携带 memory 节同样保留），防运行时口径漂移（如维度缺失回落默认 1024 而既有向量为其他维度）；`MemorySettingsValidator` 校验形状与范围。前端有记忆时锁定模型/维度控件（AppMemorySettingsPanel，依据 memory/stats）。

## 4. 接口

`GET /api/app/{id}/memory/mine?page&pageSize`（本人分页列表+开启状态回显）；`DELETE .../mine/{memoryId}`；`DELETE .../mine`（清本应用）；`GET .../memory/stats`（应用记忆统计：总条数+用户数，团队 Admin，工作台分区展示）；`DELETE /api/app/{id}/memory`（团队 Admin 清空应用全部）；`GET/PUT /api/app/{id}/userconfig` 扩展 `memoryMode/memoryEnabled`（`/mine/all` 已下线）。

前端：应用「配置」分区「长期记忆」收缩卡片（AppMemorySettingsPanel，总开关级联+应用记忆统计+危险区清空；「管理记忆」入口已按用户反馈移除；2026-10-27 起由原工作台独立「长期记忆」分区并入，memory 节随页面「保存设置」并入主配置一次提交，卡片默认收起、展开才加载，仅内部应用且 Admin+ 渲染）；对话页设置面板新「长期记忆」卡（开启/关闭两态+「管理记忆」入口，面板宽度可拖拽调整）；独立「我的记忆」管理页（MyMemoryPage，分页列表+删除+清空）。

## 5. 已知问题

- 计数依赖 `app_memory_shard.MemoryCount`，容量淘汰需全量拉取向量记录后排序删除（≤上限条数，v1 可接受）。
- 清空应用记忆与延迟到达的提取任务存在竞态窗口（清空后极短时间内可能被补写一条）。
- `app_memory_shard` 的跨应用发现职责已随功能移除，仅余计数/定位用途（保留实体避免二次 schema 变更）。
- 应用无硬删除端点，应用删除时的向量集合清理待删除能力落地时挂钩（当前可用「清空应用记忆」承担）。
