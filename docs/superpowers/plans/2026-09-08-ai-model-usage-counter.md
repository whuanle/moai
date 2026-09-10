# AI Model Usage Counter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将模型调用次数及 token 用量通过 Redis 聚合并原子刷新到 PostgreSQL。

**Architecture:** AIChannel 领域模块提供模型用量服务及激活器；Gateway 作为使用者在成功记账后调用领域接口发送四项 `long` 增量。激活器解析维度并使用 PostgreSQL UPSERT 更新现有审计表。

**Tech Stack:** .NET 10、MediatR、StackExchange.Redis、Hangfire、EF Core、PostgreSQL

---

### Task 1: 升级通用计数器值类型

**Files:**
- Modify: `src/hangfire/MoAI.Hangfire.Shared/Models/ICounterActivatorJob.cs`
- Modify: `src/hangfire/MoAI.Hangfire.Shared/Models/IncrementCounterActivatorCommand.cs`
- Modify: `src/hangfire/MoAI.Hangfire.Core/Services/CounterActivatorJobHandler.cs`

- [x] 将计数字典从 `int` 统一改为 `long`。
- [x] 修复底层 `IDatabase` 绕过 Redis key 前缀的问题；用 Lua 原子写入，以 `HINCRBY` 精确扣减 int64 快照，并用 Hangfire 分布式锁防止重叠消费。
- [x] 运行 `dotnet build src/hangfire/MoAI.Hangfire.Core/MoAI.Hangfire.Core.csproj`，0 error。

### Task 2: 增加模型用量计数服务和激活器

**Files:**
- Create: `src/aichannel/MoAI.AIChannel.Shared/Services/IAiModelUsageCounter.cs`
- Create: `src/aichannel/MoAI.AIChannel.Core/Services/AiModelUsageCounterKey.cs`
- Create: `src/aichannel/MoAI.AIChannel.Core/Services/AiModelUsageCounter.cs`
- Create: `src/aichannel/MoAI.AIChannel.Core/Services/AiModelUsageCounterActivatorJob.cs`
- Modify: `src/aichannel/MoAI.AIChannel.Core/AIChannelCoreModule.cs`
- Modify: `src/aichannel/MoAI.AIChannel.Core/MoAI.AIChannel.Core.csproj`

- [x] 实现 `v1:{modelId:N}:{teamId}:{userId}:{useType}:{useResourceId:N}:{metric}` 键格式；两个 UUID 使用小写 `N` 格式，`teamId/useType` 为非负 `int`，`userId` 为非负 `long`，整数使用 invariant culture 十进制且不得为空，指标严格限定为 `count/prompt/completion/total`。
- [x] 每次成功调用始终发送 `count=1`，仅发送值大于零的 prompt/completion/total 增量，保证零 token 调用仍被计次。
- [x] 在 `AIChannelCoreModule` 将领域服务和 `ICounterActivatorJob` 显式注册为 scoped。
- [x] 按五个业务维度归并增量；通过 `INSERT ... SELECT FROM ai_model` 排除不存在或已删除模型，并在事务中对 `ai_model_token_audit` 用参数化 PostgreSQL UPSERT 原子累加四个字段。
- [x] 构建 AIChannel Core，0 error。

### Task 3: 接入网关记账路径

**Files:**
- Modify: `src/gateway/MoAI.Gateway.Core/Services/GatewayUsageService.cs`

- [x] 删除现有审计表“先查后改”逻辑。
- [x] Gateway 依赖 `IAiModelUsageCounter`，等待 `SaveChangesAsync` 成功提交数据库明细后再调用领域服务，计数发送不参与数据库事务。
- [x] 对 Redis 计数调用使用独立异常捕获并记录模型/团队/用户维度，确保失败不回滚数据库明细或影响上游响应。
- [x] 构建 AIChannel Core 与 Gateway Core，0 error。

### Task 4: 将累计列升级为 bigint

**Files:**
- Create: `asserts/ai_model_usage_counter.sql`
- Modify: `src/database/MoAI.Database.Shared/Entities/AiModelTokenAuditEntity.cs`
- Modify: `src/database/MoAI.Database.Shared/Entities/AiModelUsageLogEntity.cs`
- Modify: `tool/PostgresScaffold/Database/Entities/AiModelTokenAuditEntity.cs`
- Modify: `tool/PostgresScaffold/Database/Entities/AiModelUsageLogEntity.cs`

- [x] 将累计字段和两个表的 `user_id` CLR 类型改为 `long`。
- [x] 提供对应 `ALTER COLUMN ... TYPE bigint` 的 PostgreSQL DDL；PostgreSQL 对已是 `bigint` 的列重复执行保持成功。
- [x] 构建模型调用链相关项目，0 error。

### Task 5: 最终验证

- [x] 运行主项目构建（输出改到临时目录以避开运行中进程锁定），0 error。
- [x] 用 xUnit 测试验证键往返解析、非法键拒绝、正常四项增量、零 token 仍计次、负 token 拒绝和归并（10/10 通过）。
- [x] 走查 Redis 发送位于数据库提交之后且具有独立异常捕获。
- [ ] 使用 PostgreSQL 测试事务验证不存在/软删模型不插入、两个并发 UPSERT 最终累加值正确，并回滚测试数据。
- [ ] 使用隔离 Redis key 验证激活器异常时快照不扣减、成功时仅扣除旧快照并保留并发新增量，测试后删除隔离 key。
- [x] 走查 `CounterActivatorJobHandler`：激活器抛异常时不扣 Redis；成功时 Lua 原子扣除快照并保留并发新增量。
- [x] 检查 UPSERT 的表名、五列冲突目标和四列累加映射与 EF 配置一致。
- [x] 检查变更仅涉及计数管线、Gateway 接线、数据库投影、测试和对应文档。
- [x] 不创建提交，由仓库维护者审阅后自行提交。

> 环境限制：2026-09-08 本机 Docker Desktop 未运行，PostgreSQL 与 Redis 两项集成验证未执行；对应任务保持未勾选。