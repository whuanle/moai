> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

# 记忆模块验证映射

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @MEM-S1 | local-dev/agent-memory-e2e.mjs#AM-01/01b~01d ｜ tests/MoAI.App.Tests/SaveAppAgentConfigMemoryLockTests#PlatformOff/PlatformOnWithoutModel | PASS 44/44 + 9/9（2026-11-04 重跑） |
| @MEM-S2 | local-dev/agent-memory-e2e.mjs#AM-02~03 ｜ local-dev/memory-management-e2e.mjs#MM-18b（携带模型字段被剥离回显） | PASS 44/44 + 54/54（2026-11-04 重跑） |
| @MEM-S3 | local-dev/agent-memory-e2e.mjs#AM-05 | PASS 44/44（2026-11-04） |
| @MEM-S4 | local-dev/agent-memory-e2e.mjs#AM-06 | PASS 44/44（2026-11-04） |
| @MEM-S5 | local-dev/agent-memory-e2e.mjs#AM-07~08 | PASS 44/44（2026-11-04） |
| @MEM-S6 | local-dev/agent-memory-e2e.mjs#AM-09 | PASS 44/44（2026-11-04） |
| @MEM-S7 | local-dev/agent-memory-e2e.mjs#AM-11 | PASS 44/44（2026-11-04） |
| @MEM-S8 | local-dev/agent-memory-e2e.mjs#AM-10 | PASS 44/44（2026-11-04） |
| @MEM-S9 | local-dev/agent-memory-e2e.mjs#AM-14~15 | PASS 44/44（2026-11-04） |
| @MEM-S10 | local-dev/agent-memory-e2e.mjs#AM-12/14/16 | PASS 44/44（2026-11-04） |
| @MEM-S11 | local-dev/agent-memory-e2e.mjs#AM-16 | PASS 44/44（2026-11-04） |
| @MEM-S11 | ui/src/pages/teams/apps/MyMemoryPage.tsx（走查，接口 @MEM-S13/S14 自动化） | PASS（2026-10-12） |
| @MEM-S12 | @manual（面板结构走查） | PASS（2026-10-12）；2026-11-05 面板重做后走查：记忆区两态分段选择、无「管理记忆」入口 |
| @MEM-S13 | local-dev/agent-memory-e2e.mjs#AM-19~20 | PASS 44/44（2026-11-04） |
| @MEM-S14 | local-dev/agent-memory-e2e.mjs#AM-21~22 | PASS 44/44（2026-11-04） |
| @MEM-S15 | local-dev/agent-memory-e2e.mjs#AM-23~24 | PASS 44/44（2026-11-04） |
| @MEM-S16 | local-dev/agent-memory-e2e.mjs#AM-25 | PASS 44/44（2026-11-04） |
| @MEM-S17 | ui/src/pages/teams/apps/__tests__/AppMemoryPanel.test.tsx（统计展示与清空归零；面板已随 @AP-S104 并入应用「配置」分区收缩卡片） ｜ ui/src/i18n/__tests__/i18n.interpolation.test.ts（真实 i18next 实例插值回归：platformDimensions/statsHint/myMemoriesHint 单花括号占位符缺陷修复守卫） | PASS 9/9 + 3/3（2026-11-04 重跑） |
| @MEM-S22 @MEM-S23 | ui/src/pages/teams/apps/__tests__/AppMemoryPanel.test.tsx（默认提示词占位展示且空值不下发/自定义回显并随回调产出；后端超长 400 由 `MemorySettingsValidator` 校验） | PASS 9/9（2026-11-04） |
| @MEM-S18 | local-dev/agent-memory-e2e.mjs#AM-10b（平台锁定） ｜ local-dev/memory-management-e2e.mjs#MM-35 | PASS 44/44 + 54/54（2026-11-04 重跑） |
| @MEM-S19 | local-dev/agent-memory-e2e.mjs#AM-10c | PASS 44/44（2026-11-04） |
| @MEM-S20 | local-dev/agent-memory-e2e.mjs#AM-10d~f（关闭/重开不携带模型字段不受锁定影响） | PASS 44/44（2026-11-04） |
| @MEM-S21 | local-dev/agent-memory-e2e.mjs#AM-24b~d（清空全部后平台换模型/维度+恢复写入） | PASS 44/44（2026-11-04） |
| @MEM-S24 | local-dev/memory-management-e2e.mjs#MM-27~28d（总览/全局计数一致/keyword 过滤命中与空回显） ｜ ui/src/pages/memory/__tests__/MemoryManagement.test.tsx（工具栏前端过滤+无新建入口断言） | PASS 54/54 + 6/6（2026-11-04） |
| # @MEM-S25/S26 | 已随 2026-11-03 记忆管理「仅删除化」退役（人工编辑/存为全局下线），编号不复用，现行行为见 @MEM-S45 | - |
| @MEM-S45 | local-dev/memory-management-e2e.mjs#MM-01a~d（四个人工写入端点下线 404/405）+ MM-12b（全局记忆仅 AI 工具写入落库） ｜ ui/src/pages/memory/__tests__/MemoryManagement.test.tsx（页面无新建/编辑/存为全局入口） | PASS 54/54 + 5/5（2026-11-04） |
| @MEM-S27 | local-dev/memory-management-e2e.mjs#MM-02~09b（MEMORY_* 新键门禁，含跨应用开关校验 06b/06c/09b） | PASS 54/54（2026-11-04） |
| # @MEM-S28/S29 | 已随 2026-11-03 记忆管理「仅删除化」退役（全局记忆人工新建/编辑与存为全局下线），编号不复用，现行行为见 @MEM-S46 | - |
| @MEM-S46 | local-dev/memory-management-e2e.mjs#MM-31~34/45b（仅删除/清空：删单条 200、重复删 404、清空后为空；写入仅 AI 工具轮） ｜ ui/src/pages/memory/__tests__/MemoryManagement.test.tsx（全局页签仅清空+删除） | PASS 54/54 + 5/5（2026-11-04） |
| @MEM-S30 | local-dev/memory-management-e2e.mjs#MM-29~30（注入后模型仍可见用户消息与工具） | PASS 54/54（2026-11-04） |
| @MEM-S31 | local-dev/memory-management-e2e.mjs#MM-35 | PASS 54/54（2026-11-04） |
| @MEM-S32 | local-dev/memory-management-e2e.mjs#MM-36~38b/39（root 清空全部：应用+全局一并清空解锁） | PASS 54/54（2026-11-04） |
| @MEM-S33 | local-dev/memory-management-e2e.mjs#MM-29c/d/i/j ｜ ui/src/pages/teams/apps/__tests__/AppMemoryPanel.test.tsx（allowGlobalMemory 开关产出 memory 节；平台跨应用开关关闭时禁用并收敛） | PASS 54/54 + 9/9（2026-11-04） |
| @MEM-S34 | local-dev/memory-management-e2e.mjs#MM-29/29b（未允许不注入且 userconfig 回显 memoryGlobalAllowed=false） | PASS 54/54（2026-11-04） |
| @MEM-S35 | local-dev/memory-management-e2e.mjs#MM-29e/f/g/h ｜ ui/src/pages/teams/apps/__tests__/AppUserSettings.test.tsx（接入开关展示/提交/不提交） | PASS 54/54 + 8/8（2026-11-04） |
| @MEM-S36 | local-dev/memory-management-e2e.mjs#MM-44/44b/45（scope=global 保存不落应用、forget 兜底删全局） | PASS 54/54（2026-11-04） |
| @MEM-S37 | local-dev/memory-management-e2e.mjs#MM-09（存量分片库实跑首次配置不受锁定；配套 `SettingsService.ValidateMemorySettingAsync` 空模型直通分支） | PASS 54/54（2026-11-04） |
| @MEM-S38 | local-dev/memory-migration-e2e.mjs#MG-02 | PASS 44/44（2026-11-04） |
| @MEM-S39 | local-dev/memory-migration-e2e.mjs#MG-07a~e | PASS 44/44（2026-11-04） |
| @MEM-S40 | local-dev/memory-migration-e2e.mjs#MG-08a/08d/09a~d（MQ 异步重嵌+换表+设置切换+进度闭环；造数经 memory_save 工具轮） ｜ ui/src/pages/settings/__tests__/Settings.test.tsx（锁定态迁移入口与确认调用） | PASS 44/44 + 25/25（2026-11-04） |
| @MEM-S41 | local-dev/memory-migration-e2e.mjs#MG-08b/08b2/08c（工具轮写入被拒不落库/删除 409/并发发起 409） ｜ ui/src/pages/settings/__tests__/Settings.test.tsx（锁定态保存仅提交开关） | PASS 44/44 + 25/25（2026-11-04） |
| @MEM-S42 | local-dev/memory-migration-e2e.mjs#MG-09e~g/10/10b（数据保留/被阻断写入未落库/新维度检索命中/新写入） | PASS 44/44（2026-11-04） |
| @MEM-S43 | local-dev/memory-migration-e2e.mjs#MG-11a~e（死渠道失败回滚：任务失败/设置不变/数据不变） | PASS 44/44（2026-11-04） |
| @MEM-S44 | local-dev/memory-migration-e2e.mjs#MG-12a~d（失败后重发成功+回迁检索命中） | PASS 44/44（2026-11-04） |
| @MEM-S47 | local-dev/agent-memory-e2e.mjs#AM-01e~h ｜ local-dev/memory-management-e2e.mjs#MM-06b/06c/09b/09c/18a ｜ tests/MoAI.App.Tests/SaveAppAgentConfigMemoryLockTests#AllowGlobal 两态 | PASS 44/44 + 54/54 + 9/9（2026-11-04） |
| @MEM-S48 | local-dev/agent-memory-e2e.mjs#AM-03c（携带 retrievalTopK 保存剥离） ｜ ui/src/pages/teams/apps/__tests__/AppMemoryPanel.test.tsx（检索条数退役：回显不读取、产出不携带） | PASS 44/44 + 9/9（2026-11-04） |
| @MEM-S49 | local-dev/agent-memory-e2e.mjs#AM-03d/03e（上限 600 拒/500 过） ｜ tests/MoAI.App.Tests/SaveAppAgentConfigMemoryLockTests#MaxPerUser 两态 | PASS 44/44 + 9/9（2026-11-04） |
| @MEM-S50 | local-dev/memory-management-e2e.mjs#MM-30b~e（平台关闭跨应用记忆后存量允许应用不注入/回显 false/恢复开启） | PASS 54/54（2026-11-04） |

运行方式：`node local-dev/agent-memory-e2e.mjs [baseUrl]`（默认 5210，后端运行中；内置本地 OpenAI 兼容桩 embedding+chat，零 SKIP；脚本会临时开启平台 `MEMORY_*` 设置并在收尾还原）；`node local-dev/memory-management-e2e.mjs [baseUrl]`（默认 5000，记忆管理/全局记忆/统一设置门禁 54 场景：人工写入端点下线断言 + 记忆仅 AI 工具写入 + 仅删除/清空 + 注入门禁 + 列表关键字检索 + 模型锁定 + 跨应用记忆开关门禁与运行时收口，零 SKIP，收尾自清理可重复跑；中断残留时先执行 `node local-dev/memory-global-reset.mjs [baseUrl]` 复位平台记忆设置）；`node local-dev/memory-migration-e2e.mjs [baseUrl]`（默认 5000，向量化模型迁移 44 场景：成功迁移/进行中写入阻断（工具轮被拒+删除 409）/失败回滚/重试链路，桩 embedding 按请求 dimensions 出向量且目标模型故意放慢撑开进行中窗口，造数经 memory_save 工具轮，零 SKIP，收尾自清理）。

子代理协作与压缩的应用级验证见 [../app/bdd.md](../app/bdd.md) @AP-Sxx 与 [local-dev/subagent-e2e.mjs](../../local-dev/subagent-e2e.mjs)。
