# 提示词（Prompt）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/prompt-e2e.mjs](../../local-dev/prompt-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景复述见 BDD，本文只做编号→验证物映射。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @PT-S1 | local-dev/prompt-e2e.mjs#PT-01~03 | PASS 46/46（2026-09-15） |
| @PT-S2 | local-dev/prompt-e2e.mjs#PT-02 | PASS 46/46（2026-09-15） |
| @PT-S3 | local-dev/prompt-e2e.mjs#PT-04~05 | PASS 46/46（2026-09-15） |
| @PT-S4 | local-dev/prompt-e2e.mjs#PT-06 | PASS 46/46（2026-09-15） |
| @PT-S5 | local-dev/prompt-e2e.mjs#PT-15a/b | PASS 46/46（2026-09-15） |
| @PT-S6 | local-dev/prompt-e2e.mjs#PT-07 | PASS 46/46（2026-09-15） |
| @PT-S7 | local-dev/prompt-e2e.mjs#PT-08~09a/b | PASS 46/46（2026-09-15） |
| @PT-S8 | local-dev/prompt-e2e.mjs#PT-09c/d | PASS 46/46（2026-09-15） |
| @PT-S9 | local-dev/prompt-e2e.mjs#PT-10 | PASS 46/46（2026-09-15） |
| @PT-S10 | local-dev/prompt-e2e.mjs#PT-11 | PASS 46/46（2026-09-15） |
| @PT-S11 | local-dev/prompt-e2e.mjs#PT-12 | PASS 46/46（2026-09-15） |
| @PT-S12 | local-dev/prompt-e2e.mjs#PT-13 | PASS 46/46（2026-09-15） |
| @PT-S13 | local-dev/prompt-e2e.mjs#PT-14 | PASS 46/46（2026-09-15） |
| @PT-S14 | local-dev/prompt-e2e.mjs#PT-15c/d | PASS 46/46（2026-09-15） |
| @PT-S15 | ui/src/pages/prompts/__tests__/PromptCenter.test.tsx#市场 Tab 渲染/切换我的提示词 | PASS 11/11（2026-09-16，全仓 vitest 270/270） |
| @PT-S16 | ui/src/pages/prompts/__tests__/PromptCenter.test.tsx#分类过滤/新建跳转/申请上架/删除 | PASS 11/11（2026-09-16，全仓 vitest 270/270） |
| @PT-S18 | ui/src/pages/prompts/__tests__/PromptEditor.test.tsx#实时预览/新建/编辑回填/团队新建 | PASS 4/4（2026-09-15，全仓 vitest 267/267） |
| @PT-S20 | ui/src/pages/prompts/__tests__/markdown.test.ts + PromptEditor.test.tsx#工具栏 | PASS 283/283（2026-09-16，全仓 vitest） |
| @PT-S19 | local-dev/prompt-e2e.mjs#PT-16 | PASS 46/46（2026-09-15） |
| @PT-S17 | @manual（浏览器走查，见 sop.md 第 3 节） | PASS（2026-09-16 浏览器实测市场卡片与编辑器工具栏/预览，团队分区未改动） |

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                             # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test    # 前端全绿
node local-dev/prompt-e2e.mjs                                 # PT 40/40（需后端运行中）
node local-dev/publication-e2e.mjs                            # PB 34/34 回归（publication 个人分支改动）
```

## 本轮修复记录

- 2026-09-16 编辑保存 400（`$.promptId` 无法转 Int32）：`UpdatePromptCommand.PromptId` 暴露在 PUT 请求体 schema 中，Kiota 把前端传入的 null 原样序列化导致 System.Text.Json 绑定失败。修复：后端 `PromptId` 加 `[JsonIgnore]`（请求体不再携带，与"路由提供"决策一致）；前端 `updatePrompt` 改传真实 id 立即兼容运行中后端。**待后端重启后执行 `npm run syncapi`，再删除封装层中的 `promptId` 字段**。


- `UpdatePromptCommand` 不校验 `PromptId`（路由提供，body 携带 0 导致 PUT 400）。
- E2E PT-14 计数断言改为 `market_list` 读数（detail GET 自身会 +1）。
