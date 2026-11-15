# 定时任务领域（Hangfire，HF）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@HF-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（Redis 连接与模块装配基座） ｜ 证据：redis-cli 命令（见 [TDD](./tdd.md)）
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标与现状

提供后台调度：周期任务（RecurringJob）与延时/一次性任务，任务体走既有 CQRS 管线（MediatR + DI scope）。基于 Hangfire.AspNetCore 1.8.24 + Hangfire.Redis.StackExchange 1.12.0 构建：Redis 存储 + 进程内调度服务器（随 Web 主进程，无独立 worker）+ 自定义 Activator 桥接 DI + MediatR 化任务模型。

**as-built 现状**：机制全部就绪并随主进程运行。业务已接入三类任务：Wiki 外部源同步（`WikiSourceSyncJobCommand`）、团队定时器（`TeamTimerRunJobCommand`）、资源使用统计批量落库（统计模块 `"resource-usage-flush"`）。早期框架内置的 `"counter"` 任务（`CounterActivatorJobHandler` + `ICounterActivatorJob`）已在统一统计模块改造中删除，遗留定义由启动期幂等清理（[@HF-S19](./bdd.md#hf-s19)）。

## 组件

```
src/hangfire/
├── MoAI.Hangfire.Shared/   Models/：IRecuringJobCommand、RecuringJobCommand<TParams>（IRequest<RecuringJobResponse>）、RecuringJobResponse（IsCancel）
│                           Services/：IRecurringJobService（增/删/查）、JobInfo
└── MoAI.Hangfire.Core/     HangfireCoreModule（装配）、HangfireActivator(+Scope)（DI 桥接）
                            RecurringJobHandler（通用执行器→MediatR）、RecurringJobService
                            ObsoleteRecurringJobCleaner（启动后自动清理任务体已不存在的遗留定义）
```

> 计数器冲刷（`CounterActivatorJobHandler` / `ICounterActivatorJob` / `IncrementCounterActivatorCommand`）已随统一统计模块改造删除，见 [../statistics/sdd.md](../statistics/sdd.md)。

挂载：`src/MoAI/MainModule.cs [InjectModule<HangfireCoreModule>]`；无 Api 层（不暴露 HTTP）。

## 装配设计（真实取值）

- 存储：`UseRedisStorage(SystemOptions.Redis)`，`Prefix="moai:hangfire"`；Hangfire.Redis 拼键**不补冒号**，实际键形如 `moai:hangfirejob:{id}`、`moai:hangfireservers`、`moai:hangfirerecurring-job:resource-usage-flush`（[@HF-S2](./bdd.md#hf-s2)）。
- 序列化：CompatibilityLevel.Version_180 + SimpleAssemblyNameTypeSerializer + RecommendedSerializerSettings。
- 服务器：`SchedulePollingInterval=10s`、`WorkerCount=ProcessorCount*2`（[@HF-S1](./bdd.md#hf-s1)）。
- 注册：各业务模块用 `ApplicationStarted` 后的 `BackgroundService` + `IRecurringJobManager` 自注册（如统计模块 `AutoRegisterStatisticsBackgroundService`，[@HF-S20](./bdd.md#hf-s20)）；基础设施侧另注册 `ObsoleteRecurringJobCleaner` 托管服务做遗留定义自愈（[@HF-S19](./bdd.md#hf-s19)）。

## 关键机制

1. **Activator 桥接 DI**：每次 job 执行开独立 DI scope（`IServiceScopeFactory.CreateScope` + `ActivatorUtilities`），scoped 服务（如 DatabaseContext）不跨 job 复用（[@HF-S10](./bdd.md#hf-s10)）。
2. **MediatR 化任务模型**：业务方实现 `XxxCommand : RecuringJobCommand<TParams>` + 普通 IRequestHandler；`IRecurringJobService.AddOrUpdateRecurringJobAsync<TCommand,TParams>` 内部以 `RecurringJobHandler<TCommand,TParams>.HandlerAsync(key, cron, params)` 注册，执行时组装 Command（TaskId=新 Guid/Key/Cron/Params）后 MediatR.Send。
3. **自取消**：Handler 返回 `IsCancel=true` → `RemoveIfExists(key)`（[@HF-S11](./bdd.md#hf-s11)）。
4. **异常吞噬**：`RecurringJobHandler` 捕获全部异常仅 LogWarning，job 永远 Succeeded——Hangfire 自动重试实际不可用（[@HF-S12](./bdd.md#hf-s12)，已知问题）。
5. **注册形态**：`(key, cron, params)` 周期（TimeZone=**UTC**）；`(key, startTime, cron, params)` 首次到点后重注册为周期（分钟粒度年份 cron）；`(key, startTime, params)` 一次性（执行后自删）（[@HF-S15](./bdd.md#hf-s15)/[@HF-S16](./bdd.md#hf-s16)）。
6. **查询/取消**：`QueryJobAsync(key)`→JobInfo（IsExist/Cron/NextExecution/LastExecution/LastJobState/Error）；`RemoveRecurringJobAsync(key)`（[@HF-S14](./bdd.md#hf-s14)）。

## 资源使用统计落库任务（统计模块）

- **定义**：统计模块启动后注册 recurring job `"resource-usage-flush"`（cron `0 * * * * ?`，UTC），任务体 `ResourceUsageFlushJobHandler`（[@HF-S20](./bdd.md#hf-s20)）。
- **语义**：`[DisableConcurrentExecution(300)]` 防重叠；逐个 `IResourceUsageWriter` 从 `moai:counter:{name}` Lua 原子取出（HGETALL + DEL）→ 写资源自身表 → 累加统一日/总量表；写库失败把增量原样回补 Redis 下轮重试。详见 [../statistics/sop.md](../statistics/sop.md)。

## 遗留定义自动清理

- **问题**：任务体类型/方法被删除后，Redis 里的 recurring 定义不会自动消失，Hangfire 调度器每 10s 入队一次即抛 `JobLoadException` 并无限重试。
- **机制**：`ObsoleteRecurringJobCleaner`（`AddHostedService`）在 `ApplicationStarted` 后扫一遍 `connection.GetRecurringJobs()`，对 `LoadException != null && !Removed` 的定义 `RemoveIfExists(id)` 幂等删除，之后每 **1 小时**兜底扫一次（覆盖回滚、长时间不重启）。
- **安全边界**：只删 `CreatedAt` 早于 **24 小时**宽限期的定义——滚动发布时旧实例能看懂新版本刚注册的任务属于正常现象，不能误删；`CreatedAt` 缺失按历史数据对待。只删定义，不动 `moai:counter:*` 计数与历史执行记录（[@HF-S19](./bdd.md#hf-s19)）。

## 已知问题

- **无 Dashboard**：未配置 `UseHangfireDashboard`，观测只能靠 Redis 键与日志（[@HF-S3](./bdd.md#hf-s3)）。
- Handler 吞异常 → Hangfire 视角永远 Succeeded，Failed/重试不可用，排查看日志（[@HF-S12](./bdd.md#hf-s12)）。
- **升级残留任务定义**：任务体类型被删除后，Redis 里的 recurring 定义仍会被调度，表现为 `JobLoadException: Could not resolve type ...` 每 10s 重试一次。已由 `ObsoleteRecurringJobCleaner` 自动兜底（24h 宽限期 + 每小时扫描），无需人工清 Redis（[@HF-S19](./bdd.md#hf-s19)）。
- startTime 重载用五段年份 cron 表达一次性触发，只精确到分钟；跨年/已过时刻行为未处理。
- 拼写沿袭源码：`IRecuringJobCommand`/`RecuringJobCommand`（少 r），文档与代码保持一致。
