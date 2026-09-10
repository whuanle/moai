# AI 模型用量计数器设计

## 目标

在保留 `ai_model_usage_log` 逐次明细的同时，将模型调用次数、输入 token、输出 token 和总 token 异步汇总到 `ai_model_token_audit`，避免请求并发时丢失累计值。

## 方案

调用成功后的 `GatewayUsageService` 继续同步完成额度扣减、明细日志和密钥最近使用时间，再通过 Hangfire 计数命令把四项增量写入 Redis。写入与扣减均通过 `IRedisDatabase.ScriptEvaluateAsync` 执行 Lua，以统一应用 `KeyPrefix="moai:"` 并保证单次批量操作原子。计数键固定为 `v1:{modelId:N}:{teamId}:{userId}:{useType}:{useResourceId:N}:{metric}`；`modelId`/`useResourceId` 为 `Guid` 并使用小写 32 位 `N` 格式，`teamId`/`useType` 为非负 `int`，`userId` 为非负 `long`，整数均使用 invariant culture 十进制且不得为空，因此各段均不含冒号、无需转义，`metric` 只允许 `count`、`prompt`、`completion`、`total`。

Hangfire 定时激活器按 `(model_id, team_id, user_id, use_type, use_resource_id)` 归并 Redis 字段，在 PostgreSQL 事务中对 `ai_model_token_audit` 执行参数化 `INSERT ... ON CONFLICT DO UPDATE`，分别累加 `count`、`prompt_tokens`、`completion_tokens`、`total_tokens`。冲突目标使用现有部分唯一索引 `idx_ai_model_token_audit_dim_uindex` 的五个维度及 `WHERE is_deleted = 0`。格式错误、非正增量和不存在的模型不产生统计行；零 token 调用始终发送 `count=1`，只省略值为零的 token 指标。模型存在性由 UPSERT 的 `INSERT ... SELECT ... FROM ai_model WHERE id = ... AND is_deleted = 0` 保证，不额外往返查询。

通用计数器的值由 `int` 升级为 `long`；`ai_model_token_audit` 的四个累计列同步改为 PostgreSQL `bigint`，防止长期运行后溢出。逐次日志仍使用 `int`，因为单次模型响应的 token 数受模型上下文限制。

## 组件边界

- `IAiModelUsageCounter`：位于 AIChannel Shared，供模型使用者按调用结果显式计数。
- `AiModelUsageCounter`：由 AIChannel Core 注册为 scoped 领域服务，把一次调用转换成四项 Redis 增量，不访问数据库。
- `AiModelUsageCounterKey`：唯一负责计数键编码与解析。
- `AiModelUsageCounterActivatorJob`：由 AIChannel Core 以 `ICounterActivatorJob` 显式注册，校验、归并并原子 UPSERT PostgreSQL。
- `GatewayUsageService`：作为模型调用者依赖 `IAiModelUsageCounter`；不再拥有计数器实现，也不再以“先查后改”方式直接累计审计表。

## 一致性与错误处理

`SaveChangesAsync` 成功提交数据库明细后才写 Redis，并使用独立的异常捕获记录 Redis 失败，不回滚或重复数据库明细记账，也不影响上游响应。`CounterActivatorJobHandler` 使用 Hangfire 分布式锁禁止多 worker 重叠消费；刷新数据库失败时不执行 Redis 扣减，下一轮重试；刷新成功后用 Lua `HINCRBY` 原子扣除本轮快照，既保持 int64 精度，也保留期间新增的增量。PostgreSQL UPSERT 消除同一维度并发更新的丢失问题。

计数刷新仍继承现有基础设施的至少一次语义：数据库提交后、Redis 扣减前若进程崩溃，可能重复累计。首版不新增批次账本；逐次明细可用于离线对账修正。

## 验证

构建主项目，确认接口类型升级、依赖注入和 PostgreSQL SQL 均可编译；通过小型单元测试覆盖键往返解析、无效输入和四指标归并。数据库集成验证覆盖：删除/不存在模型不插入审计行；两个并发 UPSERT 的最终值为两者之和；激活器抛错后 Redis 快照不扣减；激活成功时只扣本轮快照并保留期间新增量。检查变更脚本可重复将四个累计列设为 `bigint`。