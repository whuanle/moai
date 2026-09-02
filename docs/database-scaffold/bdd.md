# database + PostgresScaffold 行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。本模块无独立单测，全部为命令实测/代码走查 → @manual。

```gherkin
Feature: 数据库初始化（EnsureCreated + 扩展）
  Background:
    Given PostgreSQL 容器 moai-postgres 运行于 5432（pgvector/pgvector:pg16，库 moai）
    And 应用配置 MoAI:Database 指向该库

  @DB-S1 @manual
  Scenario: 空库首次启动
    When 应用启动且数据库不存在
    Then 创建数据库、6 张表、全部索引与默认值约束
    And 创建扩展 vector 与 uuid-ossp（即便 init-pgvector.sql 未执行也能补建）
    And 写入种子：admin 用户、setting 两行、classify 99 行
    And 所有 identity 序列被重置为 max(id)+1

  @DB-S2 @manual
  Scenario: 库已存在的再次启动
    When 应用启动且库已存在
    Then EnsureCreated 不做任何变更（不建新表、不补新列、不重写种子）
    And 序列重置 DO 块照常执行（对齐到当前 max(id)）

  @DB-S3 @manual
  Scenario: 连接串错误时启动失败
    Given MoAI:Database 指向不可达的数据库
    When 应用启动
    Then 记录 Critical 日志后终止启动进程

Feature: 种子数据与幂等
  @DB-S4 @manual
  Scenario: 种子随建库写入
    Given 空库
    When 应用首次启动
    Then user 表存在 id=1 的 admin（IsAdmin=true，密码为 abcd123456 的 PBKDF2 哈希）
    And setting 表存在 root=1 与 oauth_auto_register=false
    And classify 表存在 99 行（33 名称 × prompt/plugin/app）

  @DB-S5 @manual
  Scenario: 代码修改种子不回填存量库（缺陷记录：HasData 幂等边界）
    Given 库已存在
    When 仅修改代码（如给 SettingDefinitions 加一项）并重启
    Then 已存在的库不变（HasData 不回填）
    And 新设置项需重建库或手写 INSERT 才会出现

Feature: 序列重置（显式 Id 种子的主键冲突防护）
  Background:
    Given 种子以显式 Id 插入（user id=1、setting id 1..2、classify id 1..99）
    And identity 序列未被显式 Id 推进

  @DB-S6 @manual
  Scenario: 未重置序列的失败路径（机制反面）
    When 直接 INSERT 不带 id 的行
    Then nextval 返回 1，与种子 id=1 主键冲突

  @DB-S7 @manual
  Scenario: 启动重置后的插入
    Given 启动时已执行 setval(seq, max(id)+1, false)
    When 业务插入不带 id 的行（如注册新用户）
    Then nextval 恰好返回 max+1，插入成功
    And 多次启动重复重置亦安全（以当前 max(id) 为准）

  @DB-S8 @manual
  Scenario: 空表序列
    Given 表无数据（max 为 NULL）
    When 启动期序列重置执行
    Then setval 使用 COALESCE(max,0)+1 = 1，行为同新建

Feature: 审计字段填充
  Background:
    Given 实体实现 IFullAudited（六实体全部实现）

  @DB-S9 @manual
  Scenario: 登录用户新增实体
    When 保存一个 Added 状态实体
    Then CreateUserId/UpdateUserId 记录当前用户 id
    And CreateTime/UpdateTime 记录当前时刻

  @DB-S10 @manual
  Scenario: 匿名/后台新增（种子路径）
    When UserContext 为匿名（UserId=0）
    Then CreateUserId 落 0

  @DB-S11 @manual
  Scenario: 登录用户修改实体
    When 保存 Modified 实体
    Then UpdateTime 为当前时刻，UpdateUserId 为当前用户
    And 仅当上下文 UserId 非 0 时才覆盖 UpdateUserId（后台任务不清空原值）

  @DB-S12 @manual
  Scenario: 删除转为软删除
    When Remove 一个 IDeleteAudited 实体并保存
    Then 状态改为 Modified，不产生 DELETE 语句
    And IsDeleted 为删除时刻 Ticks（非 0），审计字段同步更新

Feature: 软删除与查询过滤
  @DB-S13 @manual
  Scenario: 查询排除已删数据
    Given 表中存在 is_deleted=0 与 is_deleted 非 0 的行
    When 通过 DbSet 查询
    Then 自动附加 IsDeleted==0 过滤，只返回存活行

  @DB-S14 @manual
  Scenario: 同一业务键多次软删除
    Given 唯一索引为 (email, is_deleted)
    When 同一 email 被软删多次（每次标记值不同）
    Then 不违反唯一约束

  @DB-S15 @manual
  Scenario: 批量软删除
    When 调用 SoftDeleteAsync(query)
    Then ExecuteUpdateAsync 批量置 IsDeleted 为雪花 ID（不加载实体、不走 ChangeTracker）

  @DB-S16 @manual
  Scenario: 批量更新仍带审计
    When 调用 WhereUpdateAsync(query, setters)
    Then 业务字段与 UpdateUserId/UpdateTime 一并更新

Feature: 文件摘要存储（Sha256 命名约定）
  @DB-S17 @manual
  Scenario: hex 字符串与 bytea 互转
    Given 属性名含 sha256 的 string 字段（FileEntity.FileSha256）
    When 写入 PostgreSQL
    Then 落库为 bytea，读取时还原为 hex 字符串

Feature: Redis 注册
  @DB-S18 @manual
  Scenario: 默认客户端统一键前缀
    When 应用启动后使用默认 Redis 客户端
    Then 键统一带前缀 moai:（如 moai:hangfirejob:*）
    And 值以 System.Text.Json 序列化

  @DB-S19 @manual
  Scenario: Redis 不可达
    Given MoAI:Redis 指向不可达地址（ConnectTimeout=5000ms）
    When 启动或首次访问
    Then 按 StackExchange.Redis 默认策略失败，日志可见连接异常

Feature: PostgresScaffold 逆向工程
  Background:
    Given src/MoAI/appsettings.Development.json 含有效的 MoAI:Database（gitignore，本地自建）

  @DB-S20 @manual
  Scenario: 运行工具全量重绘
    When 执行 dotnet run --project tool/PostgresScaffold
    Then 清空并重新生成 Database/{Data,Entities}
    And 实体命名空间为 MoAI.Database.Entities、配置 internal partial、文件名补 Entity 后缀

  @DB-S21 @manual
  Scenario: 分发到解决方案
    When 生成完成
    Then Shared/Entities 与 Postgres/Data 目标目录先删除后复制新产物
    And DatabaseContext.cs 复制到 MoAI.Database.Shared 根
    And 生成物使用自定义 T4 模板（中文注释、无 DataAnnotations、partial）

  @DB-S22 @manual
  Scenario: 缺少 appsettings.Development.json
    Given 该文件不存在
    When 运行工具
    Then 报错 "未找到数据库连接字符串配置 (MoAI:Database)"

  @DB-S23 @manual
  Scenario: 未提交手改被覆盖（缺陷记录：分发先删后拷）
    Given 生成产物目录存在未提交的手工修改
    When 再次运行工具
    Then 目标目录先删除后复制，未提交修改丢失
```
