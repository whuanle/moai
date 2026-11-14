# 团队定时器 TDD

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据目录：local-dev/team-timer-smoke.mjs、ui/src/pages/teams/timers/\_\_tests\_\_

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @TIMER-S1 | local-dev/team-timer-smoke.mjs（TIMER-05/06/07/08/09） | PASS 22/22（2026-10-12） |
| @TIMER-S2 | local-dev/team-timer-smoke.mjs（TIMER-06） | PASS（2026-10-12） |
| @TIMER-S3 | local-dev/team-timer-smoke.mjs（TIMER-10/11） | PASS（2026-10-12） |
| @TIMER-S4 | local-dev/team-timer-smoke.mjs（TIMER-12/13/14/16/17） | PASS（2026-10-12） |
| @TIMER-S5 | local-dev/team-timer-smoke.mjs（TIMER-15a/15） | PASS（2026-10-12） |
| @TIMER-S6 | local-dev/team-timer-smoke.mjs（TIMER-18/19/20/21） | PASS（2026-10-12） |
| @TIMER-S7 | @manual（浏览器走查，见 SOP §3）+ TeamTimers.test.tsx（超长 URL 列省略号用例） | PASS 8/8（2026-10-30） |
| @TIMER-S8 | @manual（Member 登录走查） | 待走查 |
| @TIMER-S9 | ui/src/pages/teams/timers/\_\_tests\_\_/TeamTimers.test.tsx | PASS 6/6（2026-10-30，含未发布应用下拉过滤/空态提示） |
| @TIMER-S10 | ui/src/design-system/components/CronExpressionInput/\_\_tests\_\_/（cron.test.ts 13 用例 + CronExpressionInput.test.tsx 6 用例） | PASS 19/19（2026-10-30） |
| @TIMER-S11 | ui/src/pages/teams/timers/\_\_tests\_\_/TeamTimers.test.tsx（不开启重试兜底用例） | PASS 7/7（2026-10-30） |
| @TIMER-S12 | local-dev/team-timer-smoke.mjs（TIMER-09b/13b/17b）+ TeamTimers.test.tsx（执行状态列用例；活跃 5s 轮询 @manual） | smoke 25/25、vitest 9/9（2026-10-30） |
| @TIMER-S13 | @manual + 实测恢复（10 分钟超时无法在冒烟等待；僵尸恢复实测：遗留 30+ 分钟 Running 记录被 sweeper 启动即清为 failed，见 rounds-log 258） | PASS（2026-10-30） |
| @TIMER-S14 | local-dev/team-timer-smoke.mjs（TIMER-13c/13d/13e：userType=timer 会话可见/消息详情含回答/本人列表排除） | PASS 28/28（2026-10-30） |

冒烟脚本自举：桩模型（本地 OpenAI 兼容 /v1/chat/completions，输入含 FAIL 标记返回 500）+ 独立 Redis db15 Hangfire，零真实模型依赖；前置 `asserts/team_timer.sql` 已对库执行。
