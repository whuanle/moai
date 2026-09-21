# 知识库模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/wiki/MoAI.Wiki.Api/MoAI.Wiki.Api.csproj --no-restore` → **0 错误**（2026-09-09；沙箱内 `--no-restore` 可用，完整 restore 仍被 NuGet ConfigurationDefaults 阻断，需系统终端复验）
- 前端：`npm run typecheck` 全绿；`WikiDocumentDetail.test.tsx` **22/22**（含推荐切片长度默认值、句子/段落切割重叠单位联动、AI 切割失败反馈、切片预览单片/批量生成元数据与未选模型提示、向量化仅提交原文/元数据开关且至少选一项）；`eslint` 0 error
- E2E：`node local-dev/wiki-e2e.mjs` → 覆盖 @WK-S1…@WK-S10（含公开只读场景）；`node local-dev/wiki-embedding-e2e.mjs` → 覆盖 @WK-S15/S16/S19/S21/S22（切割 → 向量化全流程），需后端 :5210 运行
- 文档操作页走查：上传文档后内容已自动提取入库（无需手动提取）→ 普通切割 / AI 切割 → 切片预览卡片（独立生成元数据）→ 向量化（仅原文/元数据开关，元数据生成模型选择器已移除）；「提取内容/重新提取」保留为自动提取失败的兜底重试
- 2026-09-17 上传大小上限：`WIKI_MAX_FILE_SIZE_MB`（默认 50，0 表示不限制）经 `IWikiSettingsService` 读取，内外部 preupload 双入口校验 + `GET /api/wiki/upload-limit` 供前端预检（WikiDocuments 选择文件即拦截）；wiki-e2e 扩至 **32/32**（WK-11 四项），顺手修复脚本沿用旧枚举值的 bob 角色号（role 2→0，Member 应为 0）与 WK-04b myRole 断言；浏览器走查设置页新「知识库」卡片保存/回显/折叠通过
- 2026-09-17 默认值调整：`WIKI_MAX_FILE_SIZE_MB` 默认 0（不限制）→ **50**（SettingDefinitions + i18n 描述 + 前端解析兜底同步）；WK-11d 改为恢复默认 50 断言，e2e 复跑 **32/32**
- 2026-09-09「文档内容」卡改版：文件名/提取状态/重新提取移至卡片右上角（extra），正文仅保留单行摘要；完整内容改为「查看内容」模态窗查看（截断时打开自动惰性加载全文），不再在页面内展开占高
- 2026-09-20 默认工作流 + 批量处理：新增 `PUT /wiki/{id}/workflow-config`（Admin+，整体覆盖保存三步预设）与 `POST /wiki/{id}/documents/batch-workflow`（团队成员，1-50 文档，三步布尔开关自由组合=支持单步执行）。切割同步逐文档执行（内容缺失自动提取；已有活跃任务的文档跳过并报告冲突）；元数据生成并入向量化 WorkerTask（`WikiDocumentEmbeddingTaskData` 增 `MetadataModelId/MetadataStrategyType`，`IWikiEmbeddingProcessor.ProcessAsync` 先生成元数据再按需向量化，仅元数据生成时两者全关合法）；向量化沿用既有任务机制。schema：`wiki.default_workflow_config`（asserts/wiki_workflow.sql，开发库已执行）。E2E 最终 **PASS 22 / FAIL 0 / SKIP 5**（SKIP 因共享环境模型渠道不可用：3 个 embedding 模型分别渠道 404 / 不支持 dimensions 参数；对话渠道全部对 PreferJsonResponse 返回 SSE 文本 → `'d' is an invalid start of a value` 502——与本特性代码无关，配齐可用模型后重跑即可闭环 S28d/S30b/S31b）；历史轮曾 26 PASS（当时首列对话模型可提交，完整提交语义已验证）。构建用 `-o` 独立输出 + `MoAI__Port=5010`（宿主 bin 被并行实例锁定）
- 2026-09-21 设置页「默认工作流」卡移除（产品决策：批量执行实时填参）：删 `WikiWorkflowSettings` 组件与设置页挂载、`updateWikiWorkflowConfig` 前端封装、`wiki.workflow` 设置类 i18n 键（title/settingsHint/save/saveSuccess）；`BatchWorkflowModal` 改为每次打开按通用默认值预填（切割 + 向量化，chunk 1000 / 重叠 50），vitest 同步改默认值断言。后端 `PUT workflow-config`、`wiki.default_workflow_config` 与外部源回退保留不变（WK-S27 e2e 继续有效）。
- 2026-09-20（二）AI 切割 + 多选策略：①工作流切割预设增 `mode`（`WorkflowPartitionMode` normal/ai，缺省 normal 兼容旧 JSON）、`aiModelId/promptTemplate`；批量命令增 `isAiPartition/aiModelId/promptTemplate`（AI 切割时切片参数免传、校验对话模型存在/类型/团队授权），AI 切割为 LLM 调用随**异步任务**执行（普通切割仍同步），内容提取仍在提交时同步兜底；②元数据策略改多选：`strategyTypes` 列表（空=全套，且全套补齐了原「空策略」缺失的聚合段生成），按所选策略过滤元数据类型（1←大纲 2←问题 3/4←关键词摘要 5←聚合段），多字段联合 Prompt 单次 LLM 调用；③编排收口——删除 `IWikiEmbeddingProcessor`，新增 `IWikiWorkflowProcessor/WikiWorkflowProcessor`（AI 切割→元数据→向量化逐步编排，注入两个既有领域服务），消费者改调工作流处理器，`WikiEmbeddingService.ProcessAsync` 还原为纯向量化签名。E2E 扩至 **PASS 28 / FAIL 0 / SKIP 5**（新增 S35a…c AI 切割校验/提交、S27g 多选策略回读、S36 多选策略提交）；前端切割方式 Radio（普通/AI）+ AI 模型/提示词字段 + 策略多选 Select，vitest 405/405
- 2026-09-21 召回测试：新增 `POST /wiki/{id}/recall-test`（Query，Handler 校验团队成员；`query/documentIds?/top(1-50)/minScore?(0-1)/aiModelId?/isOptimizeQuery/isAnswer`，开启 AI 时校验团队可用对话模型）。检索链路复用现有栈：`IWikiEmbeddingVectorStore.SearchAsync` 增可选 `documentIds` 过滤（`VectorSearchOptions.Filter` 表达式→pgvector SQL，`DocumentId` 已索引），`IWikiSearchService` 增 `SearchInWikiAsync`（单库召回 + 阈值内存过滤 + `MetadataType`/文档名回填，查询向量显式携带 `EmbeddingDimensions`）；AI 优化问题/生成回答走 `IAiChatCompletionService.CompleteTextAsync`，优化结果回退原始查询、无命中跳过回答。前端落地详情页「召回测试」tab（`WikiRecallTest.tsx`，替换占位），`WikiDetail.test.tsx` 占位断言改表单断言，新增 `WikiRecallTest.test.tsx` 6 例。**踩坑**：路由参数回填的 command 不是 action 参数，`AutoAssignUserIdFilter` 不填充 `ContextUserId`，Controller 必须显式 `_userContextProvider.SetUserContext(cmd)`（首跑 owner/member 全 404 即此因）。E2E 新增 `local-dev/wiki-recall-e2e.mjs`：内置本地 OpenAI 兼容桩（`/v1/embeddings` 确定性哈希向量 + `/v1/chat/completions` 固定文案，admin 建渠道+模型并授权，结束清理），无真实模型环境也能全链路闭环；**最终 PASS 30 / FAIL 0 / SKIP 0**（参数校验 8 + 门禁 1 + 前置 5 + 召回/范围/阈值 9 + AI 优化/回答 4 + 团队/上传前置 3）。dotnet build 0 error、typecheck/lint 0 error、vitest 411/411、浏览器走查通过（双栏 playground 布局：左 400px 表单栏 + 右结果栏自适应宽屏，窄屏（md 断点以下）纵向堆叠；未搜索时结果区展示引导空态，开关联动模型必填，优化问题 Alert、AI 回答、命中项得分/文档/元数据类型渲染均验证）
- 2026-09-21（二）走查修订——修复思考型模型 AI 回答为空 + 头部布局重做：①`IsAnswer` 的 `CompleteTextAsync` 未传 `DisableThinking`，走 MAF `GetResponseAsync` 时思考型模型（如 Hy4 preview）把输出预算耗在思维链，`response.Text` 返回空串导致前端无回答；补 `DisableThinking = true`（与 AI 优化同路径）。②AI 回答改为右栏顶部醒目卡片（colorInfoBg 背景 + 机器人图标 + "由 {model} 生成"署名 + 14px/1.8 行高正文）；优化后的问题独立 Alert（问题文本加粗）；召回结果标题带条数；开启回答但 answer 为空时区分提示（有命中→warning 更换模型、无命中→info 已跳过）；搜索中显示状态条。E2E 复跑 30/30（5014），浏览器走查通过；vitest 412 例（WikiRecallTest 7 例含空回答提示）；全量 vitest 与并行会话共用机器时偶发超时（Classify/WikiDocumentDetail/AppWorkspace 单跑全过），为负载噪音
- 2026-09-21 外部源爬虫能力（限速抓取 + 定时/后台自动抓取）：在既有外部源（飞书文档）之上新增 `WikiSourceType.Crawler`，抓取范围「同 host + 路径前缀 + BFS 深度/页数双重上限」（sdd D29），抓取频率「串行 + 强制请求间隔 + 429/503 按 Retry-After 退避 + 连续失败熔断」（D30），正文经 AngleSharp 解析后自行转 Markdown 并由 `WikiSourceContentWriter` 复用外部源既有写入链路落库（D31/D32）。**E2E 首跑暴露两处后端真实缺陷并修复**：①`ValidateCrawler` 的 `UserAgent`/`ContentSelector`/`PathPrefix` 规则缺 `Crawler != null` 守卫 → 非爬虫源提交 500（D33）；②内容未变化/跳过分支 `EnqueueChildren` 未传参 → 二次同步只抓 1 页（D25/D31）。`local-dev/wiki-source-e2e.mjs` **PASS 66 / FAIL 0**（对比修复前 39/21）；前端新增 `WikiSources.tsx` 等 4 文件 + 10 例 vitest；`docs/wiki` 四件套同步（bdd @WS-S14…S17、sdd D29…D34）。
- 2026-09-21 外部源前端权限空档期修复：`WikiSources` 原以 `role=null` 起步，`isAdminPlus` 恒 false，接口返回前会把「新建/编辑/删除/启停」渲染成可点（后端仍会拦，但属前端门禁失效）。改为以 `myRole` prop 作为初始 role，仅接受后端 `myRole` 的覆盖（D34）。同步修正测试断言：antd 5.28 的 `Button` 用原生 `disabled` 属性表达禁用态，`ant-btn-disabled` 类名已不再使用。
- 2026-09-21 外部源更新不透传爬虫配置：`WikiSources.handleSubmit` 在更新飞书源时剔除 `crawler` 字段，避免后端按爬虫口径校验一份并未使用的配置（与 D33「仅当配置非空才校验」配套）。
- 2026-09-21 外部源新建交互改版（产品决策）：取消「新建外部源」单一入口 + 抽屉内 `Segmented` 类型切换，改为「新建飞书源 / 新建爬虫」双按钮各自打开对应类型的 `Modal` 表单（编辑共用该弹窗，类型只读 Tag 展示）；类型由 `activeType` 状态驱动，隐藏 `sourceType` 表单字段与 `Form.useWatch` 移除，i18n 删 `wiki.source.create/createTitle` 增 `createFeishu/createCrawler`（zh/en 同步）。vitest `WikiSources.test.tsx` 11 例（原 10 例：抽屉切换用例拆为双入口两例）。typecheck/lint 0 error，全仓 vitest 423/423（4 个 unhandled rejection 为既有测试套件连 `127.0.0.1:3000` 被拒的环境噪音，与本改版无关）。
- 2026-09-21 知识库去掉「公开」字段 + 卡片统计（2026-09-21，本轮）：产品决策「团队的知识库不存在公开」。后端删 `WikiEntity.IsPublic` 全链路（实体/EF 映射、Create/Update 命令、`WikiItem`/`QueryWikiCommandResponse`、`QueryWikiCommandHandler` 非成员公开只读分支、内外部列表投影、控制器映射），`QueryWikiCommandHandler` 非成员一律 404；`WikiItem` 新增 `documentCount/chunkCount/lastDocumentUpdateTime`（EF 关联子查询统计 `wiki_document` / `wiki_document_chunk_content`）。存量库删列 `asserts/wiki_drop_is_public.sql`（开发库已执行；新库 EnsureCreated 直接建成）。前端：TeamWikis 卡片以「文件/切片/最近更新」统计条替换公开 Tag、表单删公开开关，WikiDetail 设置页同步删公开字段与成员只读视图展示，`api/wiki.ts` 封装去 `isPublic`，i18n 删 `wiki.public*` 增 `wiki.stat*`（zh/en）。E2E：wiki-e2e WK-09 公开只读场景改写为非成员 404 + 新增 WK-10b 统计断言，**PASS 32/32**（5033 独立实例）；wiki-external-e2e **30/30**、workflow-e2e **118/118**；vitest 全仓 424/424（新增 TeamWikis.test 2 例），typecheck/lint 0 error，浏览器走查通过（卡片统计 + 编辑弹窗仅名称/简介）。
- 抽取与入库链路证据：`CompleteWikiDocumentCommandHandler` 在事务提交后调用 `WikiDocumentProcessingService.ExtractAsync`，通过 `Maomi.ToMarkdown.TextExtractionService.ExtractAsync(stream, fileName, ct)` 抽取，结果 upsert 入 `wiki_document_content`（key 为 `document_id`），实现上传即自动提取；`PartitionAsync` 按 Maomi.ToMarkdown TextSplit 配置切分写 `wiki_document_chunk_content`，支持 Token 计量（`Maomi.ToMarkdown.Token`）；`WikiEmbeddingService.ProcessAsync` 复用已有内容+切片，最后由 `WikiEmbeddingTableService` 写入动态 `wiki_embedding_{wikiId}` 表

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @WK-S1 | wiki-e2e.mjs（WK-01） | PASS（2026-09-02） |
| @WK-S2 | wiki-e2e.mjs（WK-02a-c） | PASS（2026-09-02） |
| @WK-S3 | wiki-e2e.mjs（WK-03a/b） | PASS（2026-09-02） |
| @WK-S4 | wiki-e2e.mjs（WK-04a-c） | PASS（2026-09-02） |
| @WK-S5 | wiki-e2e.mjs（WK-05a/b） | PASS（2026-09-02） |
| @WK-S6 | wiki-e2e.mjs（WK-06a-c） | PASS（2026-09-02） |
| @WK-S7 | wiki-e2e.mjs（WK-07a-d） | PASS（2026-09-02） |
| @WK-S8 | wiki-e2e.mjs（WK-08a-e） | PASS（2026-09-02） |
| @WK-S9 | （场景已删除：公开概念 2026-09-21 移除，编号不复用） | — |
| @WK-S10 | wiki-e2e.mjs（WK-09a…c、WK-10：非成员详情/更新/删除一律 404） | PASS（2026-09-21，5033 实例） |
| @WK-S40 | wiki-e2e.mjs（WK-10b：列表项含 documentCount/chunkCount/lastDocumentUpdateTime）+ TeamWikis.test.tsx | PASS 32/32 + vitest 2/2（2026-09-21） |
| @WK-S11/S12 | ui/src/pages/wiki/__tests__/Wiki.test.tsx | PASS 5/5（2026-09-07） |
| @WK-S13/S14/S17 | ui/src/pages/wiki/__tests__/WikiDetail.test.tsx | PASS 18/18（2026-09-08 修复后） |
| @WK-S15/S16 | wiki-embedding-e2e.mjs（WK-S15a/b/c、S16a/b；S16a 覆盖 recursive + token） | 待后端 :5210 实测（脚本已就绪） |
| @WK-S18 | ui/src/pages/wiki/WikiDocumentDetail.tsx + WikiDocumentDetail.test.tsx | PASS 22/22（2026-09-10 调整：向量化表单移除元数据生成模型选择器，仅保留原文/元数据开关且至少选一项；2026-09-09 普通切割表单支持 Maomi.ToMarkdown 多模式/Token 参数、默认推荐值、重叠单位联动和 AI 切割失败反馈） |
| @WK-S19 | wiki-embedding-e2e.mjs（WK-S19a…f） | 待后端 :5210 实测；覆盖 提取入库 isContentExtracted=true + 30s 内 embeddingCount>0 + 切片顺序自增 + 内容含原 markdown + wiki 模型 id 不变 |
| @WK-S20 | WikiDocumentProcessingService.ExtractAsync → `NotSupportedException`（不支持类型拒绝提取） | 单元/手工待补 |
| @WK-S21 | wiki-embedding-e2e.mjs（WK-S21a/b） | 待后端 :5210 实测；覆盖 未提取/未切割向量化 409 |
| @WK-S22 | wiki-embedding-e2e.mjs（WK-S16b 向量化复用内容+切片） | 待后端 :5210 实测 |
| @WK-S23/S24 | ui/src/pages/wiki/__tests__/WikiDetail.test.tsx（重排序模型绑定 / 解绑 / 锁定后仍可改） | PASS（2026-09-09） |
| @WK-S25 | ui/src/pages/wiki/WikiDocumentDetail.tsx + WikiDocumentDetail.test.tsx；`dotnet build src/wiki/MoAI.Wiki.Core/MoAI.Wiki.Core.csproj` | PASS 22/22 + build 0 error（2026-09-10） |
| @WK-S26 | wiki-e2e.mjs（WK-11a…d：设 1MB 后 upload-limit 成员可读、超限 400、恰好 1MB 200、恢复 0 后 2MB 200） | **PASS 32/32（2026-09-17，5020 实例）** |
| @WK-S27/S28 | wiki-workflow-e2e.mjs（WK-S27a…f、S27g、S28a…d）+ BatchWorkflowModal.test.tsx | PASS 28/0/5（2026-09-21，5210 独立实例；SKIP 均为环境无可用模型。2026-09-20：S28b/c 提交语义 PASS，S28d 完成断言受环境模型渠道限制） |
| @WK-S29 | wiki-workflow-e2e.mjs（WK-S29a…c：只切割无任务、有切片/无元数据/未向量化） | PASS（2026-09-20） |
| @WK-S30/S31 | wiki-workflow-e2e.mjs（WK-S30a、S31a 提交语义） | S30a/S31a PASS（2026-09-20）；完成断言与 @WK-S28d 同因环境跳过 |
| @WK-S32/S33/S34 | wiki-workflow-e2e.mjs（WK-S32a/b、S33a…e、S34a…d） | PASS 10/10（2026-09-20） |
| @WK-S35/S36 | wiki-workflow-e2e.mjs（WK-S35a…c：AI 切割校验与提交、无需切片参数；WK-S36：多选策略提交） | PASS 5/5（2026-09-20；AI 切割/元数据在任务内的执行依赖模型渠道，完成断言同环境限制） |
| @WK-S37 | wiki-recall-e2e.mjs（WK-S37a 空查询/空白查询/top=0/top=51/minScore=1.5/AI 缺模型/非法文档 id 400、WK-S37b 非成员 404） | PASS 8/8（2026-09-21，5013 实例） |
| @WK-S38 | wiki-recall-e2e.mjs（前置绑定+上传+切割+向量化 5 项；WK-S38a 全库召回/降序/文档名/切片 id、WK-S38b 范围过滤三断言、WK-S38c 阈值两断言、WK-S38d 成员 top=3） | PASS 14/14（2026-09-21；本地桩 embedding 闭环，不依赖外部模型渠道） |
| @WK-S39 | wiki-recall-e2e.mjs（WK-S39a AI 优化问题三项、WK-S39b AI 生成回答、WK-S39c 双开关） | PASS 5/5（2026-09-21；本地桩 conversation 闭环） |
| 召回测试前端 | ui/src/pages/wiki/WikiRecallTest.tsx + __tests__/WikiRecallTest.test.tsx（6 例）+ WikiDetail.test.tsx（召回 tab 表单断言） | PASS 6/6 + 全仓 vitest 411/411（2026-09-21）；浏览器走查通过 |
| @WS-S1…S17 | local-dev/wiki-source-e2e.mjs（自带本地站点桩，走真实爬虫链路；飞书源用假凭证走失败兜底路径） | **PASS 66/66（2026-09-21，5000 实例；连续两次执行结果一致）**。首跑 PASS 39/FAIL 21，暴露并修复两处后端缺陷：①`ValidateCrawler` 中 `UserAgent`/`ContentSelector`/`PathPrefix` 三条规则缺 `When(x => Crawler != null)` 守卫，非爬虫源提交抛 `NullReferenceException`（500）——统一改为判 `Crawler != null`（sdd D33）；②爬虫内容未变化/跳过分支的 `EnqueueChildren` 未传参，导致父页无变化时子链接不再展开，二次同步只抓到索引页 1 篇（sdd D25/D31）。另修复前端权限空档期（`myRole` prop 未作为初始 role，接口返回前管理按钮可点，sdd D34） |
| 外部源前端 | ui/src/pages/wiki/WikiSources.tsx + WikiSourceCrawlerForm.tsx + WikiSourceWorkflowFields.tsx + wikiSourceCrawler.ts + __tests__/WikiSources.test.tsx（11 例）+ src/api/wiki.ts（Kiota 封装层） | **PASS 11/11（2026-09-21）**；wiki 目录 65/65；tsc 0 error / eslint 0 error；全仓 vitest 423 例（4 个 unhandled rejection 为环境噪音，见自检记录）。**踩坑**：antd 5.28 的 `Button` 用原生 `disabled` 属性表达禁用态，不再挂 `ant-btn-disabled` 类名，测试断言须按属性判定 |
| 批量工作流前端 | ui/src/pages/wiki/BatchWorkflowModal.tsx + __tests__/BatchWorkflowModal.test.tsx | PASS 3/3（2026-09-20，新增用例）；全仓 vitest 404/404（`WikiWorkflowSettings.tsx` 已于 2026-09-21 按产品决策移除） |
| 文档操作页 | ui/src/pages/wiki/WikiDocumentDetail.tsx | 已实现（tsc/eslint/vitest 全绿） |

> 备注：2026-09-08 将内容处理拆为「上传自动提取 / 切割（普通|AI）/ 向量化」：提取随 `CompleteWikiDocument` 自动完成（D13 修订），切片参数由切割步骤（普通/AI）传入，向量化仅复用已有内容+切片，对应 @WK-S15/S16/S18/S19/S21/S22。2026-09-09 普通切割扩展为 Maomi.ToMarkdown 多模式（Markdown/递归/固定长度/句子/段落）并引入 `Maomi.ToMarkdown.Token` 支持按 Token 计量。
> 2026-09-08 文件提取改用 `Maomi.ToMarkdown`（D11），抽取后的 markdown 完整存入 `wiki_document_content`（D12）。
