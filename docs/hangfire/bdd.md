# 定时任务领域（Hangfire，HF）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。业务已接入 Wiki 外部源同步、团队定时器与资源使用统计落库；机制场景有 redis-cli 实测证据（见 TDD），接入契约场景在新增业务 job 时按此验收。

```gherkin
Feature: 调度基础设施（随主进程启动）
  Background:
    Given 后端进程启动（注入 HangfireCoreModule）
    And Redis 可达（本地容器 moai-redis）

  @HF-S1 @manual
  Scenario: Hangfire 服务器注册
    When 应用启动完成
    Then Redis 集合 moai:hangfireservers 出现 1 个 server 条目
    And 调度轮询间隔 10 秒，worker 数 = CPU 数 * 2

  @HF-S2 @manual
  Scenario: 存储 key 前缀隔离
    When Hangfire 写入任意结构
    Then 所有键以 moai:hangfire 为前缀且不补冒号（如 moai:hangfirejob:{id}、moai:hangfireservers）
    And 与业务缓存键（moai: 前缀）隔离，清库时勿误删

  @HF-S3 @manual
  Scenario: 无管理界面
    When 请求任意 /hangfire 路径
    Then 返回 404（未配置 Dashboard；观测走 Redis 键与日志）

Feature: 资源使用统计落库任务（统计模块注册）
  @HF-S19 @manual
  Scenario: 自动清理任务体已不存在的遗留定义
    Given 存量 Redis 中存在某个 recurring job，其任务体类型/方法已在当前版本删除
    When 应用启动完成（或运行满 1 小时后的兜底扫描）
    Then 该定义 LoadException 非空且 CreatedAt 早于 24 小时宽限期时被 RemoveIfExists 幂等删除，记 Warning（任务Id/创建时间/原因）
    And 之后不再出现 JobLoadException 每 10 秒重试
    And 创建时间在宽限期内的定义被跳过（滚动发布不误删新版本任务）
    And 只删定义，不动 moai:counter:* 计数与历史执行记录
    And 无遗留定义的环境不产生任何删除与告警

  @HF-S20 @manual
  Scenario: 注册每分钟统计落库任务
    When 应用启动完成
    Then recurring job "resource-usage-flush" 注册（Job=ResourceUsageFlushJobHandler.InvokeAsync，Cron=`0 * * * * ?`，Queue=default，TimeZone=UTC）
    And 任务体带 DisableConcurrentExecution(300) 防重叠
    And 执行结果写入资源自身表与统一统计表，失败回补 Redis 下轮重试（见 ../statistics/sop.md）

Feature: cron 表达式语义
  @HF-S9 @manual
  Scenario: cron 段数语义（防坑）
    When 使用 6 段式含秒表达式 "0 * * * * ?"
    Then 语义为每分钟第 0 秒执行
    When 使用 5 段式 "0 * * * *"
    Then 语义同为每小时第 0 分（Hangfire 兼容两种段数，按需选型）

> 已退役场景（编号不复用）：@HF-S4~@HF-S8（内置 counter 任务注册/空转/执行节奏/差量冲刷/异常隔离）、@HF-S17~@HF-S18（IncrementCounterActivatorCommand 写入与校验）——对应实现已随统一统计模块改造删除，能力由 @HF-S19/@HF-S20 与 [../statistics/bdd.md](../statistics/bdd.md) 承接。

Feature: 新增周期任务（接入契约，接入即验收）
  Background:
    Given 业务领域引用 MoAI.Hangfire.Shared
    And 已定义 MyJobCommand : RecuringJobCommand<MyParams> 及其 IRequestHandler

  @HF-S10 @manual
  Scenario: 注册周期任务
    When 调用注册接口（key、cron、params）
    Then recurring job 写入（Job=RecurringJobHandler<MyJobCommand,MyParams>.HandlerAsync）
    And 下一次 cron 触发（按 UTC）时 Handler 收到新 TaskId、Key、CronExpression 与原样 Params
    And job 实例经 DI 桥接从独立 scope 解析（可构造注入 scoped 服务）
    And 返回 IsCancel=false 时任务保留并按 cron 继续

  @HF-S11 @manual
  Scenario: 任务自行取消
    When 某次执行 Handler 返回 IsCancel=true
    Then 注册定义被移除，后续不再触发（查询 IsExist=false）

  @HF-S12 @manual
  Scenario: 任务体异常被吞（缺陷记录）
    When Handler 内执行抛出异常
    Then 异常被捕获仅记 Warning 日志
    And Hangfire 不重试、状态仍记成功（与正常执行不可区分，排查看日志）

  @HF-S13 @manual
  Scenario: 同 key 重复注册幂等
    When 以相同 key 但不同 cron 再次注册
    Then 旧定义被覆盖，不产生第二个任务

  @HF-S14 @manual
  Scenario: 查询与手动取消
    When 查询某任务
    Then 返回 IsExist、Cron、NextExecution、LastExecution、LastJobState
    When 手动取消该任务
    Then 任务删除，查询 IsExist=false

Feature: 延时与一次性任务（接入契约）
  @HF-S15 @manual
  Scenario: 指定开始时间的周期任务
    When 以（key、startTime、cron、params）注册
    Then 首次触发精确到分钟（UTC），到点执行一次
    And 执行完业务后按传入 cron 重注册为周期任务（cron 为空则删除）

  @HF-S16 @manual
  Scenario: 一次性任务
    When 以（key、startTime、params）注册（无 cron）
    Then 到点执行一次后任务自删
```
