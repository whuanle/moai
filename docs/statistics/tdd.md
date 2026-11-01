# 统计模块 TDD

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/statistics-usage-e2e.mjs](../../local-dev/statistics-usage-e2e.mjs)

## 场景映射

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @ST-S1 | local-dev/statistics-usage-e2e.mjs（ST-01a~d） | PASS 26/26（2026-11-01） |
| @ST-S2 | local-dev/statistics-usage-e2e.mjs（ST-02a/b） | PASS 26/26（2026-11-01） |
| @ST-S3 | local-dev/statistics-usage-e2e.mjs（ST-03a~c/04/05/08） | PASS 26/26（2026-11-01） |
| @ST-S4 | local-dev/statistics-usage-e2e.mjs（ST-05/06a~c） | PASS 26/26（2026-11-01） |
| @ST-S5 | local-dev/statistics-usage-e2e.mjs（ST-07，`[FromQuery]` 整模型 + SharpGrip） | PASS 26/26（2026-11-01） |
| @ST-S6 | @manual（设计保证：取出即删 + 回补逻辑走查，见 sdd 关键决策 3） | 代码走查（2026-11-01） |
| @ST-S7 | local-dev/statistics-usage-e2e.mjs（app/prompt 链路）＋ local-dev/app-usecount-e2e.mjs（AU 13，app 写入器）＋ local-dev/skill-usecount-e2e.mjs（SKC 8，skill 写入器） | ST 26/26、AU 13/13、SKC 8/8（2026-11-01） |
| @ST-S8 | local-dev/statistics-usage-e2e.mjs（ST-09a~c：创建专家→会话选用→日表 prompt-usage≥1） | PASS 26/26（2026-11-01） |
| @ST-S9 | @manual（端到端计数结果由 AU/SKC/ST 覆盖；批量推送次数为内部行为，代码走查 PluginAppToolProvider/SkillAppToolProvider/WikiSearchService → collector.Record + 作用域 Dispose flush） | 代码走查（2026-11-01） |
| @ST-S10 | local-dev/statistics-usage-e2e.mjs（ST-10a~f：清单回显 day/week 400/切 hour→小时桶/还原 day） | PASS 32/32（2026-11-01） |
| @ST-S11 | @manual（校验放宽 + 写入器守卫放宽，走查 sdd 组件节） | 代码走查（2026-11-01） |
| @ST-S12 | local-dev/statistics-usage-e2e.mjs（ST-11a~f：插件运行→plugin-usage 总表→trend 按日聚合含今日/参数校验 400/不带 name 全类型） | PASS 32/32（2026-11-01） |

## 回归口径

- AU（app 写入器资源表）、SKC（skill 写入器资源表 + options/userconfig 回显，轮询 ≤100s）。
- e2e 轮询完成条件统一为「目标计数达成」而非「行出现」（flush 周期可能把一批计数拆到两个批次）；ST-06 查询页大小 100 防历史数据分页截断。
- 后端构建：`dotnet build src/MoAI/MoAI.csproj` 0 error（2026-11-01）；前端 typecheck 0 error、lint 0 error（15 warning 均存量）、vitest 700/700（2026-11-01）。

## 自检记录

- `node local-dev/statistics-usage-e2e.mjs` → PASS=32 FAIL=0
- `node local-dev/app-usecount-e2e.mjs` → PASS=13 FAIL=0
- `node local-dev/skill-usecount-e2e.mjs http://127.0.0.1:5000` → PASS=8 FAIL=0
- 实测链路：管理员运行 static_echo 插件 → 总表 plugin-usage total=1 → trend 今日 count=1 → plugin.counter=1（2026-11-01）
