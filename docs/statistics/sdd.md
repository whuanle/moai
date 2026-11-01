# 统计模块 SDD（资源使用统计）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../hangfire/sdd.md](../hangfire/sdd.md)、[../app/sdd.md](../app/sdd.md)、[../skill/sdd.md](../skill/sdd.md)、[../aiplugin-static/sdd.md](../aiplugin-static/sdd.md)、[../prompt/sdd.md](../prompt/sdd.md) ｜ 证据：[local-dev/statistics-usage-e2e.mjs](../../local-dev/statistics-usage-e2e.mjs)

## 1. 目标

平台内多资源（插件/应用/技能/知识库/AI 模型用量/提示词）都有「使用量」统计诉求。本模块是**全站使用量统计的唯一入口**：业务侧只通过领域服务把增量送进 Redis，统计模块定期批量取出，按资源类型分发给写入器落库，并同步维护统一日表/总量表。**禁止业务模块自行直写数据库或自建计数**（接入指引见 [sop.md 第 1 节](./sop.md)）。

## 2. 组件（src/statistics/，MoAI.Statistics.Shared/Core/Api）

- **Shared**（消费方只引用此层）：
  - `IResourceUsageService`（**统计领域服务，业务模块统一入口**）：`RecordAsync(name, resourceId, count)` / `RecordAsync(name, counters)` / `RemoveAsync(name, resourceId, count)`（减），实现聚合成 `IncrementResourceUsageCommand` 批量原子推送 Redis。
  - `IResourceUsageCollector`（**对话轮次收集器**，scoped）：一轮对话内多次工具/技能/知识库调用 `Record()` 内存聚合，作用域 Dispose 时按资源类型各推送一次 Redis（批量原子增）。
  - `IResourceUsageWriter`（写入器接口）：`GetNameAsync()` + `WriteAsync(dict)`，每个资源类型一个实现，DI 自动发现。
  - `IncrementResourceUsageCommand`（MediatR）：`Name` + `Counters`（资源 id 文本 → 增量，**正增负减、不可为 0**）。
  - `ResourceUsageNames` 常量：`plugin-usage` / `app-usage` / `skill-usage` / `wiki-usage` / `ai-model-usage` / `prompt-usage`。
  - 查询四命令：`QueryResourceUsageDailyCommand`（分页，name/日期区间过滤，响应含 `usageHour`）、`QueryResourceUsageTotalCommand`、`QueryResourceUsageNamesCommand`、`QueryResourceUsageTrendCommand`（**按日聚合趋势**：缺省近 30 天、区间钳制 ≤366 天、无数据日期补 0、不带 name 统计全部类型合计；插件统计页趋势图数据源）。
- **Core**：
  - `IncrementResourceUsageCommandHandler`：Lua 脚本循环 `HINCRBY counter:{name}`（原子批量增/减；Redis 全局 KeyPrefix `moai:`）。
  - `ResourceUsageFlushJobHandler`：Hangfire 周期任务 `resource-usage-flush`（**每分钟**，cron `0 * * * * ?`）。对每个写入器：Lua **原子取出**（`HGETALL`+`DEL` 同脚本，取出即删，非数字脏数据清除、正负增量保留）→ `writer.WriteAsync` → `ResourceUsageStore.RecordAsync`；任一环节抛异常则把整批增量经领域服务回补 Redis 后重抛（计数不丢，下周期重试）。
  - `ResourceUsageCollector`：收集器实现。**flush 时自建独立 scope** 解析领域服务 —— 收集器常在宿主 scope 的 Dispose 路径上被调用，彼时原 scope 的 MediatR 等已按逆序释放，直接复用会 `ObjectDisposedException`；同时实现 `IDisposable`（同步 flush 兜底，覆盖 `using var` 同步释放的作用域）。
  - `ResourceUsageStore`：统一统计表落库，参数化 `INSERT ... ON CONFLICT DO UPDATE` 累加（upsert，含小时桶列）。
- **Api**：`StatisticsController` `/statistics/usage/daily|total|names`，全部 `EnsureAdminAsync`（403）；查询命令走 `[FromQuery]` 整模型绑定，SharpGrip 自动校验（400）。

## 3. 覆盖资源与触发点（六类）

| name | 资源自身表 | 触发点 |
|---|---|---|
| `plugin-usage` | `plugin.counter` | AI 对话工具调用（PluginAppToolProvider → **轮次收集器**）；插件管理运行（RunPluginCommand → 门面计数器单次推送） |
| `app-usage` | `app.use_count` | 会话创建（正式/A2A/ACP/外部，门面计数器单次推送）；调试会话不计 |
| `skill-usage` | `skills.use_count` | 模型装载技能工具（SkillAppToolProvider → **轮次收集器**） |
| `wiki-usage` | `wikis.counter` | 知识库检索 `IWikiSearchService.SearchAsync`（**轮次收集器**；召回测试/外部召回/MCP 工具/AI RAG 全部汇经此入口，每个被检索的知识库计 1 次） |
| `ai-model-usage` | `ai_model_token_audit` | 每次模型调用 token 用量（UsageCapturingChatClient，门面计数器批量推送，复合 key） |
| `prompt-usage` | `prompts.use_count` | 会话选用专家提示词（创建/切换会话，门面计数器单次推送） |

各资源模块保留类型化门面（`IPluginUsageCounter`/`IAppUsageCounter`/`ISkillUsageCounter`/`IWikiUsageCounter`/`IAiModelUsageCounter`/`IPromptUsageCounter`，均委托领域服务）。

## 4. 数据

- `resource_usage_daily`（`ResourceUsageDailyEntity`）：`(usage_date, usage_hour, name, resource_id)` 唯一，`count` 累加；usage_date/hour 均为 UTC。`usage_hour=-1` 表示按天粒度（整日一个桶），`0~23` 为按小时粒度的小时桶。辅助索引 `(name, usage_date)`。
- `resource_usage_total`（`ResourceUsageTotalEntity`）：`(name, resource_id)` 唯一，`total` 累加。
- `resource_id` 统一 varchar(128) 字符串形态：插件/应用/技能为 Guid "N" 格式（32 hex），知识库/提示词为十进制 int，AI 模型为复合 key `v1:{modelId:N}:{teamId}:{userId}:{useType}:{useResourceId:N}:{metric}`（最长约 110）。
- 存量库幂等 DDL 在 `DatabasPostgresModule`（含 usage_hour 补列与唯一索引三列→四列替换，与 [../../asserts/resource_usage.sql](../../asserts/resource_usage.sql) 等价）；无软删除列、无审计人（纯机器写入）。

## 5. 关键决策

1. **写入器留在各业务模块**（实现统计模块接口），统计模块只管 Redis 与统一表 —— 业务表结构知识不外泄。
2. **Redis key 沿用 `counter:{name}`**（迁移自 hangfire 模块），一个资源类型一个 hash、资源 id 为 field，key 数量只随资源类型增长。
3. **取出即删 + 失败回补**：正常路径原子、无重复累加；落库失败回补重试。代价：取出后进程崩溃丢当批（≤1 分钟增量）。
4. **轮次批量**：agent 一轮对话内多次工具调用经收集器聚合，作用域结束时按资源类型各推一次（原先每次调用推一次）。
5. **粒度管理员可配**：设置项 `STATISTICS_GRANULARITY`（day/hour，默认 day，root 经通用 `/settings` PUT 修改、前端设置页「资源使用统计」面板），flush 按粒度写 `-1` 或当前 UTC 小时桶；只影响统一日表，资源自身 use_count 不受影响。
6. 旧 cron `"* * * * ? *"`（每秒语义）修正为每分钟 `0 * * * * ?`；hangfire 模块回归纯「定时任务扩展点」。

## 6. 已知问题

- flush「取出后、落库前」进程崩溃丢当批增量（≤1 分钟窗口）；落库部分成功（写入器成功、统一表失败）时整批回补会导致下周期重复累加。均为小概率且方向可容忍，不做分布式事务。
- 收集器在宿主 scope 同步 Dispose 路径上以 `GetAwaiter().GetResult()` 同步 flush（自建独立 scope）——ASP.NET Core 无同步上下文，安全。
- 日表无自动归档/清理，长期增长需运维定期归档（见 sop）。
