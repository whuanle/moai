# 团队定时器 SOP

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## 1. 前置

- 存量数据库执行 `psql ... -f asserts/team_timer.sql`（新库 EnsureCreated 自带）。
- Redis 可用（Hangfire 周期任务存储）；定时器不依赖 RabbitMQ。

## 2. 日常操作

- 入口：团队页侧边菜单「定时器」（仅 Owner/Admin 可见），`/team/{teamId}/timers`。
- 新建：绑定本团队**已发布**的 Agent/流程应用 → 填 cron（UTC，可用快选：每小时/每天 9 点/每天 18 点/每周一 9 点，按北京时间换算）→ 执行内容（作为提问发给应用）→ 启用调度 + 失败重试（0-5 次、间隔 10-3600 秒）。
- 调试：行内「立即执行」异步执行一次并自动打开执行历史抽屉（默认最近 10 条，进行中每 3 秒轮询），行展开查看回答/失败原因。
- 启停：行内开关；停用即移除 Hangfire 周期任务（key `team-timer:{timerId:N}`），启用即恢复。

## 3. 验收流程

1. 后端运行后执行 `node local-dev/team-timer-smoke.mjs [base]`，期望 PASS 22/0 FAIL。
2. 前端 `npx vitest run src/pages/teams/timers`，期望 4/4。
3. 浏览器走查 @TIMER-S7：列表 → 新建（选应用/快选 cron）→ 立即执行 → 抽屉看历史 → 停用/启用 → 删除。

## 4. 排障表

| 现象 | 定位 |
|---|---|
| 列表"下次调度"为空（已启用） | cron 非法被 Hangfire 拒绝：查 Redis `moai:hangfire:team-timer:{id}` 任务的 Error 字段；或后端日志 `Task error,Key:team-timer:*` |
| 执行历史一直"等待中" | Hangfire 服务未启动/Redis 断连；看 `AutoRegisterHangfireBackgroundService` 日志 |
| 历史失败"Agent 应用尚未发布/未配置对话模型" | 应用配置在绑定后被改动，按提示重新发布应用 |
| 历史失败"绑定的应用已删除或被禁用" | 非瞬时错误不重试，重新绑定后编辑定时器保存 |
| 立即执行 404"团队不存在" | token 用户已不在该团队 |
| 删除后历史仍能查看 | 预期行为（@TIMER-S6），历史随软删定时器保留 |
