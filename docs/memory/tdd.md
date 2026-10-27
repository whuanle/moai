> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

# 记忆模块验证映射

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @MEM-S1 | local-dev/agent-memory-e2e.mjs#AM-01 | PASS 25/25（2026-10-12） |
| @MEM-S2 | local-dev/agent-memory-e2e.mjs#AM-02~03 | PASS（2026-10-12） |
| @MEM-S3 | local-dev/agent-memory-e2e.mjs#AM-05 | PASS（2026-10-12） |
| @MEM-S4 | local-dev/agent-memory-e2e.mjs#AM-06 | PASS（2026-10-12） |
| @MEM-S5 | local-dev/agent-memory-e2e.mjs#AM-07~08 | PASS（2026-10-12） |
| @MEM-S6 | local-dev/agent-memory-e2e.mjs#AM-09 | PASS（2026-10-12） |
| @MEM-S7 | local-dev/agent-memory-e2e.mjs#AM-11 | PASS（2026-10-12） |
| @MEM-S8 | local-dev/agent-memory-e2e.mjs#AM-10 | PASS（2026-10-12） |
| @MEM-S9 | local-dev/agent-memory-e2e.mjs#AM-13~15 | PASS（2026-10-12） |
| @MEM-S10 | local-dev/agent-memory-e2e.mjs#AM-12/14/16 | PASS 24/24（2026-10-12 复验） |
| @MEM-S11 | local-dev/agent-memory-e2e.mjs#AM-16 | PASS（2026-10-12） |
| @MEM-S11 | ui/src/pages/teams/apps/MyMemoryPage.tsx（走查，接口 @MEM-S13/S14 自动化） | PASS（2026-10-12） |
| @MEM-S12 | @manual（面板结构走查） | PASS（2026-10-12） |
| @MEM-S13 | local-dev/agent-memory-e2e.mjs#AM-19~20 | PASS（2026-10-12） |
| @MEM-S14 | local-dev/agent-memory-e2e.mjs#AM-21~22 | PASS（2026-10-12） |
| @MEM-S15 | local-dev/agent-memory-e2e.mjs#AM-23~24 | PASS（2026-10-12） |
| @MEM-S16 | local-dev/agent-memory-e2e.mjs#AM-25 | PASS（2026-10-12） |
| @MEM-S17 | ui/src/pages/teams/apps/__tests__/AppMemoryPanel.test.tsx（统计展示与清空归零；面板已随 @AP-S104 并入应用「配置」分区收缩卡片） | PASS 7/7（2026-10-27 复跑） |
| @MEM-S22 @MEM-S23 | ui/src/pages/teams/apps/__tests__/AppMemoryPanel.test.tsx（默认提示词占位展示且空值不下发/自定义回显并随回调产出；后端超长 400 由 `MemorySettingsValidator` 校验） | PASS 9/9（2026-10-27） |
| @MEM-S18 | local-dev/agent-memory-e2e.mjs#AM-10b ｜ tests/MoAI.App.Tests/SaveAppAgentConfigMemoryLockTests#ModelChange/DisabledDraftSwitch | PASS 32/32（2026-10-13） |
| @MEM-S19 | local-dev/agent-memory-e2e.mjs#AM-10c ｜ tests/MoAI.App.Tests/SaveAppAgentConfigMemoryLockTests#DimensionChange | PASS 32/32（2026-10-13） |
| @MEM-S20 | local-dev/agent-memory-e2e.mjs#AM-10d~f ｜ tests/MoAI.App.Tests/SaveAppAgentConfigMemoryLockTests#ReEnable/RequestWithoutDimensions | PASS 32/32（2026-10-13） |
| @MEM-S21 | local-dev/agent-memory-e2e.mjs#AM-24b~d | PASS 32/32（2026-10-13） |

运行方式：`node local-dev/agent-memory-e2e.mjs [baseUrl]`（默认 5210，后端运行中；内置本地 OpenAI 兼容桩 embedding+chat，零 SKIP）。

子代理协作与压缩的应用级验证见 [../app/bdd.md](../app/bdd.md) @AP-Sxx 与 [local-dev/subagent-e2e.mjs](../../local-dev/subagent-e2e.mjs)。
