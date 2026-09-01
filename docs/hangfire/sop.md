# 定时任务领域（Hangfire，HF）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（redis-cli 证据） ｜ [SOP](./sop.md)

## 1. 如何加一个后台任务（开发视角）

### 方式 A：MediatR 化周期/一次性任务（[@HF-S10](./bdd.md#hf-s10)）

1. 业务项目引用 `MoAI.Hangfire.Shared`；定义参数与命令（Command 须可 `new()`）：
   `CleanTempFilesCommand : RecuringJobCommand<CleanTempFilesParams>`，写普通 `IRequestHandler<…, RecuringJobResponse>`（request 内 Key/CronExpression/Params/TaskId 可用；要退役返回 `IsCancel=true`，见 [@HF-S11](./bdd.md#hf-s11)）。
2. 注册：`await _recurringJobService.AddOrUpdateRecurringJobAsync<TCommand,TParams>("key", "0 3 * * *", params)`。
   - cron 一律按 **UTC**（北京时间任务减 8 小时：每天 11:00 → `0 3 * * *`）。
   - 一次性/延时：`(key, startTime, params)` 重载，只精确到分钟（[@HF-S15](./bdd.md#hf-s15)/[@HF-S16](./bdd.md#hf-s16)）。
   - 取消/查询：`RemoveRecurringJobAsync(key)` / `QueryJobAsync(key)`（[@HF-S14](./bdd.md#hf-s14)）。

### 方式 B：计数器（高频计数削峰，[@HF-S7](./bdd.md#hf-s7)/[@HF-S17](./bdd.md#hf-s17)）

1. 写入侧：发 `IncrementCounterActivatorCommand { Name, Counters }`（MediatR），`moai:counter:{Name}` 原子自增。
2. 落库侧：实现 `ICounterActivatorJob`（`GetNameAsync` 返回同一 Name；`ActivateAsync` **事务落库**，失败抛异常保留计数下轮重试）并注册 DI；内置 counter 任务自动冲刷（实测 ≈10s/轮），无需自己注册。

## 2. 排查「job 不跑」（按序检查）

| 步骤 | 命令/检查 | 判定 |
|---|---|---|
| 1 服务器活着吗 | `docker exec moai-redis redis-cli SMEMBERS moai:hangfireservers` | 空 = 后端没起或 server 未随进程启动（[@HF-S1](./bdd.md#hf-s1)） |
| 2 任务注册了吗 | `docker exec moai-redis redis-cli HGETALL moai:hangfirerecurring-job:{key}` | 无键 = 注册代码没执行（[@HF-S4](./bdd.md#hf-s4)） |
| 3 cron 对吗 | 上一步的 Cron/TimeZoneId | TimeZoneId 应为 UTC；分钟粒度注意时区换算（[@HF-S9](./bdd.md#hf-s9)） |
| 4 到底跑没跑 | `HGET …:{key} LastJobId` → `HGET moai:hangfirejob:{id} State`；`GET moai:hangfirestats:succeeded` 隔 ~15s 再看 | State=Succeeded 但业务没生效 = **大概率 Handler 抛异常被吞** |
| 5 看日志（关键） | 后端日志搜 `Task error,Key:{key}` 或 `Counter activation execution failed` | job 体异常不进 Failed，只能靠日志（[@HF-S12](./bdd.md#hf-s12)） |
| 6 轮询延迟 | `moai:hangfireserver:{server}:queues` | 轮询 10s + WorkerCount=CPU*2，峰值秒级延迟属正常 |

常见坑：cron 全按 UTC（本地时间直接写差 8 小时）；Hangfire 兼容 5 段与含秒 6 段两种表达式，内置 counter 的 `* * * * ? *` 是 6 段"每秒"语义（实测被 10s 轮询钳制约 10s 一跑，**源码注释"每分钟"是错的**），要每分钟写 `0 * * * *`；Handler 抛异常 = 永远 Succeeded（自动重试不可用，需 Handler 内自实现）；`moai:` 与 `moai:hangfire` 前缀并存，`--scan --pattern 'moai:hangfire*'` 批量删除前先确认（[@HF-S2](./bdd.md#hf-s2)）；一次性任务只精确到分钟；多实例部署每实例都起 server 抢同一队列（注册幂等，见 [@HF-S13](./bdd.md#hf-s13)）。

## 3. 运维命令速查

```bash
docker exec moai-redis redis-cli ZRANGE moai:hangfirerecurring-jobs 0 -1 WITHSCORES   # 列出全部 recurring job
docker exec moai-redis redis-cli HGETALL moai:hangfirerecurring-job:counter           # 任务详情（Cron/NextExecution/LastJobId）
docker exec moai-redis redis-cli MGET moai:hangfirestats:succeeded moai:hangfirestats:failed
docker exec moai-redis redis-cli LRANGE moai:hangfiresucceeded 0 9                    # 最近成功 job id
docker exec moai-redis redis-cli ZREM moai:hangfirerecurring-jobs {key}               # 手动摘除任务（等价 RemoveRecurringJobAsync）
```

注意：业务异常也计入 succeeded（[@HF-S12](./bdd.md#hf-s12)）。

## 4. 验收流程

1. 基础设施/counter：跑 [TDD 回归命令](./tdd.md)（server 存活、counter 定义与执行链、succeeded 递增）。
2. 新接入任务（[@HF-S10](./bdd.md#hf-s10)~[@HF-S16](./bdd.md#hf-s16)）：注册 → 等 NextExecution 到点 → `LastJobId` 有值且 State=Succeeded → 业务副作用可见 → `RemoveRecurringJobAsync` 后 `IsExist=false`。
3. `dotnet build src/MoAI/MoAI.csproj` 0 错误。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01（轮 8，as-built）T1~T7 全过**：T1 build 0 错误（58 个存量 NU1507 警告）+ `MainModule.cs:28` 确认 `[InjectModule<HangfireCoreModule>]`；T2 `SMEMBERS` 1 个 server（`192:47661:dc42f2ff-8a1a-4a8b-b7ad-3c0f1af28485`）；T3 counter job 定义四字段与源码一致（CreatedAt=2026-09-01T05:59:43Z）；T4 succeeded=2395、队首 job id == LastJobId、NextExecution 与查询时刻吻合；**T4b 执行节奏实测：7 个 job 的 CreatedAt 相邻间隔 10102/10080/10098/9188/374/10101 ms（≈10s+偶发重复）、65 秒计数 +9、NextExecution−LastExecution=1000ms——证明 cron 按含秒 6 段式解析（每秒语义、被 10s 轮询钳制），源码注释"每分钟"错误**；T5 队首 job 类型即 CounterActivatorJobHandler（仅可经 DI 构造注入，Activator 桥接生效）；T6 键前缀清单核对；T7 全仓库 grep 零业务实现。
- 备注：查询时 `ZCARD moai:hangfireschedule=0` 属正常（轮询 10s，recurring 触发即被取走）；`moai:counter:*` 无键（无激活器，counter 空转成功）。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 8，as-built）：机制梳理 + Redis 实测自检 T1~T7 |
| 2026-09-01 | 按 [DOC-STANDARD](../DOC-STANDARD.md) 重构四件套（场景编号化、四件互链） |
