# 知识库模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)

- 日期：2026-09-02（一期）/ 2026-09-07（重做：卡片聚合 + 公开开关 + 占位详情页）/ 2026-09-08（向量化设置）/ 2026-09-08（修：元数据/切片参数下沉到文档级）/ 2026-09-09（重排序模型设置）/ 2026-09-10（向量化只做向量化，元数据生成独立）/ 2026-09-21（移除公开概念 + 卡片统计）
- 状态：知识库 CRUD、团队级 embedding 模型绑定、可选 rerank 模型绑定已实现；元数据生成模型在切片预览区独立选择，切片大小/重叠由切割步骤动态传入；公开概念已于 2026-09-21 整体移除
- 领域：`src/wiki`（Shared/Core/Api），前端 `ui/src/pages/wiki`

## 1. 目标

知识库是团队下的第一类资源（对齐"按团队管理"路线）。重做后交付：
- 知识库 CRUD（团队作用域），列表以**卡片网格**聚合我加入的所有团队的知识库（仅我的团队）。
- **（2026-09-21）** 团队知识库不存在公开概念：`is_public` 字段删除，非成员一律 404；卡片展示文件数/切片数/最近文档更新统计。
- `/wiki/:id` 知识库详情页为**占位模板**（内容/文档/检索能力下阶段落地）。
- **（2026-09-08 修正，2026-09-10 更新）** wiki 设置页只承载 embedding 模型与维度上限；元数据生成模型由切片预览区独立的元数据生成操作选择，切片大小、切片重叠由切割步骤动态传入，均不再随向量化触发传入。

## 2. 数据模型

- `wiki`：`id / team_id / name / description / avatar_path / counter / embedding_model_id / embedding_dimensions / rerank_model_id / is_lock / default_workflow_config` + 审计（bool 软删除，同 team 约定 D1）；`is_public` 列已删除（`asserts/wiki_drop_is_public.sql`）
  - `embedding_dimensions` 上限 2000（pgvector 建 hnsw 索引的硬上限）
  - `rerank_model_id` **可空**：未绑定表示检索不做重排序；与向量化配置解耦，锁定后仍可修改
  - `default_workflow_config`：默认工作流三步预设 JSON（`{partition?, metadata?, embedding?}`，null=未配置该步骤；序列化 camelCase + camelCase 枚举，`WikiWorkflowConfigJson`），空串表示未配置；增量 DDL `asserts/wiki_workflow.sql`；2026-09-21 起仅作为外部源工作流缺省时的回退值（批量执行已改为实时填参）
    - `partition`：`mode`（`WorkflowPartitionMode` normal/ai，缺省 normal 兼容旧 JSON）；普通切割带 `splitMode/chunkSize/chunkOverlap/overlapUnit/sizeUnit/tokenEncodingOrModel`；AI 切割带 `aiModelId/promptTemplate`
    - `metadata`：`metadataModelId` + `strategyTypes`（多选，空/null=全套）
  - **无** `metadata_model_id` / `chunk_size` / `chunk_overlap` 列，这些参数属于单文档触发口径，不再由 wiki 持有
- `wiki_document` 与切片/向量表不变；`wiki_document.slice_config` 保存该文档上次触发的切片 JSON（`splitMode/chunkSize/chunkOverlap/overlapUnit/sizeUnit/tokenEncodingOrModel`），用于展示历史
- 文档内容持久化：`wiki_document_content`（按 `document_id` 唯一一行），保存最近一次触发由 Maomi.ToMarkdown 抽取出的完整 markdown，供后续编辑/重抽/溯源使用
  - 抽取入口：`Maomi.ToMarkdown.TextExtractionService`（注册名 `AddTextExtraction()`，由 `WikiCoreModule` 装配），按文件 MIME 类型路由到 `MsWordExtractor / MsExcelExtractor / MsPowerPointExtractor / PdfToMarkdownConverter / ReverseMarkdown` 等实现
  - 切割入口：普通切割使用 Maomi.ToMarkdown TextSplit，支持 `SplitByMarkdown / SplitRecursive / SplitFixedSize / SplitBySentence / SplitByParagraph`；`chunkSize` 的含义由 `sizeUnit` 决定（字符或 `Maomi.ToMarkdown.Token` token），`chunkOverlap` 的含义由 `overlapUnit` 决定（字符/句子/段落）
  - 抽取时机：完成上传后自动入库内容；切割时按用户选择的 `splitMode / chunkSize / chunkOverlap / overlapUnit / sizeUnit / tokenEncodingOrModel` 写入 `wiki_document_chunk_content`；失败时不影响已入库文档内容
  - 抽取与切分互不阻塞：抽取失败抛 `NotSupportedException`，业务层映射为 400 提示「文件类型不受支持」
- partial 唯一 `(team_id, name) WHERE is_deleted = false`：同团队未删除范围内名称唯一，删除后同名可重建；不同团队互不影响
- 索引 `idx_wiki_team_id`；DDL：`asserts/wiki.sql`、`asserts/wiki_vectorization.sql`（vectorization 文件已收缩为「去列 + 上限 CHECK 约束」）
- **外部源**：`wiki_source`（`source_type` 见 `WikiSourceType`：0=飞书文档 1=爬虫；`config` 存类型专属配置 JSON；`workflow_config` 存该源的三步工作流预设，空串=回退 `wiki.default_workflow_config`；`cron` 空串=未开定时；`is_event_subscription`；`last_sync_time/status/message`）与 `wiki_source_document`（`(source_id, external_key)` 映射 + `content_hash` 用于增量比对 + `external_token/title/path/revision` + 文档级 `status/last_error`）。DDL `asserts/wiki_source.sql`，唯一索引 `idx_wiki_source_wiki_name_uindex`（知识库内名称唯一）

## 3. 权限（复用 Team 领域角色，Handler 层判定）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 创建/更新/删除 | ✅ | 403 | 404 |
| 列表 | ✅ | ✅ | 404 |
| 详情 | ✅ | ✅ | 404 |
| 更新 wiki embedding 模型 / 维度 | ✅ | 403 | 404 |
| 更新 wiki rerank 模型（含解绑） | ✅ | 403 | 404 |
| 更新默认工作流配置 | ✅ | 403 | 404 |
| 触发文档向量化 / 批量工作流 | ✅ | ✅ | 404 |
| 外部源：创建/更新/删除/同步 | ✅ | 403 | 404 |
| 外部源：列表 / 已同步文档 | ✅ | ✅ | 404 |

- 角色判定注入 `MoAI.Team.Shared` 的 `ITeamService`（跨域接口复用，实现由 Team 模块注册）
- 列表/详情响应携带 `myRole`，前端据此隐藏管理操作；非成员一律 404（公开语义已移除）
- 管理动作（建/改/删）仍要求 Admin+
- 触发文档向量化只要求「团队成员」即可（不再硬性要求 Admin），元数据模型授权校验与 embedding 模型一致

## 4. API

| 方法 | 路由 | 说明 | 出参 |
|---|---|---|---|
| POST | `/api/wiki` | 创建 `{teamId, name, description?}` | `SimpleLong` |
| GET | `/api/wiki/list?teamId=` | 团队知识库列表（含 myRole、avatarPath、documentCount、chunkCount、lastDocumentUpdateTime） | `QueryWikisCommandResponse` |
| GET | `/api/wiki/{id}` | 详情（含 myRole、avatarPath、embeddingModelName、embeddingDimensions、rerankModelId/Name、isLock） | `QueryWikiCommandResponse` |
| PUT | `/api/wiki/{id}` | 更新 `{name, description?}` | Empty |
| DELETE | `/api/wiki/{id}` | 软删除 | Empty |
| POST | `/api/wiki/{id}/avatar` | 设置头像 `{objectKey}`，仅 Admin+ | Empty |
| GET | `/api/wiki/model-options?teamId=` | 可用模型候选（公开 + 已授权 conversation / embedding / rerank），仅团队成员 | `QueryWikiModelOptionsCommandResponse` |
| PUT | `/api/wiki/{id}/embedding-config` | 绑定向量模型 + 维度（1-2000），仅 Admin+；已锁定 wiki 返回 409 | Empty |
| PUT | `/api/wiki/{id}/rerank-model` | 绑定/更换重排序模型（`rerankModelId` 可空），仅 Admin+；**已锁定 wiki 仍可修改** | Empty |
| PUT | `/api/wiki/{id}/workflow-config` | 保存默认工作流三步预设（`partition?/metadata?/embedding?`，null 步骤=清除），仅 Admin+，整体覆盖；不触发文档处理 | Empty |
| POST | `/api/wiki/{id}/documents/preupload` | 预上传 | `PreUploadWikiDocumentCommandResponse` |
| POST | `/api/wiki/{id}/documents/complete` | 完成上传 | Empty |
| POST | `/api/wiki/{id}/documents/list` | 文档分页 | `QueryWikiDocumentsCommandResponse` |
| DELETE | `/api/wiki/{id}/documents` | 批量删除 | Empty |
| GET | `/api/wiki/{id}/documents/{documentId}/download` | 下载地址 | `SimpleString` |
| PUT | `/api/wiki/{id}/documents/{documentId}/rename` | 重命名 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/partition` | 普通切割，传 `splitMode/chunkSize/chunkOverlap/overlapUnit/sizeUnit/tokenEncodingOrModel?`，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/ai-partition` | AI 切割，传 `aiModelId/promptTemplate?`，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/chunks/{chunkId}/metadata/generate` | 为单个切片生成/重生成元数据，传 `metadataModelId`，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/chunks/metadata/generate` | 为文档切片批量生成/重生成元数据，传 `metadataModelId`，`chunkIds` 为空表示全部切片，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/embedding` | 触发向量化，传 `isEmbedSourceText/isEmbedMetadata`（至少一项为真），仅团队成员 | `{ taskId }` |
| POST | `/api/wiki/{id}/documents/batch-workflow` | 批量执行工作流：`documentIds`(1-50) + 三步开关（切割/生成元数据/向量化，可只选一步）+ 各步参数（切割支持 `isAiPartition`+`aiModelId`+`promptTemplate` 或普通切割参数；元数据 `strategyTypes` 多选，空=全套），仅团队成员；逐文档返回 `{documentId, fileName, success, message, taskId?}` | `BatchRunWikiDocumentWorkflowCommandResponse` |
| GET | `/api/wiki/{id}/documents/{documentId}/embedding` | 查询文档向量化详情（含 wiki embedding 模型/维度、上次切割配置、切片列表） | `QueryWikiDocumentEmbeddingCommandResponse` |
| POST | `/api/wiki/{id}/recall-test` | 召回测试：`query`(≤1000) + `documentIds?`（范围过滤，空=全部）+ `top`(1-50，默认 5) + `minScore?`(0-1) + `aiModelId?` + `isOptimizeQuery` + `isAnswer`，仅团队成员；开启优化/回答时 `aiModelId` 必填且校验为团队可用对话模型 | `QueryWikiRecallTestCommandResponse`（`query/optimizedQuery/answer/items[]`，items 含 `documentId/documentName/chunkId/metadataType/content/score`） |
| POST | `/api/wiki/{wikiId}/sources` | 创建外部源：`sourceType` + `name` + 类型专属配置（爬虫 `crawler`；飞书二选一：`feishuAppId` 或 `newAppName/newAppId/newAppSecret` + `nodeToken`）+ 可选 `cron`/`workflow`/`isEventSubscription`，仅 Admin+；创建即绑定飞书渠道并立即拉取一轮 | `SimpleGuid` |
| GET | `/api/wiki/{wikiId}/sources` | 外部源列表（含 `myRole`、`sourceType`、类型配置、飞书应用名/开放平台 AppId/长连接在线、`cron`、`lastSyncStatus/Time/Message`、`documentCount`），仅团队成员 | `QueryWikiSourcesCommandResponse` |
| PUT | `/api/wiki/{wikiId}/sources/{sourceId}` | 更新外部源，仅 Admin+；未传字段保持不变，`cron` 空串=关闭定时，`workflow` null=清除预设回退默认 | Empty |
| DELETE | `/api/wiki/{wikiId}/sources/{sourceId}` | 删除外部源，仅 Admin+；解除飞书绑定、移除定时任务与映射，**不删除已入库文档** | Empty |
| POST | `/api/wiki/{wikiId}/sources/{sourceId}/sync` | 手动同步，`force=true` 忽略哈希比对全量重处理，仅 Admin+；停用源 409 | `SyncWikiSourceCommandResponse` |
| POST | `/api/wiki/{wikiId}/sources/{sourceId}/documents` | 已同步文档分页 + 标题/路径关键字筛选，仅团队成员 | `QueryWikiSourceDocumentsCommandResponse` |

> 原文档层接口（`/documents/{id}` GET）已随实体重做移除；内容/文档能力下阶段按文件接入模型重建。

## 5. 前端「知识库」页与团队模式上下文

- `/wiki` 页：**纯只读卡片网格**，聚合我加入的所有团队的知识库（前端 `getMyTeams()` → 逐团队 `getWikis(teamId)` 合并，附 `teamName`、`myRole`）。仅我的团队。**不提供新建/编辑/删除入口**（管理功能在团队详情）。
- `/wiki` 页卡片点击 → `/team/{teamId}/wiki/{wikiId}`：知识库详情页，左侧菜单（文件列表 / 召回测试 / 设置）；设置页可编辑名称/简介、上传头像（仅 Admin+），以及绑定 embedding 模型与向量维度（1-2000）。**元数据模型 / 切片参数不在 wiki 设置页**。文件列表与召回测试已落地（召回测试见下），设置页管理操作仅 Admin+。**详情路由为团队嵌套**（`/team/:teamId/wiki/:wikiId/:section?`）。
- **召回测试 tab（`WikiRecallTest`，仅团队成员）**：双栏 playground 布局——左栏 400px 查询表单（查询问题 ≤1000 字、Enter 直接搜索、Shift+Enter 换行；文档范围多选（`getWikiDocuments` 拉取前 100 篇，空=全部）；返回条数（1-50，默认 5）与相似度阈值（可选 0-1）并排；「AI 增强」分组内 AI 优化问题 / AI 生成回答开关 + 对话模型选择（`getWikiModelOptions` 的 conversationModels，开关全关时禁用、任一开启时必填并自动带入第一个可用模型）；搜索召回按钮通栏）+ 右栏结果区自适应剩余宽度（未搜索时展示引导空态，搜索中展示状态条；已有结果再次搜索时顶部叠加状态条、旧结果保留）。结果区自上而下：**AI 回答卡片**（`colorInfoBg` 背景 + 机器人图标 + "由 {model} 生成"署名 + 14px/1.8 行高正文；请求了回答但为空时降级提示——有命中→warning"AI 未返回回答内容，可尝试更换模型"、无命中→info"已跳过 AI 回答"）、优化后的问题 Alert（问题文本加粗）、召回结果列表（标题带条数；命中项含序号 + 相似度得分着色 Tag、文档名、元数据类型 Tag——0=原文 1=大纲 2=问题 3=关键词 4=摘要 5=聚合段、内容 4 行截断可展开）。md 断点以下纵向堆叠。i18n 键 `wiki.recall.*`（zh/en 同步）。后端配套：`IsAnswer` 的回答调用显式 `DisableThinking = true`——思考型模型（如 Hy4 preview）经 MAF `GetResponseAsync` 会把输出预算耗在思维链，`response.Text` 返回空串。
- `/team/{teamId}/wiki/{wikiId}/document/{documentId}/embedding` 文档级页：展示文档内容、普通/AI 切割与向量化；普通切割可选择 Maomi.ToMarkdown 切割方式、长度单位（字符/Token）、重叠单位、切片长度与切片重叠；无历史切割配置时，前端按 wiki 向量维度默认推荐 Markdown 感知 + Token 计量 + 句子重叠 1 的最佳配置，并提供「应用推荐值」恢复推荐；选择句子优先/段落优先切割时，前端自动切换并锁定对应重叠单位；选择 Token 时前端下拉提供后端明确支持的编码；AI 切割提供可直接使用的默认提示词模板，不再额外展示说明提示；切片预览可选择对话模型后对单个切片或全部切片生成/重生成元数据；向量化仅选择是否对原文切片/已生成元数据向量化，两项均未勾选时阻止提交，元数据生成模型选择器不在向量化表单中。
- **团队详情「知识库」tab（`TeamWikis`）**：真正的知识库管理（新建/修改/删除），仅 Owner/Admin 可操作，Member 只读。数据源 `getWikis(teamId)` 的 `myRole` 判定。卡片以**统计条**展示文件数量 / 切片数量 / 最近文档更新（来自列表接口 `documentCount/chunkCount/lastDocumentUpdateTime`，无文档时最近更新显示占位符），公开状态标识已随公开概念移除；新建/编辑弹窗仅名称 + 简介。
- **设置页不提供「默认工作流」卡（2026-09-21 产品决策移除）**：批量执行的步骤参数在多选文档弹窗中每次实时填写；`PUT workflow-config` 保留为后端能力，主要供外部源工作流缺省时回退（见 D21）。
- **爬虫源表单（`WikiSourceCrawlerForm`）**：起始地址（必填，前端同步做 http/https 校验）、路径前缀、抓取深度、单轮页数上限、**请求间隔（抓取频率）**、请求超时、正文选择器、UserAgent、是否覆盖已存在页面。请求间隔提供「快速 1 秒 / 常规 3 秒 / 慢速 10 秒 / 极慢 30 秒」四档快捷链接，默认 1 秒；表单内以 `Alert` 明示「串行抓取 + 间隔 + 页数上限」的三重保护，避免把目标站点抓崩。
- **新建/编辑弹窗（`Modal`，2026-09-21 由抽屉 + `Segmented` 改版）**：新建无单一入口，而是「新建飞书源 / 新建爬虫」双按钮各自打开对应类型的 Modal；编辑打开同一 Modal，类型以只读 Tag 展示（后端 D24 规定类型创建后不可变更）。类型由入口按钮/记录决定（`activeType` 状态驱动字段区切换），飞书字段与爬虫字段互不渲染，避免隐藏字段被一并校验。
- **外部源列表列**：名称（含类型图标 + 描述 tooltip）/ 类型 / 抓取目标（爬虫=起始地址，飞书=连接名或节点 token）/ **抓取频率（`N 秒/次`）** / 定时同步（cron，未开启显示「未开启」）/ 已同步文档数（点击打开文档抽屉）/ 最近同步（状态 Tag + 时间 + 消息）/ 启用开关 / 操作（立即同步、查看文档、编辑、删除）。
- **文件列表批量处理（`BatchWorkflowModal`）**：多选行 → 工具栏「批量处理」→ 弹窗三步勾选（参数每次按通用默认值预填，实时调整后提交）→ 提交后展示逐文档成败列表；向量化模型未配置时禁用提交并提示；批量删除按钮同步补齐 Popconfirm。
- `store/app.ts` 现有 `currentTeamId`（persist）与 `myTeams`（内存）保持；`Teams.tsx` 增删后同步 `myTeams`。

## 6. 关键决策

- **D1** 权限复用团队角色：wiki 不引入新角色体系，Admin+ 可管理
- **D2** 跨域接口复用：Core 引用 `MoAI.Team.Shared`（接口），不引用其实现项目
- **D3** 名称唯一性为团队作用域（团队间允许同名）
- **D4** 公开概念已整体移除（2026-09-21 产品决策「团队的知识库不存在公开」）：`wiki.is_public` 列与全部接口字段删除，原「公开库非成员只读（`myRole=0`）」语义作废，非成员访问详情一律 404；存量库经 `asserts/wiki_drop_is_public.sql` 删列
- **D5** `/wiki` 列表只在「我的团队」作用域聚合（原「公开库可经详情路径只读访问」随 D4 作废）
- **D6** 权限位置拆分：一级菜单 `/wiki` 只读；知识库管理（建/改/删）收敛到「团队详情 → 知识库」tab
- **D7** 详情路由团队嵌套：`/team/{teamId}/wiki/{wikiId}/:section`，知识库隶属于团队作用域
- **D8** embedding 模型与维度由 wiki 持有（pgvector 表结构强绑）；元数据生成模型由切片预览区的独立生成操作选择，切片参数在切割步骤（普通/AI）动态传入
- **D9** 首次向量化后 wiki embedding 模型 + 维度被锁定：避免同一 wiki 产生维度不兼容的向量数据；元数据生成模型与切片参数不受锁定影响（分属独立步骤，已在 @WK-S14 明确）
- **D10** `embedding_dimensions` 上限 2000：pgvector 在 2000 以内才可建 hnsw 索引，超过则只支持精确检索；用户需自行权衡
- **D11** 文件抽取统一走 `Maomi.ToMarkdown`（自维护包，1.0.0-alpha.2），不做第二套抽取实现：保证 PDF/Docx/Xlsx/Pptx/HTML/纯文本一致输出 markdown；扩展新格式只需在 `Maomi.ToMarkdown` 内追加 extractor，`WikiCoreModule` 已通过 `AddTextExtraction()` 一次性注册
- **D12** 抽取得到的 markdown 完整存入 `wiki_document_content`：不仅用于切片的"原料"，也是后续文档级编辑/再抽取/审计的依据；同一 `document_id` 始终只有一行，提取/重提取时 upsert
- **D14** 重排序模型（`rerank_model_id`）为 wiki 级**可选**配置：绑定后检索结果先经该模型重排；未绑定则不做重排序。模型类型新增 `rerank`（`AIModelKind.Rerank`，字符串 `rerank`；`AIModelMetaMapper` 按 id/family 含 rerank/reranker 推导，须先于 embedding 判定）
- **D15** 重排序模型与向量化配置解耦：**不受 IsLock 限制**，知识库锁定后仍可绑定/更换/解绑——它不参与向量维度，不会破坏已有向量数据；权限与授权校验（模型启用、类型匹配、公开/团队授权）与 embedding 一致
- **D16** rerank 模型目前只做配置绑定：召回测试（2026-09-21 落地）与检索接口暂未接入重排序，接入为下阶段工作
- **D13** 内容提取自动随上传完成：`CompleteWikiDocumentCommandHandler` 在文档登记成功并提交事务后，立即同步调用 `WikiDocumentProcessingService.ExtractAsync` 用 Maomi.ToMarkdown 抽取 markdown 入库，用户无需在操作页手动提取；提取失败不回滚文档创建（记日志），操作页保留「提取内容/重新提取」作兜底重试。切割与向量化仍是显式步骤：切割（`PartitionAsync` 普通 / `AiPartitionAsync` AI）生成切片后，向量化（`WikiEmbeddingService.ProcessAsync`）复用已有内容+切片，未提取/未切割时 409
- **D17** 普通切割直接暴露 Maomi.ToMarkdown TextSplit 能力：默认保持 Markdown 感知 + 字符计量 + 字符重叠以兼容旧行为；用户可切换递归、固定长度、句子、段落模式，并通过 `Maomi.ToMarkdown.Token` 按 token 计量 chunkSize。AI 切割仍保留独立页签，不与普通切割参数混用
- **D18** 切片元数据生成可独立于向量化执行：切片预览区支持单片与全部切片生成/重生成元数据，结果直接保存到 `wiki_document_chunk_metadata`；向量化勾选元数据时直接复用这些已生成元数据，不再生成元数据
- **D19** 普通切割默认推荐值由 wiki 向量维度决定：`<=384 → 320`、`<=768 → 512`、`<=1024 → 700`、`<=1536 → 900`、`>1536 → 1100`（单位均为 Token）；默认重叠为 1 个句子，用户仍可自由覆盖
- **D20** 向量化只做向量化：文档操作页的向量化表单仅保留「对原文切片向量化 / 对元数据向量化」两个开关（至少选一项），删除元数据生成模型选择器；元数据生成是切片预览区的独立步骤与接口，向量化触发请求体只含 `isEmbedSourceText` 与 `isEmbedMetadata`
- **D21** 默认工作流为 wiki 级 JSON 预设（`default_workflow_config`）：切割/生成元数据/向量化三步各自可空，不触发处理、不参与 IsLock；权限与 rerank 一致（Admin+ 可改，锁定不限制）。2026-09-21 起设置页不再提供编辑入口（产品决策：批量执行实时填参），预设仅作为外部源工作流缺省时的回退值
- **D22** 批量工作流 = 三步开关自由组合（支持单步）：普通切割同步逐文档执行（内容缺失自动提取，重新切割清空旧元数据与向量，已有活跃任务的文档跳过并逐文档报告）；**AI 切割为 LLM 调用，随异步任务执行**（提交时同步兜底提取内容，任务内 AiPartition → 元数据 → 向量化）；元数据生成同样并入任务（`WikiDocumentEmbeddingTaskData` 携带全部步骤参数），`strategyTypes` 多选、空=全套（全套含聚合段），按所选策略过滤元数据类型并以联合 Prompt 单次 LLM 调用；向量化沿用既有任务机制与唯一约束。编排收口在 `WikiWorkflowProcessor`（`IWikiWorkflowProcessor`，组合 `WikiDocumentProcessingService` 与 `WikiEmbeddingService`）。前置不满足（未提取/未切割/无元数据）在提交时逐文档报告而非投递必败任务；重新切割后勾选元数据向量化但未勾选元数据生成的组合在提交时拒绝；单文档失败不影响其他文档
- **D23** 召回测试复用现有检索栈不加新表：查询向量按 wiki 绑定的 embedding 模型生成（显式携带 `EmbeddingDimensions`，与向量化链路一致避免维度不匹配），检索走 `IWikiEmbeddingVectorStore.SearchAsync` 新增的可选 `documentIds` 过滤（`VectorSearchOptions.Filter` 表达式翻译为 pgvector SQL，`DocumentId` 已建索引）；相似度阈值在服务层内存过滤（不依赖 provider 的 ScoreThreshold 支持）。命中项含 `MetadataType`（0=原文 1=大纲 2=问题 3=关键词 4=摘要 5=聚合段）。AI 优化问题 / AI 生成回答走 `IAiChatCompletionService.CompleteTextAsync` 一次性调用，对话模型校验（启用 + conversation 类型 + 公开或团队授权）与元数据生成模型一致；优化失败回退原始查询，无命中时跳过回答调用。门禁在 Handler 校验团队成员（与 wiki 模块既有约定一致），Controller 需显式 `_userContextProvider.SetUserContext(cmd)` 回填 `ContextUserId`（路由参数回填的 command 不是 action 参数，AutoAssignUserIdFilter 不会处理）

- **D24** 外部源 = 知识库级的外部数据入口，类型（`WikiSourceType`：0=飞书文档 / 1=爬虫）**创建后不可变更**；类型专属配置整体存 `wiki_source.config` 的 camelCase JSON（`WikiSourceConfigJson`），因此新增类型只加枚举与反序列化分支，不改表结构
- **D25** 同步基线统一为「内容 SHA-256 哈希比对」：每篇外部文档在 `wiki_source_document.content_hash` 留痕，仅有变化的文档才重新写入并触发工作流；`force=true` 用于人工兜底全量重处理。**无论本页是否有变化都要继续展开其子链接**，否则站点新增页面永远发现不了（2026-09-21 E2E 暴露并修正）
- **D26** 定时/事件/手动三条触发路径共用同一个 `WikiSourceSyncService`：定时任务由 Hangfire `RecuringJobCommand` 承载，key 为 `wiki-source-sync:{sourceId}`（`WikiSourceDefaults.BuildSyncJobKey`）；源被停用或删除时任务自行取消/移除。同步失败只写入源上的 `last_sync_status/message`，不让定时任务反复投递堆栈
- **D27** 飞书渠道一对多、应用渠道独占：外部源以 `FeishuChannelType.WikiSource` + `ChannelId = sourceId` 绑定飞书应用（见 [../feishu/sdd.md](../feishu/sdd.md)），多个外部源可共享同一个飞书应用；团队应用（`App`）渠道仍独占为一对一。删源/改绑必须显式 `UnbindFeishuAppCommand`，避免留下孤儿绑定
- **D28** 外部源工作流复用知识库批量处理口径：源级 `workflow_config` 为空时回退 `wiki.default_workflow_config`（D21），由 `WikiSourceWorkflowRunner` 按三步预设投递既有 `WorkerTask` 管线，不新造第二套执行器。本期前端不提供逐源工作流编辑器，避免与知识库默认值两处口径打架
- **D29** 爬虫抓取范围 = **同 host + 路径前缀限定 + BFS 双重上限**：`ResolvePathPrefix` 在未显式配置前缀时取起始 URL 所在目录；`ExtractLinks` 只放行与起始 URL 同 host 且落在前缀内的链接；`MaxDepth`（上限 10）与 `MaxPages`（上限 2000，默认 200）共同兜底。**深度/页数上限在链接入队与主循环两处同时校验**，保证任何入口都收敛
- **D30** 抓取频率 = **串行 + 强制请求间隔 + 服务端退避**三重限速：单线程 BFS 串行抓取（无并发），每次请求前用 `Task.Delay` 补齐 `RequestIntervalSeconds`（1~3600 秒，默认 1）形成保底间隔；收到 429/503 时读 `Retry-After` 主动退避且不计为失败；**连续失败 5 次触发熔断**提前中止本轮（`result.BreakerTripped`），保护目标站点。`WikiSourceDefaults` 统一承载全部默认值与上下限
- **D31** 爬虫正文 → Markdown 由爬虫服务自行完成：`AngleSharp` 解析 HTML（`ContentSelector` 可指定正文容器，未命中回退 `body`），递归遍历 h1-h6/p/li/pre/code/blockquote/a/table 转 Markdown；`NormalizeUrl` 去掉 fragment 并剔除 `utm_*`/`from`/`spm` 等追踪参数，保证同一页面不因参数差异被重复抓取。**正文为空的页面记 `skipped` 且不生成文档**，但**仍要在入库成功后展开子链接**（自建单类型文件 `CrawlerPageResult`/`WikiSourceContentItem`/`CrawlerSyncResult` 满足 SA1402）
- **D32** 爬虫内容落库复用外部源既有写入链路：`WikiSourceContentWriter.WriteAsync` 统一负责文件上传（ObjectKey `wiki/{wikiId}/source/{sourceId}/{SafeObjectKey}.md`）→ upsert `wiki_document`/`wiki_document_content` → 触发工作流 → upsert `wiki_source_document`，飞书源与爬虫源共用同一实现，`WikiSourceSyncService.SyncCoreAsync` 只按 `SourceType` 分发抓取方式
- **D33** 爬虫与飞书的校验口径分离：`CreateWikiSourceCommand`/`UpdateWikiSourceCommand` 各自 `ValidateCrawler`/`ValidateFeishu`，**爬虫相关规则的 `When` 守卫一律判 `Crawler != null`**（而非 `SourceType == Crawler`），因为 Update 命令的 `SourceType` 是 `[JsonIgnore]` 且不可变更；`Crawler` 配置内的每一条规则都必须有守卫，否则非爬虫源提交会抛 `NullReferenceException`（2026-09-21 E2E 暴露并修正）。**仅当爬虫配置非空时才校验其字段**，同理飞书源更新不下发 `crawler`
- **D34** 外部源前端权限取自 props 的初始角色并只接受后端 `myRole` 的升级：`WikiSources` 以 `myRole` prop 初始化 `role` 状态，避免接口返回前把管理按钮渲染成可点；`getWikiSources` 返回 `myRole` 时覆盖。**注意 antd 5.28 的 `Button` 用原生 `disabled` 属性表达禁用态，不再挂 `ant-btn-disabled` 类名**（前端断言需按属性判定）

## 7. 已知问题 / 下阶段

- 内容/文档层：`wiki_document` 等实体已重做为文件接入模型（ObjectKey/FileName/向量化），相关 API 与前端已落地
- 旧文本文档接口（`/documents`、`/document/{id}`）及前端文档页/编辑器已移除
- `wiki-e2e.mjs` 覆盖知识库 CRUD + 文档 CRUD；`wiki-embedding-e2e.mjs` 覆盖 提取→切割→向量化（WK-S15/S16/S19/S21/S22）已加脚本，待后端可运行时联调
- 文档操作页（提取/切割/切片预览/向量化）已落地；召回测试已落地（2026-09-21，`POST /wiki/{id}/recall-test` + 详情页召回测试 tab，证据 `local-dev/wiki-recall-e2e.mjs` PASS 30/0/0）；文档检索接口（供应用引用的独立查询端点）与 rerank 模型接入召回为下阶段工作（D16/D23）
- **D11/D12/D13** 抽取 → `wiki_document_content` → `wiki_document_chunk_content` → `wiki_embedding_{wikiId}` 的整条流水线已拆分为三步独立操作，抽取/切割在 `WikiDocumentProcessingService`，向量化在 `WikiEmbeddingService`
