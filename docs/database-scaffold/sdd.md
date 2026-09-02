# database + PostgresScaffold 设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（连接配置/SystemOptions） ｜ 证据：psql/构建命令（见 [TDD](./tdd.md)）
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@DB-Sxx），本文不重复。

## 目标

database 层为全部领域模块统一装配 PostgreSQL 数据访问与 Redis 缓存：

1. **EF Core 上下文**：`DatabaseContext`（partial 三文件）+ `PostgresDatabaseContext`（Postgres Provider 差异）。
2. **启动初始化**：`DatabasPostgresModule` 装配期 `EnsureCreated()`（建库建表+种子）并执行全库序列重置（[@DB-S1](./bdd.md#db-s1)）。
3. **Redis**：`DatabaseCoreModule` 注册默认客户端（键前缀 `moai:`，[@DB-S18](./bdd.md#db-s18)）。
4. **DB-first 工作流**：`tool/PostgresScaffold` 从既有库逆向生成实体/配置/DbContext 并分发回 `src/database/`；`init-pgvector.sql` 提供扩展初始化。

## 组件

```
src/database/
├── MoAI.Database.Shared/        引用 Infra.Shared；EF Core Relational + LinqKit + StackExchange.Redis.Extensions
│   ├── DatabaseContext.cs       6 个 DbSet + OnModelCreatingPartial 骨架（t4 生成）
│   ├── *.Configuration.cs       构造挂审计事件；Sha256 转换器；软删过滤器；种子装配
│   ├── *.Extensions.cs          SoftDeleteAsync / WhereUpdateAsync（批量操作带审计）
│   ├── Audits/                  ICreationAudited / IModificationAudited / IDeleteAudited / IFullAudited
│   ├── Entities/                User/Setting/Classify/File/OauthConnection/UserOauthConnection（t4 生成）
│   ├── Seed/                    UserSeed / SettingSeed / ClassifySeed / SettingDefinitions
│   └── Helper/                  TransactionScopeHelper（Required+AsyncFlow，RepeatableRead）
├── MoAI.Database.Postgres/      Npgsql EF 10.0.3；双程序集 ApplyConfigurations + vector/uuid-ossp 扩展
│                                + UTF8 collation；DatabasPostgresModule（Npgsql 开关 + 初始化）
└── MoAI.Database.Core/          DatabaseCoreModule：注册 Redis（见下）

tool/PostgresScaffold/           独立 console（Exe）：EF 逆向工程 + 自定义 T4 模板 + 分发
init-pgvector.sql                CREATE EXTENSION vector; uuid-ossp（docker initdb 挂载）
```

装配：`MainModule → DatabaseCoreModule → DatabasPostgresModule`；注册 `AddDbContext<DatabaseContext, PostgresDatabaseContext>()`（服务类=抽象，领域层统一注入 `DatabaseContext`）。

## 关键决策

1. **审计填充**：构造函数挂 `ChangeTracker.Tracked/StateChanged` 双事件 → `AuditFilter`。Added 填 Create+Update 审计；Modified 仅在 UserContext 非空且 UserId≠0 时覆盖 UpdateUserId（后台任务不清空原值）；**Deleted 改状态为 Modified + `IsDeleted=删除时刻 Ticks`**，不产生 DELETE（[@DB-S9](./bdd.md#db-s9)~[@DB-S12](./bdd.md#db-s12)）。
2. **软删除语义**：`IsDeleted` 0=存活、非 0=已删（Ticks 或雪花 ID）；`IDeleteAudited` 实体自动挂全局过滤器 `IsDeleted==0L`（表达式树）；唯一索引均为 `(业务键, is_deleted)` 复合，同键可多次软删（[@DB-S13](./bdd.md#db-s13)~[@DB-S16](./bdd.md#db-s16)）。
3. **Sha256 命名约定**：string 属性名含 "sha256"（忽略大小写）→ hex↔byte[] 转换器 + 列型 `bytea`（[@DB-S17](./bdd.md#db-s17)）。
4. **批量操作**：`SoftDeleteAsync`（ExecuteUpdate 置雪花 ID）/ `WhereUpdateAsync`（业务 setter 合并审计 setter）——绕过 ChangeTracker 的批量路径也带审计。
5. **初始化用 EnsureCreated 而非 EF Migrations**（`Migrate()` 被注释）：无迁移历史表、不做增量演进；`HasData` 种子以显式 Id INSERT 不推进 identity 序列，故启动期 DO 块全库 `setval(seq, max(id)+1, false)`（[@DB-S6](./bdd.md#db-s6)~[@DB-S8](./bdd.md#db-s8)）；初始化失败 LogCritical 后 rethrow 终止进程（[@DB-S3](./bdd.md#db-s3)）。
6. **Npgsql 兼容开关**：`EnableLegacyTimestampBehavior` / `DisableDateTimeInfinityConversions`（全局 AppContext）。
7. **Redis 注册**：`SystemTextJsonSerializer` + 默认库单例；`KeyPrefix="moai:"`、PoolSize=10、ConnectTimeout=5000——运维 DEL 勿漏前缀。

## 表与种子（psql 实测，与 Configuration 源码一致）

| 表 | 主键/默认值 | 关键索引 |
|---|---|---|
| `user` | id bigint identity | 唯一 (user_name/email/phone, is_deleted) 三组 + 各自单列索引 |
| `setting` | id int identity | `setting_key_index` **hash** 索引 (key) |
| `classify` | id int identity | name/type 普通索引 |
| `file` | id bigint identity | idx_file_file_sha256 (bytea)、idx_file_object_key |
| `oauth_connection` | id uuid 默认 uuid_generate_v4() | — |
| `user_oauth_connection` | id bigint identity | 唯一 (provider_id, sub, is_deleted) |

全部表含审计五件套列（create_user_id/create_time/update_user_id/update_time/is_deleted）。种子（HasData，随 EnsureCreated 写入，[@DB-S4](./bdd.md#db-s4)）：admin（id=1、IsAdmin=true、密码为 abcd123456 的 PBKDF2 哈希，**上线即改**）；setting 2 行（`root=1`、`oauth_auto_register=false`）；classify **99 行**（33 名称 × prompt/plugin/app）。新设置项先注册进 `SettingDefinitions`；HasData 不回填存量库（[@DB-S5](./bdd.md#db-s5)）。

## 迁移策略（EnsureCreated 取舍，as-built）

- schema 变更 = 手写 DDL 改库 → PostgresScaffold 逆向再生成（DB-first，EF 模型是库结构的投影，[@DB-S20](./bdd.md#db-s20)）。
- 代价：EnsureCreated 对已存在的库零动作（[@DB-S2](./bdd.md#db-s2)）；线上 DDL 与回滚人工管理；无版本化升级脚本。
- 好处：无迁移文件负担；工具一键全量重绘；库与代码强一致。

## PostgresScaffold 工具

独立控制台工程，封装 EF Core `IReverseEngineerScaffolder` + Npgsql design-time 服务，用自定义 T4 模板（`CodeTemplates/EFCore/*.t4`：中文注释、无 DataAnnotations、partial、`MoAI.Database(.Entities)` 命名空间）逆向整个库。流程：清空重建 `Database/{Data,Entities}` → 逆向生成 → `DistributeToSolution` 分发（实体补 `Entity` 后缀 → `Shared/Entities/`、配置 → `Postgres/Data/`、DatabaseContext.cs → Shared 根）。生成后需人工补 `IFullAudited` 与种子并 `dotnet build` 回归。前置与风险见 [@DB-S22](./bdd.md#db-s22)/[@DB-S23](./bdd.md#db-s23) 与 [SOP](./sop.md)。

## 本地环境

docker-compose `postgres` 服务：镜像 `pgvector/pgvector:pg16`，容器 `moai-postgres`，5432、用户 postgres、密码 moai123456、库 moai；`init-pgvector.sql` 只读挂载 `/docker-entrypoint-initdb.d/`（仅首次建卷执行）。`HasPostgresExtension("vector")/("uuid-ossp")` 使 EnsureCreated 亦会补建扩展（双保险）。

## 已知问题

1. `EnableSensitiveDataLogging()` + `EnableDetailedErrors()` 无条件开启（不区分环境），SQL 参数可能进入日志——生产按环境关闭。
2. EnsureCreated 无法演进既有库，新增实体/列不生效（见迁移策略）。
3. PostgresScaffold 依赖 `src/MoAI/appsettings.Development.json`（gitignore），克隆后开箱不可用。
4. 分发步骤**先删除再重建** `Shared/Entities` 与 `Postgres/Data`——未提交手改被覆盖（[@DB-S23](./bdd.md#db-s23)）；手改应放 partial 或生成后立即提交。
5. `Program.cs` 命名空间为 `MysqlScaffold`（MySQL→PostgreSQL 历史遗留）。
6. 匿名/种子路径 CreateUserId 落 0（[@DB-S10](./bdd.md#db-s10)，admin 行实测）。
7. 时间默认值不一致：user/setting/file/user_oauth_connection 用 `timezone('utc',now())`，classify/oauth_connection 为 `CURRENT_TIMESTAMP`（t4 两种产物，无实际危害）。
8. Redis `PoolSize=10` 等参数硬编码，未暴露配置。
