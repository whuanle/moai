# 插件与知识库使用计数器设计

## 目标

复用现有 Redis + Hangfire 计数管线，为插件和知识库维护可快速查询的累计使用次数，同时避免旧实现的 SQL 拼接、键类型过时和并发覆盖问题。

## 统计口径

- 插件：使用者取得 `PluginEntity.Id` 后显式调用 `IPluginUsageCounter.IncrementAsync(Guid pluginId)`，每次调用固定增加 1。领域服务不判断业务成功与否，该判断由调用者负责。
- 知识库：使用者取得 `WikiEntity.Id` 后显式调用 `IWikiUsageCounter.IncrementAsync(int wikiId)`，每次调用固定增加 1。当前仓库尚无检索/问答入口，因此不把管理查询、文档上传或向量化误算为使用。
- 知识库插件未来同时对应插件记录和 Wiki 检索时，两类成功事件分别计数，各加一次。

## 组件

- `IPluginUsageCounter` 位于 AIPlugin Shared，AIPlugin Core 实现按 `PluginEntity.Id` 发送 Redis 增量；Custom 模块只实现依赖数据库的 PostgreSQL 激活器。插件执行 Handler 不内置计数，具体使用者显式调用领域服务。
- `IWikiUsageCounter` 位于 Wiki Shared；Wiki Core 已单向依赖 Database，只新增 Hangfire Shared 引用并实现 Redis 增量发送和 PostgreSQL 激活器，不形成环。
- Redis counter name 分别为 `plugin-usage`、`wiki-usage`；field 分别为 UUID `N` 格式和 invariant culture 十进制整数。
- 发送端统一调用现有 MediatR `IncrementCounterActivatorCommand`；激活器统一实现现有 `ICounterActivatorJob`。激活器过滤非法 key、非正增量和软删除实体，在事务中以 EF `ExecuteUpdateAsync` 执行 `counter = counter + delta`。

## 数据与失败语义

`plugin.counter`、`wiki.counter` 从 PostgreSQL `integer` 升级为 `bigint`，CLR 类型从 `int` 升级为 `long`；现有 EF Configuration 无显式列类型，因此 CLR `long` 自动映射 `bigint`，无需修改配置。发送 Redis 失败不改变插件执行结果，只记录错误；刷新数据库抛异常时事务回滚，通用 Hangfire 管线不扣 Redis 快照，下一轮重试。本轮不改公开 API，避免产生未同步的 Kiota 客户端契约。

与模型计数器一致，该方案为跨 PostgreSQL/Redis 的至少一次统计：数据库更新成功但 Redis 扣减前进程崩溃可能重复。后续若要求财务级精确，应增加持久化 outbox/批次幂等表，而不是继续扩大内存或 Lua 逻辑。

## 验证

单元测试覆盖模型、插件、Wiki 领域服务的命令编码与非法输入过滤；主项目构建验证模块依赖和 DI。Docker 集成测试覆盖数据库失败后 Redis 快照保留、成功刷新后 Counter 原子累加，以及跨系统提交后崩溃可能重复的已知至少一次边界；Docker 不可用时明确记录未执行。