# 统计模块 SOP

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/statistics-usage-e2e.mjs](../../local-dev/statistics-usage-e2e.mjs)

## 0. 全站约定（先读）

**任何模块要做「使用量/调用次数」统计，一律走本模块，禁止自行直写数据库、自建 Redis 计数或另起统计表。** 接入只需三步（第 1 节）；需要按轮批量（agent 对话内）用第 2 节的收集器。本节为全站契约，新增资源统计前先检索本文档。

## 1. 新增一类资源统计（开发者，约 3 步）

1. 在 `ResourceUsageNames` 加资源类型名常量（如 `export-usage`）。
2. 业务侧定义 `I<X>UsageCounter` 接口（放该资源 `.Shared/Services`，供其它模块引用）与实现：注入 `IResourceUsageService`，`RecordAsync(ResourceUsageNames.X, id, 1)`（删除场景用 `RemoveAsync`），在业务模块 Module 里 `AddScoped` 注册。
3. 实现写入器 `XUsageCounterActivatorJob : IResourceUsageWriter`（`GetNameAsync` 返回常量；`WriteAsync` 事务内把增量累加到资源自身表，正负增量都处理），业务模块 Module 里 `AddScoped<IResourceUsageWriter, ...>` 注册。

无需改动统计模块本体 —— flush 与统一表自动覆盖新 name。

## 2. 对话轮内批量统计（agent 工具/技能/知识库）

工具提供者（`IAppToolProvider` 的 InvokeAsync 闭包内）注入 `IResourceUsageCollector`，调用点改为 `collector.Record(ResourceUsageNames.X, id)`（多次调用自动内存聚合）；**不要**在闭包里直接发领域服务。作用域（一轮对话/一次任务）结束时收集器自动批量推送（Dispose/DisposeAsync 兜底，flush 自建独立 scope）。注意：收集器只适合「有明确作用域结束」的场景；无作用域的场景（如会话创建）用领域服务单次推送。

## 3. 管理员操作

- **统计粒度**：设置页「资源使用统计」面板（root）按天/按小时切换，保存后下一个统计周期生效；同 key 走通用 `PUT /api/settings`（key=`STATISTICS_GRANULARITY`，值 `day`|`hour`，非法值 400）。只影响统一日表，不影响各资源自身 use_count。
- **统计查询**（Token 鉴权，管理员）：
  - `GET /api/statistics/usage/names` —— 已有统计的资源类型名清单。
  - `GET /api/statistics/usage/daily?name=&startDate=&endDate=&pageNo=&pageSize=` —— 日表分页（`resourceId` 为 "N" 格式 Guid / 十进制 id / 复合 key 文本；hour 模式下 `usageHour` 为 0~23，day 模式为 -1）。
  - `GET /api/statistics/usage/total?name=&pageNo=&pageSize=` —— 总量表，按 total 降序。
  - `GET /api/statistics/usage/trend?name=&startDate=&endDate=` —— **按日聚合趋势**（数据分析），缺省近 30 天、无数据日期补 0；插件统计页「调用趋势」图即此接口（name=plugin-usage）。

## 4. 验收

```bash
dotnet build src/MoAI/MoAI.csproj          # 0 error
node local-dev/statistics-usage-e2e.mjs [base]   # ST 32（门禁/触发/落库/查询/校验/prompt/粒度切换/插件链路+趋势）
node local-dev/app-usecount-e2e.mjs [base]       # AU 13 回归
node local-dev/skill-usecount-e2e.mjs [base]     # SKC 8 回归（含 100s 轮询）
```

## 5. 排障表

| 症状 | 排查 |
|---|---|
| 使用量迟迟不涨 | 计数为分钟级批量落库，先等 ≥1 分钟再查；查 Hangfire `resource-usage-flush` 任务是否在跑（`SucceededJobs`） |
| 对话内工具调用统计不上 | 收集器只在作用域 Dispose 时 flush，查应用日志 `轮次使用量批量推送失败`/`同步释放推送失败`；确认调用点走 `collector.Record` 而非直发命令 |
| 某资源类型计数一直为 0 | 该资源写入器是否注册（业务模块 `AddScoped<IResourceUsageWriter,...>`）；增量 key 是否符合该写入器解析格式（如 Guid 须 "N" 格式） |
| 统一表有数但资源列表 useCount=0 | 资源自身表按软删除过滤（`is_deleted=0`），资源可能已被删除；统计表保留历史计数 |
| Redis 有 `moai:counter:*` 残留不消 | 正常 —— 非数字脏数据会在下次取出时清理；若持续增长检查 flush 任务是否异常（应用日志 `资源使用统计落库失败`） |
| 切 hour 后日表还是 -1 桶 | 粒度在 flush 取批次时读取，切换后要等下一个统计周期；确认保存返回 200 且值合法 |
| 想清零某资源统计 | 直接按 name 删 `resource_usage_daily` / `resource_usage_total` 行即可（仅影响统一表展示；资源自身表与 Redis 缓冲不在统计模块管辖内） |

## 6. 运维备忘

- 日表长期增长：按 `usage_date` 归档/清理历史（当前无自动清理任务，需要时再加 Hangfire 任务）；hour 粒度数据量约为 day 的 24 倍，管理员自行权衡。
- 升级部署：存量库建表与 usage_hour 补列 DDL 已由 `DatabasPostgresModule` 幂等执行，无需手工跑 [../../asserts/resource_usage.sql](../../asserts/resource_usage.sql)（该脚本供手工兜底）。
