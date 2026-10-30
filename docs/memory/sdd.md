> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)、[../ai/sdd.md](../ai/sdd.md) ｜ 证据：[local-dev/agent-memory-e2e.mjs](../../local-dev/agent-memory-e2e.mjs)、[local-dev/memory-management-e2e.mjs](../../local-dev/memory-management-e2e.mjs)

# Agent 应用长期记忆（跨会话记忆）

- 日期：2026-10-12（2026-10-30 增补个人全局记忆与记忆管理页，见 D-M8；同日向量化模型/维度统一收敛到系统设置，见 D-M10）
- 状态：已实现（数据库 + 后端 `src/memory` 模块 + AI 模块挂点 + 前端应用「配置」分区长期记忆收缩卡片、对话页记忆偏好与侧边栏「记忆管理」页，2026-10-27 起原工作台独立「长期记忆」分区并入「配置」收缩卡片）；mem0 v3 式两段写入 + 按需激活 + 个人跨应用全局记忆
- 领域：`src/memory`（Shared/Core/Api），AI 模块挂点 `src/ai`（工具来源 + 上下文贡献者 + flush 触发），前端 `ui/src/pages/teams/apps`（AppMemorySettingsPanel、AppUserSettings）、`ui/src/pages/memory`（MemoryManagement）
- 场景编号前缀：`@MEM-S*`（见 [bdd.md](./bdd.md)）

## 1. 目标

Agent 应用获得跨会话长期记忆：重要信息选择性写入（mem0 v3 式抽取+合并）、相关记忆按需激活（每轮注入），按用户隔离，支持语义/情景/程序三类记忆（类型由 AI 在保存/提取时自行判定，2026-10-27 起配置不再提供类型限制）；应用管理员在应用设置中开启与配置，用户在对话界面调整个人偏好（关闭/仅本应用/清空）。

## 2. 组件与数据

- **向量本体**：`__agent_mem_{appId:N}` 动态表（`PgVectorAgentMemoryVectorStore`，镜像 `__wiki_{id}`/`__kg_{id}`；HNSW+Cosine，维度取应用配置，≤2000），记录 `{Key=记忆id, UserId(索引), MemoryType(0语义/1情景/2程序, 索引), Content, SourceSessionId, CreateTimeMs, UpdateTimeMs, Embedding}`；写入前做集合维度兼容检查——表不存在直接建，已存在但维度不一致时空表重建自愈（换维度后首写）、非空表抛错提示先清空（维度锁定的运行时兜底）；`__` 前缀不进 EF 模型（脚手架黑名单）。
- **分片登记**：`app_memory_shard`（EF 实体，(app_id,user_id) 部分唯一）：`MemoryCount/LastActiveTime`，写入增/删减/清零保留，支撑**跨应用候选发现**（全平台按用户维度）、清空全部与记忆列表定位。
- **用户偏好**：`app_user_config.memory_mode smallint`（0 跟随应用 / 1 关闭 / 2 仅本应用 / 3 跨应用）。
- **配置**：`app_agent_config.execution_settings.memory`（`AgentMemorySettings`，AI.Shared）：`Enabled/AllowGlobalMemory/AutoExtract(true)/Prompt/RetrievalTopK(5)/MinScore(0.35)/MaxPerUser(500)`；随发布快照双轨。**向量化模型/维度不再存储在应用配置**（2026-10-30 统一到系统设置，历史字段保存时剥离、运行时由平台设置回填，`EmbeddingModelId/EmbeddingDimensions` 属性仅作运行时载体保留），应用层只有开/关与参数，**跨应用与否是用户自己的设置**。
- **模块**：`MoAI.Memory.Shared`（IMemoryService/命令/事件）→ `MoAI.Memory.Core`（向量存储/MemoryService/MemoryExtractionTrigger/MemoryExtractConsumer）→ `MoAI.Memory.Api`（`/api/app/{id}/memory/*` 控制器）。

## 3. 关键决策

- **D-M1 存储粒度**：每应用一张向量表（同 wiki/KG 先例），应用各自绑定 embedding 模型/维度，**记忆仅应用内检索**。初版的跨应用检索（shard 发现候选表 + 分维度归并）已于 2026-10-12 移除：各应用向量模型与维度不同无法统一检索，且分表天然隔离，跨应用场景收益不抵复杂度；shard 表保留计数职责（容量上限/管理页定位）。
- **D-M2 写入双通道**：AI 工具 `memory_save`（显式）+ 会话 flush 后 MQ 异步提取（`app.memory.extract`，mem0 两段：①事实抽取含重要性过滤与类型分类，②与既有记忆合并决策 ADD/UPDATE/DELETE/NONE）。合并决策天然幂等；Redis 会话锁串行化；消费失败 Ack 放弃（尽力而为，后续轮次再触发）。
- **D-M3 读路径**：每轮 `MemoryContextProviderContributor`（Order=30，压缩之前）按当前用户消息向量检索注入指令块（MAF `InvokingCoreAsync` 不可返回 null，无可注入时透传原 AIContext）；`memory_search/save/forget` 三工具（Order=22，用户关闭时不注册），描述统一追加「记忆管理约定」——应用配置的 `memory.prompt`（空则平台默认 `AgentMemorySettings.DefaultPrompt`，`GET /api/app/agent/memory-defaults` 供前端占位展示，上限 2000 字），AI 在工具列表即看到如何管理与使用记忆；子代理装配（IsSubAgent）两者均退出。
- **D-M4 用户开关（2026-10-12 二次修订）**：用户记忆模式仅 **开启/关闭** 两态（历史值 0/2/3 一律按开启生效），对话面板无「跟随默认」项（避免用户无法判断应用是否开启记忆）、不展示记忆明细（明细管理进独立「我的记忆」页，避免面板过长）；跨应用选项随功能移除。
- **D-M5 边界**：外部应用强制关闭（保存拒绝 + `CloneWithExternalRestrictions` 装配兜底，沿沙箱/技能先例）；外部/调试会话不提取记忆；去重阈值 0.92 合并更新；超容量删最旧；应用配置模型/维度变更守卫见 D-M6（2026-10-13 收紧）。
- **D-M7 记忆管理页**：独立路由 `team/:teamId/app/:appId/memory/mine`（对话页设置面板「管理记忆」按钮进入；原工作台分区入口随分区并入配置卡片而移除），展示当前用户在本应用下的记忆，仅删除/清空（2026-10-30 起编辑能力收敛到全局「记忆管理」页，见 D-M8）；`/mine/all`（清空全平台）端点随跨应用功能下线。
- **D-M6 保存校验与模型锁定（2026-10-30 随统一口径重写，原「应用级锁定」语义废止）**：应用配置不再接收 `embeddingModelId/embeddingDimensions`——`ValidateAndNormalizeMemoryAsync` 剥离这两个字段后落库（历史存量值随保存收敛），`MemorySettingsValidator` 只校验其余参数形状与范围；开启记忆时校验平台总开关已开且已选统一模型，否则 400「平台未开启长期记忆，请先在系统设置中开启」（[@MEM-S1](./bdd.md#mem-s1)）。模型/维度变更锁定的职责上移到平台设置层（见 D-M10），应用侧不再有锁定参照系逻辑。
- **D-M8 个人全局记忆与记忆管理页（2026-10-30；设置键已并入 D-M10 统一口径）**：用户跨应用记忆以**平台级全局集合** `__agent_mem_global` 落地（固定表名，记录复用 `AgentMemoryVectorRecord` 按 `UserId` 隔离；shard 登记以哨兵 appId `MoAI.Database.MemoryGlobalScope.GlobalAppId` 写入 `app_memory_shard`，TeamId=0），向量化模型由系统设置统一指定（**须为公开启用中的 embedding 模型**——全局解析按 teamId=0 仅公共模型）。全局记忆**仅人工维护**（管理页新建/编辑/删除/清空 + 应用记忆「存为全局」promote 复制重嵌）+ AI 工具按需全局写入（见下），不做应用提取的自动镜像（避免应用噪声污染全局与双倍向量化成本）。读路径并入既有检索：`MemoryService.SearchAsync` 在应用召回后追加全局召回（统一模型生成查询向量）按相似度归并，注入行带「全局|」标记、`memory_search` 结果带 `scope`。前端侧边栏新增「记忆管理」页（`/memory`，Tabs：应用记忆总览→明细维护 / 全局记忆 CRUD），应用记忆编辑走 `PUT /api/app/{id}/memory/mine/{memoryId}`（保留 id 与创建时间，重嵌向量）。**顺带修复存量缺陷**：注入时原返回全新 `AIContext`（MAF 提供者为链式替换语义）导致用户消息与工具一并丢失——改为在既有上下文追加指令后返回（[@MEM-S30](./bdd.md#mem-s30)）。
- **D-M9 记忆三级开关（2026-10-30；平台级改单总开关）**：记忆能力按 **平台（`MEMORY_ENABLED` 总开关）→ 管理员（应用 `memory.allowGlobalMemory`）→ 用户（`app_user_config.memory_global_mode`：0=默认接入 / 1=不接入）** 三级门禁；总开关同时管控应用记忆与全局记忆（关闭时应用侧不可开启 [@MEM-S1](./bdd.md#mem-s1)、全局新建/写入 400、运行时注入/工具/提取全部停用）。`MemoryResolution.GlobalAllowed` 汇总应用允许 + 用户接入两态；检索合并、注入与工具全局写入均以 `GlobalAllowed` 为闸（管理员未允许或用户关闭时用户记忆仅在本应用内使用 [@MEM-S33~S35](./bdd.md#mem-s33)）。管理员开关在应用「长期记忆」卡片（不参与模型锁定，随保存自由切换）；用户开关在对话页设置面板（仅管理员允许时展示，记忆关闭时禁用）；未开启记忆的应用不允许设置用户接入偏好。**工具集补齐**：`memory_save` 增可选 `scope`（app 默认 / global——`GlobalAllowed` 时可用，拒绝时返回提示文案由模型转告）；`memory_forget` 应用内未命中且允许全局时按全局兜底删除；`memory_search` 结果带 `scope` 区分（[@MEM-S36](./bdd.md#mem-s36)）。存量库补列 `app_user_config.memory_global_mode`（asserts/app_user_config.sql）。
- **D-M10 记忆设置统一到系统设置（2026-10-30）**：向量化模型/维度从「全局记忆专属 + 应用各自配置」**统一收敛为平台单套口径**——系统设置三键 `MEMORY_ENABLED`（总开关，默认 false）/ `MEMORY_EMBEDDING_MODEL`（统一向量化模型，须公开启用中的 embedding 模型）/ `MEMORY_EMBEDDING_DIMENSIONS`（1~2000，默认 1024），旧 `MEMORY_GLOBAL_*` 三键废弃（asserts/setting.sql 增量种子含旧键清理）。运行时由 `MemoryService.ResolveSettingsAsync` 单点收口：读应用 memory 节后用平台设置**回填**模型/维度，平台不可用（总开关关或未选模型）返回 null——注入/工具/提取/检索全路径一并停用。**平台锁定**：统一模型已配置且 `app_memory_shard` 任一分片 `MemoryCount>0`（应用记忆或全局记忆）时模型/维度不可更换，`SettingsService.ValidateMemorySettingAsync` 返回 400「请先清空全部记忆」；**尚未配置统一模型（首次设置）不触发锁定**（存量库直切策略：历史应用分片按旧口径写入，由管理员按需清空后按新口径重建）。root `DELETE /api/memory/all` 一键清空**全部**记忆（各应用集合删除 + shard 清零 + 全局集合）解锁换模型；原 `DELETE /api/memory/global`（仅全局）端点废止。应用面板（AppMemorySettingsPanel）不再提供模型/维度控件，经 `GET /api/app/agent/memory-defaults` 回显平台模型名/维度（响应扩 `platformEnabled/embeddingModelName/embeddingDimensions`），平台未开启时禁用应用开关。

## 4. 接口

`GET /api/app/{id}/memory/mine?page&pageSize`（本人分页列表+开启状态回显）；`DELETE .../mine/{memoryId}`；`PUT .../mine/{memoryId}`（编辑本人应用记忆，2026-10-30）；`DELETE .../mine`（清本应用）；`GET .../memory/stats`（应用记忆统计：总条数+用户数，团队 Admin，工作台分区展示）；`DELETE /api/app/{id}/memory`（团队 Admin 清空应用全部）；`GET/PUT /api/app/{id}/userconfig` 扩展 `memoryMode/memoryEnabled`（`/mine/all` 已下线）。

记忆管理（2026-10-30）：`GET /api/memory/overview`（本人记忆总览：应用列表+全局概况）；`GET /api/memory/global/mine`、`POST .../global/mine`（新建全局）、`PUT .../global/mine/{memoryId}`、`DELETE .../global/mine/{memoryId}`、`DELETE .../global/mine`（清本人全局）；`POST /api/memory/global/promote`（应用记忆存为全局）；`GET /api/memory/global/model-options`（root：公开 embedding 模型选项）；`DELETE /api/memory/all`（root：清空全部记忆——所有应用记忆+全局记忆，解除统一模型锁定；原仅清全局的 `DELETE /api/memory/global` 已废止）。系统设置 `MEMORY_ENABLED/MEMORY_EMBEDDING_MODEL/MEMORY_EMBEDDING_DIMENSIONS`（`GET/PUT /api/settings`）；`GET /api/app/agent/memory-defaults` 扩展回显 `platformEnabled/embeddingModelName/embeddingDimensions`。

前端：应用「配置」分区「长期记忆」收缩卡片（AppMemorySettingsPanel，总开关级联+统一模型只读回显（系统设置统一配置，应用侧不可自选；平台未开启时禁用开关）+应用记忆统计+危险区清空；「管理记忆」入口已按用户反馈移除；2026-10-27 起由原工作台独立「长期记忆」分区并入，memory 节随页面「保存设置」并入主配置一次提交，卡片默认收起、展开才加载，仅内部应用且 Admin+ 渲染）；对话页设置面板新「长期记忆」卡（开启/关闭两态+「管理记忆」入口，面板宽度可拖拽调整）；独立「我的记忆」管理页（MyMemoryPage，分页列表+删除+清空）；侧边栏「记忆管理」页（MemoryManagement，`/memory`：应用记忆 Tab（总览列表→明细，编辑/删除/清空/存为全局）+ 全局记忆 Tab（新建/编辑/删除/清空，平台未开启时引导提示），系统设置页「长期记忆」卡片（root：总开关+公开模型选择+维度+清空全部记忆）。

## 5. 已知问题

- 计数依赖 `app_memory_shard.MemoryCount`，容量淘汰需全量拉取向量记录后排序删除（≤上限条数，v1 可接受）。
- 清空应用记忆与延迟到达的提取任务存在竞态窗口（清空后极短时间内可能被补写一条）。
- `app_memory_shard` 的跨应用发现职责已随功能移除，仅余计数/定位用途（保留实体避免二次 schema 变更）。
- 应用无硬删除端点，应用删除时的向量集合清理待删除能力落地时挂钩（当前可用「清空应用记忆」承担）。
- **统一口径直切策略（2026-10-30）**：历史应用记忆按各应用旧模型/维度写入，统一切换后运行时一律按平台模型解析——维度不一致的应用向量检索/写入会报错，由管理员「清空应用全部记忆」后按新口径重建；平台首次选模型不因存量分片锁定（见 D-M10）。
