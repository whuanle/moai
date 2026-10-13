# 团队定时器 SDD

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md) ｜ 调度底座：[../hangfire](../hangfire)（无四件套，见 src/hangfire） ｜ 证据：[local-dev/team-timer-smoke.mjs](../../local-dev/team-timer-smoke.mjs)

## 1. 目标

团队维度定时执行一个绑定的 Agent/流程应用：管理员把定时器绑定到本团队**已发布**的应用，配置固定提问内容与 cron（UTC），由 Hangfire 周期任务无头执行并把回答/错误落入执行历史。典型场景：定时收集数据生成分析报告推送到飞书（由应用自身工具完成推送）。支持立即执行（调试）、失败重试策略（次数 + 间隔）、执行历史查看（默认加载最近 10 条）。

## 2. 组件

```
src/timer/
  MoAI.Timer.Shared   Commands/Queries + IModelValidator（引用 Database.Shared/Infra.Shared/Team.Shared）
  MoAI.Timer.Api      TeamTimerController（/api/timer*）
  MoAI.Timer.Core     Handlers + Jobs + Services（引用 Timer.Api/Database.Shared/Team.Shared/Account.Shared/Hangfire.Shared/AI.Core + Hangfire.Core）
```

- `TeamTimerScheduler`：每定时器一个 Hangfire 周期任务（key `team-timer:{timerId:N}`），走 `IRecurringJobService`（UTC cron）；`ValidateCron` 做落库前校验（5 段 + 字段取值范围）。
- `TeamTimerRunJobCommand(+Handler)`：调度触发入口；定时器已删/停用返回 `IsCancel=true` 移除任务；触发即建执行记录并刷新 `last_run_time`。
- `TeamTimerAppInvoker`：无头执行分发——Agent 应用走发布快照 `AppAgentConfigSnapshot.ResolveEffectiveConfig(preferPublished:true)` → `IWorkflowNodeAiInvoker.AgentAppAsync`（与流程 agentApp 节点/正式对话同口径）；流程应用走 `IWorkflowAppChatInvoker`（UseDraft=false，每次新生成 SessionId，不产生用户可见会话）。
- `TeamTimerExecutor`：执行状态机 Pending→Running→(Success | Retrying→…)→Success/Failed；失败按 `maxRetryCount`/`retryIntervalSeconds` 用 `IBackgroundJobClient.Schedule` 延迟重试（重试同行累计 `attempt_count`）；应用删除/禁用属非瞬时错误直接终态不重试。
- 身份：Handler 一律用命令携带的 `ContextUserId`（不注入 IUserContextProvider）；Hangfire 线程无用户上下文，身份取 job params/实体冗余字段（审计 CreateUserId 记 0 是平台既有行为，执行记录以 `trigger_user_id` 为准）。Controller 对路由参数构造的命令（update/delete/run）注入 `IUserContextProvider` 显式回填 `ContextUserId`——AutoAssignUserIdFilter 只对绑定的 action 参数赋值，控制器内 `new` 的命令必须手工携带（variable 更新接口同做法）。

## 3. 数据

- `team_timer`（TeamTimerEntity，IFullAudited，uuid 主键）：team_id、name（团队内唯一，partial unique is_deleted=0）、app_id、app_type（绑定时快照）、cron_expression、input_content(text)、is_enabled、max_retry_count(0-5)、retry_interval_seconds(10-3600)、last_run_time。
- `team_timer_execution`（TeamTimerExecutionEntity）：timer_id、team_id（冗余）、trigger_type(0=调度 1=手动)、trigger_user_id、status(0=等待 1=执行中 2=成功 3=失败 4=待重试)、attempt_count、start/end_time、duration_ms、next_retry_time、app_instance_id（流程实例）、result/error_message(text)；索引 (timer_id, create_time)。
- 增量 DDL：`asserts/team_timer.sql`（存量库手动 psql 执行，无 EF Migration）。

## 4. 关键决策

- **cron 统一 UTC**（复用 `IRecurringJobService` 既有口径，与知识库外部源一致）；前端快选预设按北京时间换算并标注。
- **绑定即校验已发布**：创建/更新要求应用存在、本团队、未禁用、Agent/流程类型且 PublishStatus=1；执行期再校验一次，避免历史里全是"未发布"失败。
- **手动执行不改调度**：`POST {id}/run` 入队一次执行并计入历史；停用的定时器也允许立即执行（调试用）。
- **删除保留历史**：定时器软删；执行历史查询对 timer 用 `IgnoreQueryFilters` 取归属以完成权限校验，删除后历史仍可查看。
- **重试非瞬时错误跳过**：应用被删/禁用直接终态，避免空转重试预算。
- 并发：Hangfire 同名周期任务自身不并发触发；同一定时器可同时存在手动执行与调度执行（互不影响）。

## 5. 已知问题 / 边界

- cron 语义校验为字段级（取值范围 + 结构），不校验"2 月 30 日"类组合；此类表达式由 Hangfire 在触发时报错（JobInfo.Error），下次调度时间将为空。
- Agent 应用无头执行不落会话消息（与 agentApp 节点一致），历史里只有最终回答文本；流程应用记录实例 id 可跳转运行历史。
- 定时器执行消耗真实模型 token，无独立配额限制。
