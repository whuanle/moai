# database + PostgresScaffold 验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。实测环境：容器 moai-postgres（本机未装 psql，全部经 `docker exec moai-postgres psql -U postgres -d moai` 等价执行）。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DB-S1 | `psql -c "\dt"`（6 表）、`pg_extension`（vector/uuid-ossp）、种子查询（见 [SOP 第 6 节](./sop.md)） | PASS：6 表、vector 0.8.6、uuid-ossp 1.1、种子齐全（2026-09-01） |
| @DB-S2、@DB-S5 | EnsureCreated/HasData 语义代码走查（DatabasPostgresModule） | PASS（2026-09-01，代码级） |
| @DB-S3 | 初始化失败 LogCritical + rethrow 走查 | PASS（2026-09-01，代码级） |
| @DB-S4 | `SELECT ... FROM "user"/setting` + `SELECT count(*) FROM classify` | PASS：admin(id=1,is_admin=t)、setting 2 行、classify 99（2026-09-01） |
| @DB-S6 ~ @DB-S8 | `SELECT last_value,is_called FROM setting_id_seq` → `3 / f`；`user_id_seq=45=max(id)` | PASS（2026-09-01） |
| @DB-S9 ~ @DB-S12 | AuditFilter 走查（双事件三分支）+ admin 行 `create_user_id=0` 实测 | PASS（2026-09-01，代码级+实例佐证） |
| @DB-S13 ~ @DB-S16 | DatabaseContext.Configuration/Extensions 走查（过滤器/SoftDeleteAsync/WhereUpdateAsync） | PASS（2026-09-01，代码级） |
| @DB-S17 | `psql -c "\d file"` → `file_sha256 bytea not null` | PASS（2026-09-01） |
| @DB-S18 | `docker exec moai-redis redis-cli --scan --pattern "moai:*"`（命中 hangfirejob 等，DBSIZE=7209） | PASS（2026-09-01） |
| @DB-S19 | 配置走查（ConnectTimeout=5000） | PASS（2026-09-01，代码级） |
| @DB-S20 ~ @DB-S23 | `dotnet build tool/PostgresScaffold/PostgresScaffold.csproj` 0 错误 + 生成物与分发目标一致性抽查（User/Setting/File）+ csproj 编译边界走查 | PASS（2026-09-01，见覆盖率说明） |

## 回归命令

```bash
dotnet build src/MoAI/MoAI.csproj                             # 0 错误（2026-09-01 实测，3.39s）
dotnet build tool/PostgresScaffold/PostgresScaffold.csproj    # 0 错误（2026-09-01 实测，56.01s）
docker exec moai-postgres psql -U postgres -d moai -c "\dt" -c "SELECT count(*) FROM classify;"
docker exec moai-redis redis-cli --scan --pattern "moai:*" | head
```

## 覆盖率说明

- 本模块无独立单测，全部为命令实测（psql/Redis/构建）+ 代码走查，故全部 @manual。
- @DB-S20~S23 本轮**未实际执行逆向生成**（避免重写 `src/database/` 工作区），以「工具构建 0 错误 + 生成物/分发目标一致性 + 编译边界」替代；完整工具操作留待 schema 变更时按 [SOP 第 1 节](./sop.md) 执行。
