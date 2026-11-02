# 登录安全风控 行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../auth/bdd.md](../auth/bdd.md) ｜ 证据：[local-dev/loginsafety-e2e.mjs](../../local-dev/loginsafety-e2e.mjs)

```gherkin
Feature: 登录日志与来源环境记录
  每一次登录尝试（成功或失败、账密或 OAuth）都留下含来源 IP、归属地与 User-Agent 的日志

  @LS-S1 @auto:e2e
  Scenario: 非管理员不能查看登录安全数据
    Given 普通成员已登录
    When 其查询登录日志
    Then 返回 403

  @LS-S2 @auto:e2e
  Scenario: 未登录不能查看登录日志
    When 匿名请求登录日志接口
    Then 返回 401

  @LS-S3 @auto:e2e
  Scenario: 非管理员不能修改登录安全设置
    Given 普通成员已登录
    When 其修改 LS_* 设置项
    Then 返回 403

  @LS-S4 @auto:e2e
  Scenario: 成功登录记录登录日志
    Given 管理员已登录成功
    When 按用户名查询登录日志
    Then 存在该用户 IsSuccess 为真的记录

  @LS-S5 @auto:e2e
  Scenario: 未读风险事件计数可查询
    When 管理员查询未读风险事件数量
    Then 返回非负整数

  @LS-S6 @auto:e2e
  Scenario: 反向代理头解析真实来源 IP
    When 携带 X-Forwarded-For 的登录成功
    Then 登录日志的 IP 为该转发地址而非代理地址

  @LS-S7 @auto:e2e
  Scenario: IP 归属地解析
    When 来自已知国内 IP 的登录成功
    Then 日志记录国家与省份（如 中国/北京市）
    And 来自海外 IP 的登录记录对应国家省份

  @LS-S26 @auto:e2e
  Scenario: 用户可查看本人登录记录
    When 用户查询本人登录记录
    Then 仅返回本人成功登录记录且不含失败原因等敏感字段
```

```gherkin
Feature: IP 爆破与撞库防护
  同一 IP 的高频失败与多账号失败触发自动封禁

  @LS-S8 @auto:e2e
  Scenario: 爆破过程中失败返回 401
    Given 同一 IP 对同一用户名连续错误密码
    When 未达封禁阈值时再次错误登录
    Then 返回 401

  @LS-S9 @auto:e2e
  Scenario: 达到失败阈值后封禁该 IP
    When 同一 IP 登录失败达到阈值
    Then 该 IP 的后续登录（即使密码正确）返回 403

  @LS-S10 @auto:e2e
  Scenario: 生成 R1 高风险事件
    When IP 爆破触发封禁
    Then 生成规则为 R1 的高风险事件

  @LS-S11 @auto:e2e
  Scenario: 封禁列表出现自动封禁记录
    When IP 爆破触发封禁
    Then 封禁列表存在该 IP 的 auto:R1 生效记录

  @LS-S12 @auto:e2e
  Scenario: 管理员解封后恢复
    When 管理员解封该记录
    Then 该 IP 可正常登录成功

  @LS-S13 @auto:e2e
  Scenario: 多用户名撞库触发封禁
    When 同一 IP 对多个不同用户名登录失败达到喷洒阈值
    Then 该 IP 后续登录返回 403

  @LS-S14 @auto:e2e
  Scenario: 生成 R2 高风险事件
    When 撞库喷洒触发封禁
    Then 生成规则为 R2 的高风险事件

  @LS-S15 @auto:e2e
  Scenario: 封禁列表出现撞库记录
    When 撞库喷洒触发封禁
    Then 封禁列表存在该 IP 的 auto:R2 生效记录

  @LS-S22 @auto:e2e
  Scenario: 管理员手动封禁 IP
    When 管理员手动封禁某 IP
    Then 该 IP 登录返回 403

  @LS-S23 @auto:e2e
  Scenario: 手动封禁解封恢复
    When 管理员解封手动封禁的 IP
    Then 该 IP 恢复正常登录
```

```gherkin
Feature: 风险登录识别与通知
  成功登录按新 IP/异地/疾速旅行/管理员新环境判定风险并通知

  @LS-S16 @auto:e2e
  Scenario: 用户从新 IP 登录成功
    Given 用户近 90 天未从该 IP 登录过
    When 其从该 IP 登录成功
    Then 登录成功且日志风险等级为中

  @LS-S17 @auto:e2e
  Scenario: 生成 R3 中风险事件
    When 新 IP 登录发生
    Then 生成规则为 R3 的中风险事件

  @LS-S18 @auto:e2e
  Scenario: 用户短间隔异地登录成功
    When 用户在疾速旅行窗口内从不同省份 IP 再次登录
    Then 登录成功

  @LS-S19 @auto:e2e
  Scenario: 生成 R5 高风险事件
    When 疾速旅行发生
    Then 生成规则为 R5 的高风险事件

  @LS-S19b @auto:e2e
  Scenario: 用户收到风险登录站内信
    When 中/高风险登录成功发生
    Then 受影响用户本人收到 Source=loginsafety 的站内信（固定逻辑，无开关）

  @LS-S19c @auto:e2e
  Scenario: 站内信使用内置模板
    When 站内信发出
    Then 标题为内置模板「{siteName} 登录安全提醒」，正文含登录 IP 与地点（界面不可定制）

  @LS-S20 @auto:e2e
  Scenario: 高风险事件推送到通知渠道
    Given 管理员已勾选通知渠道（LS_NOTIFY_CHANNEL_IDS）
    When 高风险事件发生
    Then 各渠道收到告警（webhook 渠道收 JSON 体、SMTP 渠道向其收件人发告警邮件）

  @LS-S20b @auto:e2e
  Scenario: 渠道告警使用内置模板
    When 渠道告警发出
    Then 标题为内置模板「登录风险告警：{userName}」插值结果（SMTP 渠道邮件主题为标题加渠道前缀）

  @LS-S21 @auto:e2e
  Scenario: 标记已读联动未读计数
    When 管理员批量标记风险事件已读
    Then 未读计数减少相应数量

  @LS-S24 @auto:e2e
  Scenario: 总开关关闭时仅记录不判定
    Given LS_ENABLED 为 false
    When 用户登录成功
    Then 登录日志仍记录但风险等级为无
    And 不产生新的风险事件
```

```gherkin
Feature: 风控设置
  阈值、开关与通知渠道通过系统设置管理

  @LS-S25 @auto:e2e
  Scenario: 非法设置值被拒绝
    When 管理员保存低于下限的失败阈值
    Then 返回 400
    And 开关非布尔值、回看天数越界、通知渠道 ID 非法（非正整数）同样返回 400
```
