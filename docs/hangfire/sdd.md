# 定时任务领域（Hangfire，HF）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@HF-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（Redis 连接与模块装配基座） ｜ 证据：redis-cli 命令（见 [TDD](./tdd.md)）
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标与现状

提供后台调度：周期任务（RecurringJob）与延时/一次性任务，任务体走既有 CQRS 管线（MediatR + DI scope）。基于 Hangfire.AspNetCore 1.8.24 + Hangfire.Redis.StackExchange 1.12.0 构建：Redis 存储 + 进程内调度服务器（随 Web 主进程，无独立 worker）+ 自定义 Activator 桥接 DI + MediatR 化任务模型 + 计数器冲刷削峰。

**as-built 现状**：机制全部就绪并随主进程运行，但**尚无业务方接入**——`RecuringJobCommand` 子类与 `ICounterActivatorJob` 实现均为 0；唯一在跑的是框架内置 `counter` 任务。

## 组件

```
src/hangfire/
├── MoAI.Hangfire.Shared/   Models/：IRecuringJobCommand、RecuringJobCommand<TParams>（IRequest<RecuringJobResponse>）、
│                           RecuringJobResponse（IsCancel）、ICounterActivatorJob、IncrementCounterActivatorCommand
│                           Services/：IRecurringJobService（增/删/查）、JobInfo
└── MoAI.Hangfire.Core/     HangfireCoreModule（装配）、AutoRegisterHangfireBackgroundService（启动后注册内置 job）
                            HangfireActivator(+Scope)（DI 桥接）、RecurringJobHandler（通用执行器→MediatR）
                            RecurringJobService、CounterActivatorJobHandler（"counter" 任务体）、
                            IncrementCounterActivatorCommandHandler（Redis 批量自增）
```

挂载：`src/MoAI/MainModule.cs [InjectModule<HangfireCoreModule>]`；无 Api 层（不暴露 HTTP）。

## 装配设计（真实取值）

- 存储：`UseRedisStorage(SystemOptions.Redis)`，`Prefix="moai:hangfire"`；Hangfire.Redis 拼键**不补冒号**，实际键形如 `moai:hangfirejob:{id}`、`moai:hangfireservers`、`moai:hangfirerecurring-job:counter`（[@HF-S2](./bdd.md#hf-s2)）。
- 序列化：CompatibilityLevel.Version_180 + SimpleAssemblyNameTypeSerializer + RecommendedSerializerSettings。
- 服务器：`SchedulePollingInterval=10s`、`WorkerCount=ProcessorCount*2`（[@HF-S1](./bdd.md#hf-s1)）。
- 注册：`AddHostedService<AutoRegisterHangfireBackgroundService>`，等 `ApplicationStarted` 后再注册 job（避免阻塞 Web 启动）。

## 关键机制

1. **Activator 桥接 DI**：每次 job 执行开独立 DI scope（`IServiceScopeFactory.CreateScope` + `ActivatorUtilities`），scoped 服务（如 DatabaseContext）不跨 job 复用（[@HF-S10](./bdd.md#hf-s10)）。
2. **MediatR 化任务模型**：业务方实现 `XxxCommand : RecuringJobCommand<TParams>` + 普通 IRequestHandler；`IRecurringJobService.AddOrUpdateRecurringJobAsync<TCommand,TParams>` 内部以 `RecurringJobHandler<TCommand,TParams>.HandlerAsync(key, cron, params)` 注册，执行时组装 Command（TaskId=新 Guid/Key/Cron/Params）后 MediatR.Send。
3. **自取消**：Handler 返回 `IsCancel=true` → `RemoveIfExists(key)`（[@HF-S11](./bdd.md#hf-s11)）。
4. **异常吞噬**：`RecurringJobHandler` 捕获全部异常仅 LogWarning，job 永远 Succeeded——Hangfire 自动重试实际不可用（[@HF-S12](./bdd.md#hf-s12)，已知问题）。
5. **注册形态**：`(key, cron, params)` 周期（TimeZone=**UTC**）；`(key, startTime, cron, params)` 首次到点后重注册为周期（分钟粒度年份 cron）；`(key, startTime, params)` 一次性（执行后自删）（[@HF-S15](./bdd.md#hf-s15)/[@HF-S16](./bdd.md#hf-s16)）。
6. **查询/取消**：`QueryJobAsync(key)`→JobInfo（IsExist/Cron/NextExecution/LastExecution/LastJobState/Error）；`RemoveRecurringJobAsync(key)`（[@HF-S14](./bdd.md#hf-s14)）。

## 计数器冲刷（当前唯一在跑的 job）

- **写入侧**：`IncrementCounterActivatorCommand { Name, Counters }` → FluentValidation（Name NotEmpty）→ Redis batch 对 `counter:{Name}` 各 field 原子自增（实际键 `moai:counter:{Name}`，带库默认前缀）（[@HF-S17](./bdd.md#hf-s17)）。
- **冲刷侧**：启动后自动注册 recurring job `"counter"`（cron `* * * * ? *`，UTC，default 队列）。`InvokeAsync` 取**所有** `ICounterActivatorJob` 实现，逐个：读 hash 全量正值 → `ActivateAsync`（实现者事务落库）→ 重读 → 逐 field 减旧值（下限 0）→ 回写。单个激活器异常仅记 Error，不影响他人（[@HF-S7](./bdd.md#hf-s7)/[@HF-S8](./bdd.md#hf-s8)）。
- **执行节奏（实测）**：cron `* * * * ? *` 为含秒 6 段式，语义"**每秒**"而非源码注释声称的"每分钟"；受 10s 轮询钳制实测 ≈10.1s/轮（偶发 <1s 重复），`NextExecution-LastExecution=1000ms`。要真正每分钟应写 `0 * * * *`（[@HF-S6](./bdd.md#hf-s6)）。
- **差量语义**：激活器拿到自上次冲刷以来的增量，冲刷后清零；读取与回写之间的并发写入被保留。

## 已知问题

- **无 Dashboard**：未配置 `UseHangfireDashboard`，观测只能靠 Redis 键与日志（[@HF-S3](./bdd.md#hf-s3)）。
- Handler 吞异常 → Hangfire 视角永远 Succeeded，Failed/重试不可用，排查看日志（[@HF-S12](./bdd.md#hf-s12)）。
- `IncrementCounterActivatorCommand` / `IRecurringJobService` 全仓零调用、`ICounterActivatorJob` 零实现——counter 空转成功（`moai:counter:*` 无键）。
- **cron 注释与实际不符**：counter 注册处注释"每分钟"是错的（实际每秒语义，见上）；业务依赖每分钟须改表达式。
- startTime 重载用五段年份 cron 表达一次性触发，只精确到分钟；跨年/已过时刻行为未处理。
- 拼写沿袭源码：`IRecuringJobCommand`/`RecuringJobCommand`（少 r），文档与代码保持一致。
