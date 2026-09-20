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
| @PT-S18 | ui/src/pages/prompts/__tests__/PromptEditor.test.tsx#实时预览/新建/编辑回填/团队新建/工具栏 | PASS 6/6（2026-09-18，全仓 vitest 330/330） |
| @PT-S20 | ui/src/pages/prompts/__tests__/markdown.test.ts + PromptEditor.test.tsx#工具栏 | PASS 283/283（2026-09-16，全仓 vitest） |
| @PT-S19 | local-dev/prompt-e2e.mjs#PT-16 | PASS 46/46（2026-09-15） |
| @PT-S21 | ui/src/pages/teams/prompts/__tests__/TeamPrompts.test.tsx | PASS 3/3（2026-09-18，全仓 vitest 330/330） |
| @PT-S17 | @manual（浏览器走查，见 sop.md 第 3 节） | PASS（2026-09-16 市场/编辑器走查；2026-09-18 详情弹窗与团队分区改版，待复走查） |
| @PT-S22 | 后端单测（绑定点计数：CreateAppSession/UpdateAppSessionPrompt Handler）+ API 实测 top_used 排序 | PASS（2026-09-20，浏览器实测 top10 返回与使用次数展示） |

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                             # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test    # 前端全绿
node local-dev/prompt-e2e.mjs                                 # PT 40/40（需后端运行中）
node local-dev/publication-e2e.mjs                            # PB 34/34 回归（publication 个人分支改动）
```

## 本轮修复记录

- 2026-09-18 团队提示词分区交互改版（纯前端）：固定卡片列表（新建/刷新并入查询行，不展示条数与视图切换）、卡片按被使用次数降序、分类筛选改为头部固定分类列表并显示 emoji（`classifyLabel`）、`PromptDetailModal` 去背景色/边框改横线分隔并新增复制按钮；提示词中心（市场/我的）同步：卡片一行 6 张、去掉「共 N 条」、分类 chip 显示 emoji；新增 [@PT-S21](./bdd.md#pt-s21) 与 TeamPrompts 组件测试；设计系统 `QueryBar` 新增 `extra` 插槽（[@FE-CB-S19](../components-base/bdd.md#fe-cb-s19)）。
- 2026-09-16 编辑保存 400（`$.promptId` 无法转 Int32）：`UpdatePromptCommand.PromptId` 暴露在 PUT 请求体 schema 中，Kiota 把前端传入的 null 原样序列化导致 System.Text.Json 绑定失败。修复：后端 `PromptId` 加 `[JsonIgnore]`（请求体不再携带，与"路由提供"决策一致）；前端 `updatePrompt` 改传真实 id 立即兼容运行中后端。**待后端重启后执行 `npm run syncapi`，再删除封装层中的 `promptId` 字段**。


- `UpdatePromptCommand` 不校验 `PromptId`（路由提供，body 携带 0 导致 PUT 400）。
- E2E PT-14 计数断言改为 `market_list` 读数（detail GET 自身会 +1）。
