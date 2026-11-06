# 团队智能体模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/team-agent-e2e.mjs](../../local-dev/team-agent-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 错误（2026-11-05，key/图谱/去模型改造后）
- 单测：`tests/MoAI.TeamAgent.Tests`（TeamAgentJson 7 + 子标识唯一性 3 + 内置定义合法性 2）→ **12/12 全绿**（2026-11-05）
- E2E：`node local-dev/team-agent-e2e.mjs` → **52/52 PASS**（2026-11-05，后端 5210，key/name 双唯一 + 知识图谱依赖 + 内置 key=plan/build + 种子器领养旧名字行）
- 前端：vitest **770/770**（98 文件，含 `pages/teams/agents/__tests__/TeamAgents.test.tsx` 7 用例）+ tsc + eslint 0 error（2026-11-05；列表页为卡片列表——同团队提示词款）

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @AG-S1 | team-agent-e2e.mjs（AG-01、AG-02、AG-03a、AG-10c/d、AG-11、AG-12、AG-16、AG-18、AG-19） | PASS 52/52（2026-11-05） |
| @AG-S2 | team-agent-e2e.mjs（AG-04a~h：含 key 非法/空、子名空） | PASS 52/52（2026-11-05） |
| @AG-S3 | team-agent-e2e.mjs（AG-05a~e：插件/wiki/图谱 400 + 伪造头像 404） | PASS 52/52（2026-11-05） |
| @AG-S4 | team-agent-e2e.mjs（AG-06、AG-07a/b、AG-08/08b/08c：含 key 与子 key/name 回读） | PASS 52/52（2026-11-05） |
| @AG-S5 | team-agent-e2e.mjs（AG-09a name 409、AG-09b key 409、AG-13b） | PASS 52/52（2026-11-05） |
| @AG-S6 | team-agent-e2e.mjs（AG-10a/b） | PASS 52/52（2026-11-05） |
| @AG-S7 | team-agent-e2e.mjs（AG-13a、AG-14、AG-15） | PASS 52/52（2026-11-05） |
| @AG-S8 | team-agent-e2e.mjs（AG-16、AG-17） | PASS 52/52（2026-11-05） |
| @AG-S9 | team-agent-e2e.mjs（AG-18~23，AG-23 同名同 key 重建） | PASS 52/52（2026-11-05） |
| @AG-S10 | team-agent-e2e.mjs（AG-24~32：新团队种入 key=plan/build/子智能体/删除 400/编辑保留/Member 可见/补种门禁与幂等/第二团队种入） | PASS 52/52（2026-11-05） |
| JSON 列读写/子标识唯一/内置定义合法 | tests/MoAI.TeamAgent.Tests | PASS 12/12（2026-11-05） |
| 前端列表页（卡片列表，含内置标记与删除隐藏） | ui/src/pages/teams/agents/__tests__/TeamAgents.test.tsx | PASS 7/7（2026-11-05） |
