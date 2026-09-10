# Plugin And Wiki Usage Counter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为插件成功执行和知识库业务使用提供 Redis 聚合、Hangfire 刷新、PostgreSQL 原子累加能力。

**Architecture:** 插件 Core 依赖 Shared 接口，Custom 模块负责数据库映射和激活；Wiki Shared 暴露调用接口，Wiki Core 实现发送与激活。两个领域共享现有通用计数基础设施，不共享业务 key。

**Tech Stack:** .NET 10、MediatR、EF Core、Redis、Hangfire、PostgreSQL、xUnit、Moq

---

### Task 1: 插件计数闭环

**Files:**
- Create: `src/aiplugin/MoAI.AIPlugin.Shared/Services/IPluginUsageCounter.cs`
- Create: `src/aiplugin/MoAI.AIPlugin.Core/Services/PluginUsageCounter.cs`
- Create: `src/aiplugin/MoAI.AIPlugin.Custom/Services/PluginUsageCounterActivatorJob.cs`
- Modify: `src/aiplugin/MoAI.AIPlugin.Core/Commands/RunPluginCommandHandler.cs`
- Modify: `src/aiplugin/MoAI.AIPlugin.Custom/CustomPluginModule.cs`
- Modify: `src/aiplugin/MoAI.AIPlugin.Custom/MoAI.AIPlugin.Custom.csproj`

- [x] Core 项目注册 `IPluginUsageCounter`，按 `PluginEntity.Id` 通过 `IncrementCounterActivatorCommand` 发送固定 +1 的 `plugin-usage` 增量；Custom 项目注册数据库激活器。
- [x] RunPluginCommandHandler 不内置计数，使用者负责在业务成功点显式调用领域服务。
- [x] 激活器实现 `ICounterActivatorJob`，过滤非法 UUID/非正增量并在事务中以 `ExecuteUpdateAsync` 原子累加存活插件的 Counter；异常向上抛出以保留 Redis 快照。
- [x] 构建 AIPlugin Custom，0 error。

### Task 2: 知识库计数基础设施

**Files:**
- Create: `src/wiki/MoAI.Wiki.Shared/Services/IWikiUsageCounter.cs`
- Create: `src/wiki/MoAI.Wiki.Core/Services/WikiUsageCounter.cs`
- Create: `src/wiki/MoAI.Wiki.Core/Services/WikiUsageCounterActivatorJob.cs`
- Modify: `src/wiki/MoAI.Wiki.Core/WikiCoreModule.cs`
- Modify: `src/wiki/MoAI.Wiki.Core/MoAI.Wiki.Core.csproj`

- [x] Wiki Core 新增 Hangfire Shared 单向引用；通过 `IncrementCounterActivatorCommand` 发送 invariant culture 整数 field 的 `wiki-usage` 增量。
- [x] 激活器实现 `ICounterActivatorJob`，过滤非法 int/非正增量并在事务中以 `ExecuteUpdateAsync` 原子累加存活 Wiki 的 Counter；异常向上抛出以保留 Redis 快照。
- [x] 不接管理或向量化入口，也不改公开 API DTO。
- [x] 构建 Wiki Core，0 error。

### Task 3: 数据库 bigint 投影

**Files:**
- Create: `asserts/plugin_wiki_usage_counter.sql`
- Modify: `src/database/MoAI.Database.Shared/Entities/PluginEntity.cs`
- Modify: `src/database/MoAI.Database.Shared/Entities/WikiEntity.cs`
- Modify: `tool/PostgresScaffold/Database/Entities/PluginEntity.cs`
- Modify: `tool/PostgresScaffold/Database/Entities/WikiEntity.cs`

- [x] 将两个 Counter CLR 类型及相关插件 DTO 改为 `long`；现有 EF Configuration 无显式列类型，保持不变并由 Npgsql 映射为 bigint。
- [x] 提供可重复执行的 PostgreSQL DDL，并同步 `asserts/postgres.sql` 新库初始化类型。

### Task 4: 测试与验证

**Files:**
- Create: `tests/MoAI.UsageCounters.Tests/MoAI.UsageCounters.Tests.csproj`
- Create: `tests/MoAI.UsageCounters.Tests/PluginUsageCounterTests.cs`
- Create: `tests/MoAI.UsageCounters.Tests/WikiUsageCounterTests.cs`
- Modify: `MoAI.sln`

- [x] 覆盖插件领域服务 UUID field、空 id 拒绝和激活器过滤。
- [x] 覆盖 Wiki field 编码、非法 id 和激活器过滤（总计 6/6 通过）。
- [x] 运行计数器测试（8/8）与独立输出目录的主项目构建（0 error）。
- [ ] Docker 可用时验证 `ExecuteUpdateAsync` 原子累加、数据库失败保留 Redis 快照及至少一次重复边界；不可用则记录限制。

> 环境限制：2026-09-08 本机 Docker Desktop 未运行，PostgreSQL 与 Redis 集成验证未执行。