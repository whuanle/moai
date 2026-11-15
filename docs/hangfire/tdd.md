# 定时任务领域（Hangfire，HF）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。验证物为 redis-cli 命令（容器 moai-redis）与代码走查；后端运行于 :5210。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @HF-S1 | `redis-cli SMEMBERS moai:hangfireservers` | PASS（2026-09-01，1 个 server：`192:47661:dc42f2ff-…`） |
| @HF-S2 | `redis-cli --scan --pattern 'moai:hangfire*'` | PASS（2026-09-01，键形 job:/servers/recurring-job 均无额外冒号） |
| @HF-S3 | 代码走查 `Program.cs` 无 UseHangfireDashboard | PASS（2026-09-01） |
| @HF-S9 | @manual 代码走查（Cronos 段数语义） | PASS（2026-09-01） |
| @HF-S10 ~ @HF-S16 | @manual 接入契约（Wiki 源同步 / 团队定时器使用同一注册通道，见各自模块 TDD） | 走查 PASS（2026-09-01） |
| @HF-S19 | 代码走查 `ObsoleteRecurringJobCleaner`：LoadException 判定 + 24h 宽限期 + `RemoveIfExists` 幂等；升级后日志 `JobLoadException` 停止重试、`HGETALL moai:hangfirerecurring-job:counter` 无键 | PASS（2026-11-15，修复 counter 遗留定义无限重试） |
| @HF-S20 | `redis-cli HGETALL moai:hangfirerecurring-job:resource-usage-flush` + `dotnet build src/MoAI/MoAI.csproj` | PASS（2026-11-15，Cron=`0 * * * * ?`、TimeZoneId=UTC；build 0 error，仅运行中进程占用输出文件时复制失败） |

> 已退役：@HF-S4~@HF-S8、@HF-S17~@HF-S18（实现随统计模块改造删除，证据保留于 [SOP 第 5 节](./sop.md) 历史存档）。

## 回归命令

```bash
docker exec moai-redis redis-cli SMEMBERS moai:hangfireservers          # 服务器存活
docker exec moai-redis redis-cli HGETALL moai:hangfirerecurring-job:resource-usage-flush
docker exec moai-redis redis-cli GET moai:hangfirestats:succeeded      # 隔 ~15s 再取，应增长
docker exec moai-redis redis-cli LRANGE moai:hangfiresucceeded 0 0     # 队首 job id
dotnet build src/MoAI/MoAI.csproj                                      # 期望 0 错误
```

## 覆盖率说明

- 基础设施与统计落库任务有代码走查 + build 证据；Activator 桥接由队首 job 的 Invocation 类型（仅可经 DI 构造注入）间接证明。
- 接入契约场景（@HF-S10 ~ @HF-S16）由 Wiki 源同步与团队定时器消费，各自模块 TDD 覆盖端到端行为。
