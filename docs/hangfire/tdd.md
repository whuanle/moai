# 定时任务领域（Hangfire，HF）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。验证物为 redis-cli 命令（容器 moai-redis）与代码走查；后端运行于 :5210。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @HF-S1 | `redis-cli SMEMBERS moai:hangfireservers` | PASS（2026-09-01，1 个 server：`192:47661:dc42f2ff-…`） |
| @HF-S2 | `redis-cli --scan --pattern 'moai:hangfire*'` | PASS（2026-09-01，键形 job:/servers/recurring-job 均无额外冒号） |
| @HF-S3 | 代码走查 `Program.cs` 无 UseHangfireDashboard | PASS（2026-09-01） |
| @HF-S4 | `redis-cli HGETALL moai:hangfirerecurring-job:counter` | PASS（2026-09-01，Job=CounterActivatorJobHandler.InvokeAsync、Cron=`* * * * ? *`、TimeZoneId=UTC、Queue=default） |
| @HF-S5 | `GET moai:hangfirestats:succeeded`（65s 内 2453→2462）+ `grep -rn 'ICounterActivatorJob\|RecuringJobCommand' src`（排除 src/hangfire） | PASS（2026-09-01，succeeded 持续递增；业务实现零命中） |
| @HF-S6 | `LRANGE moai:hangfiresucceeded 0 6` + `HMGET moai:hangfirejob:{id} CreatedAt RecurringJobId Type` | PASS（2026-09-01，相邻间隔 10102/10080/10098/9188/374/10101ms≈10s+偶发重复；NextExecution−LastExecution=1000ms——**cron 为含秒 6 段式每秒语义，被 10s 轮询钳制；源码注释"每分钟"与实际不符**） |
| @HF-S7 ~ @HF-S9 | @manual 代码走查（CounterActivatorJobHandler 差量算法与异常隔离；Cronos 段数语义） | PASS（2026-09-01） |
| @HF-S10 ~ @HF-S16 | @manual 接入契约（无业务 job，首个接入时按 [SOP 第 4 节](./sop.md) 验收；机制走查与 SDD 一致） | 走查 PASS（2026-09-01），待接入实测 |
| @HF-S17、@HF-S18 | @manual 代码走查（IncrementCounterActivatorCommandHandler：FluentValidation + Redis batch） | PASS（2026-09-01） |

## 回归命令

```bash
docker exec moai-redis redis-cli SMEMBERS moai:hangfireservers          # 服务器存活
docker exec moai-redis redis-cli HGETALL moai:hangfirerecurring-job:counter
docker exec moai-redis redis-cli GET moai:hangfirestats:succeeded      # 隔 ~15s 再取，应增长
docker exec moai-redis redis-cli LRANGE moai:hangfiresucceeded 0 0     # 队首 job id
dotnet build src/MoAI/MoAI.csproj                                      # 期望 0 错误
```

## 覆盖率说明

- 基础设施/counter 均有 Redis 实测证据；Activator 桥接由队首 job 的 Invocation 类型（仅可经 DI 构造注入）间接证明。
- 接入契约场景（@HF-S10 ~ @HF-S16）无业务消费方，暂以代码走查为准——接入第一个业务 job 后必须回补实测。
