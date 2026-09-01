# 定时任务领域（Hangfire，HF）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。当前无业务 job 接入：已在跑的机制场景有 redis-cli 实测证据（见 TDD）；接入契约场景在首个业务接入时按此验收。

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

Feature: 内置 counter 任务（计数器冲刷）
  @HF-S4 @manual
  Scenario: 应用启动后自动注册
    When 应用启动完毕
    Then recurring job "counter" 注册（Job=CounterActivatorJobHandler.InvokeAsync，Queue=default，TimeZone=UTC）
    And Redis moai:hangfirerecurring-job:counter 可查到定义

  @HF-S5 @manual
  Scenario: 无激活器实现时空转成功
    Given 全仓库无 ICounterActivatorJob 实现
    When counter 任务触发
    Then 任务正常返回且统计 succeeded 持续递增，不产生 Failed

  @HF-S6 @manual
  Scenario: counter 实际执行节奏（实测发现）
    When counter 任务按 cron "* * * * ? *" 触发
    Then 该表达式为含秒 6 段式，语义是每秒而非源码注释声称的每分钟（缺陷记录：注释与实际不符）
    And 受 10 秒轮询钳制，实测相邻执行间隔约 10.1 秒（偶发不足 1 秒的重复）
    And recurring 定义中 NextExecution - LastExecution = 1000 毫秒

  @HF-S7 @manual
  Scenario: 计数器差量冲刷（机制契约）
    Given 业务已向 moai:counter:{name} 写入若干增量
    And 存在返回该 name 的激活器
    When counter 任务触发
    Then 激活器收到当前 hash 中全量正值字段（含历史未冲刷值）
    And 冲刷完成后各 field 值 = 新值 - 旧值（下限 0）
    And 冲刷期间新写入的增量被保留到下一轮

  @HF-S8 @manual
  Scenario: 单个激活器异常不影响他人
    Given 激活器 A 正常、激活器 B 抛异常
    When counter 任务触发
    Then B 的异常被捕获并记 Error 日志
    And A 正常执行，job 状态仍为成功

  @HF-S9 @manual
  Scenario: cron 段数语义（防坑）
    When 使用 6 段式含秒表达式 "* * * * ? *"
    Then 语义为每秒（被 10 秒轮询钳制为约 10 秒一次），不是每分钟
    When 需要每分钟执行时
    Then 应使用 5 段式 "0 * * * *"

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

Feature: 计数器写入（供业务高频计数）
  @HF-S17 @manual
  Scenario: 批量自增
    When 发送计数命令 { Name, Counters: { a1: 1, a2: 3 } }
    Then Redis hash moai:counter:{Name} 的各 field 原子自增对应值（batch 提交）

  @HF-S18 @manual
  Scenario: 参数校验
    When Name 为空
    Then 模型校验拒绝（名称不能为空）
    When Counters 为空字典
    Then 直接返回，不产生 Redis 调用
```
