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
- 2026-09-20（二）AI 切割 + 多选策略：①工作流切割预设增 `mode`（`WorkflowPartitionMode` normal/ai，缺省 normal 兼容旧 JSON）、`aiModelId/promptTemplate`；批量命令增 `isAiPartition/aiModelId/promptTemplate`（AI 切割时切片参数免传、校验对话模型存在/类型/团队授权），AI 切割为 LLM 调用随**异步任务**执行（普通切割仍同步），内容提取仍在提交时同步兜底；②元数据策略改多选：`strategyTypes` 列表（空=全套，且全套补齐了原「空策略」缺失的聚合段生成），按所选策略过滤元数据类型（1←大纲 2←问题 3/4←关键词摘要 5←聚合段），多字段联合 Prompt 单次 LLM 调用；③编排收口——删除 `IWikiEmbeddingProcessor`，新增 `IWikiWorkflowProcessor/WikiWorkflowProcessor`（AI 切割→元数据→向量化逐步编排，注入两个既有领域服务），消费者改调工作流处理器，`WikiEmbeddingService.ProcessAsync` 还原为纯向量化签名。E2E 扩至 **PASS 28 / FAIL 0 / SKIP 5**（新增 S35a…c AI 切割校验/提交、S27g 多选策略回读、S36 多选策略提交）；前端切割方式 Radio（普通/AI）+ AI 模型/提示词字段 + 策略多选 Select，vitest 405/405
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
| @WK-S9 | wiki-e2e.mjs（WK-09a-d） | 待后端可运行实测 |
| @WK-S10 | wiki-e2e.mjs（WK-10） | 待后端可运行实测 |
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
| @WK-S27/S28 | wiki-workflow-e2e.mjs（WK-S27a…f、S27g、S28a…d）+ BatchWorkflowModal.test.tsx | PASS（2026-09-20，5010 实例；S28b/c 提交语义 PASS，S28d 完成断言受环境模型渠道限制见下） |
| @WK-S29 | wiki-workflow-e2e.mjs（WK-S29a…c：只切割无任务、有切片/无元数据/未向量化） | PASS（2026-09-20） |
| @WK-S30/S31 | wiki-workflow-e2e.mjs（WK-S30a、S31a 提交语义） | S30a/S31a PASS（2026-09-20）；完成断言与 @WK-S28d 同因环境跳过 |
| @WK-S32/S33/S34 | wiki-workflow-e2e.mjs（WK-S32a/b、S33a…e、S34a…d） | PASS 10/10（2026-09-20） |
| @WK-S35/S36 | wiki-workflow-e2e.mjs（WK-S35a…c：AI 切割校验与提交、无需切片参数；WK-S36：多选策略提交） | PASS 5/5（2026-09-20；AI 切割/元数据在任务内的执行依赖模型渠道，完成断言同环境限制） |
| 批量工作流前端 | ui/src/pages/wiki/BatchWorkflowModal.tsx + __tests__/BatchWorkflowModal.test.tsx + WikiWorkflowSettings.tsx | PASS 3/3（2026-09-20，新增用例）；全仓 vitest 404/404 |
| 文档操作页 | ui/src/pages/wiki/WikiDocumentDetail.tsx | 已实现（tsc/eslint/vitest 全绿） |

> 备注：2026-09-08 将内容处理拆为「上传自动提取 / 切割（普通|AI）/ 向量化」：提取随 `CompleteWikiDocument` 自动完成（D13 修订），切片参数由切割步骤（普通/AI）传入，向量化仅复用已有内容+切片，对应 @WK-S15/S16/S18/S19/S21/S22。2026-09-09 普通切割扩展为 Maomi.ToMarkdown 多模式（Markdown/递归/固定长度/句子/段落）并引入 `Maomi.ToMarkdown.Token` 支持按 Token 计量。
> 2026-09-08 文件提取改用 `Maomi.ToMarkdown`（D11），抽取后的 markdown 完整存入 `wiki_document_content`（D12）。
