# 团队定时器 BDD

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 验收脚本：[local-dev/team-timer-smoke.mjs](../../local-dev/team-timer-smoke.mjs)

```gherkin
Feature: 团队定时器管理
  团队管理员把已发布的 Agent/流程应用绑定到定时器，按 cron 定时无头执行固定提问，并可立即执行调试。

  @TIMER-S1
  Scenario: 创建定时器并注册调度
    Given 团队里有一个已发布的 Agent 应用
    When 管理员创建定时器：名称、绑定应用、5 段 cron（UTC）、执行内容、重试策略并启用
    Then 创建成功返回定时器 id
    And 列表中能看到该定时器的绑定应用名称与下次调度时间
    And 同名再建返回冲突
    And 非法 cron（段数/取值范围不合法）被拒绝且不落库

  @TIMER-S2
  Scenario: 绑定应用合法性
    When 管理员创建定时器绑定一个未发布的应用
    Then 请求被拒绝并提示先发布应用

  @TIMER-S3
  Scenario: 角色门禁
    When 团队成员（Member）创建定时器
    Then 被拒绝（仅管理员可管理定时器）
    But 该成员可以查看定时器列表

  @TIMER-S4
  Scenario: 立即执行成功并记录历史
    Given 定时器绑定的应用可正常回答
    When 管理员点击立即执行
    Then 返回执行记录 id 并异步执行
    And 执行历史出现一条手动触发记录：状态成功、尝试 1 次、触发人为操作人、回答文本完整
    And 定时器的最近触发时间被刷新

  @TIMER-S5
  Scenario: 失败按重试策略重试后终态失败
    Given 定时器执行内容会使应用执行失败，重试次数 1、间隔 10 秒
    When 管理员立即执行
    Then 执行记录先进入待重试（含下次重试时间）
    And 重试仍失败后终态为失败、尝试 2 次、失败原因完整

  @TIMER-S6
  Scenario: 停用与删除的调度语义
    When 管理员停用定时器
    Then 列表不再返回下次调度时间
    When 管理员删除定时器
    Then 列表不再出现该定时器
    But 执行历史仍可查看（保留 2 条）
```

```gherkin
Feature: 定时器页面（团队分区）
  团队管理分区下的定时器列表页，管理员可建/改/删/启停/立即执行/看历史。

  @TIMER-S7
  Scenario: 管理员视角的列表与操作
    Given 当前用户是团队管理员
    Then 可见新建按钮与每行的立即执行/历史/编辑/删除操作与启用开关

  @TIMER-S8
  Scenario: 成员视角只读
    Given 当前用户是团队成员
    Then 无新建按钮与操作列，仅能查看列表

  @TIMER-S9
  Scenario: 新建弹窗与执行历史抽屉
    When 管理员打开新建弹窗
    Then 可见绑定应用（未发布应用禁选）/调度表达式（UTC 快选）/执行内容/重试配置
    When 点击立即执行
    Then 执行历史抽屉打开并默认加载最近 10 条，执行中有记录时自动轮询到终态
```
