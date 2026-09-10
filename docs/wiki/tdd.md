# 知识库模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/wiki/MoAI.Wiki.Api/MoAI.Wiki.Api.csproj --no-restore` → **0 错误**（2026-09-09；沙箱内 `--no-restore` 可用，完整 restore 仍被 NuGet ConfigurationDefaults 阻断，需系统终端复验）
- 前端：`npm run typecheck` 全绿；`WikiDocumentDetail.test.tsx` **22/22**（含推荐切片长度默认值、句子/段落切割重叠单位联动、AI 切割失败反馈、切片预览单片/批量生成元数据与未选模型提示、向量化仅提交原文/元数据开关且至少选一项）；`eslint` 0 error
- E2E：`node local-dev/wiki-e2e.mjs` → 覆盖 @WK-S1…@WK-S10（含公开只读场景）；`node local-dev/wiki-embedding-e2e.mjs` → 覆盖 @WK-S15/S16/S19/S21/S22（切割 → 向量化全流程），需后端 :5210 运行
- 文档操作页走查：上传文档后内容已自动提取入库（无需手动提取）→ 普通切割 / AI 切割 → 切片预览卡片（独立生成元数据）→ 向量化（仅原文/元数据开关，元数据生成模型选择器已移除）；「提取内容/重新提取」保留为自动提取失败的兜底重试
- 2026-09-09「文档内容」卡改版：文件名/提取状态/重新提取移至卡片右上角（extra），正文仅保留单行摘要；完整内容改为「查看内容」模态窗查看（截断时打开自动惰性加载全文），不再在页面内展开占高
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
| 文档操作页 | ui/src/pages/wiki/WikiDocumentDetail.tsx | 已实现（tsc/eslint/vitest 全绿） |

> 备注：2026-09-08 将内容处理拆为「上传自动提取 / 切割（普通|AI）/ 向量化」：提取随 `CompleteWikiDocument` 自动完成（D13 修订），切片参数由切割步骤（普通/AI）传入，向量化仅复用已有内容+切片，对应 @WK-S15/S16/S18/S19/S21/S22。2026-09-09 普通切割扩展为 Maomi.ToMarkdown 多模式（Markdown/递归/固定长度/句子/段落）并引入 `Maomi.ToMarkdown.Token` 支持按 Token 计量。
> 2026-09-08 文件提取改用 `Maomi.ToMarkdown`（D11），抽取后的 markdown 完整存入 `wiki_document_content`（D12）。
