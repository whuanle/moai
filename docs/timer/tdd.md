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
| @TIMER-S7 | @manual（浏览器走查，见 SOP §3） | PASS（2026-10-12） |
| @TIMER-S8 | @manual（Member 登录走查） | 待走查 |
| @TIMER-S9 | ui/src/pages/teams/timers/\_\_tests\_\_/TeamTimers.test.tsx | PASS 4/4（2026-10-12） |

冒烟脚本自举：桩模型（本地 OpenAI 兼容 /v1/chat/completions，输入含 FAIL 标记返回 500）+ 独立 Redis db15 Hangfire，零真实模型依赖；前置 `asserts/team_timer.sql` 已对库执行。
