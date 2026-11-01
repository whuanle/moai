# 通知中心（Notification，NT）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/notification-e2e.mjs](../../local-dev/notification-e2e.mjs)

Feature: 通知渠道管理（仅管理员）

@NT-S1 渠道接口门禁
@auto:e2e
  Scenario: 未登录与非管理员访问被拒
    When 未登录用户请求渠道列表
    Then 返回 401
    When 非管理员请求渠道列表、创建渠道或发送广播
    Then 均返回 403

@NT-S2 创建渠道与配置校验
@auto:e2e
  Scenario: 按类型创建渠道
    When 管理员按飞书/钉钉/企微/Telegram/通用 webhook/SMTP/站内推送七类提交合法配置
    Then 创建成功并返回渠道 id
  Scenario: 非法请求被拒
    When 名称与既有渠道重复
    Then 返回 400
    When webhook 地址不是合法 http(s) 地址、配置不是有效 JSON、Telegram 缺少 chatId、
      And SMTP 收件人含无效邮箱、站内推送未配置任何收件人
    Then 均返回 400

@NT-S3 渠道列表与搜索
@auto:e2e
  Scenario: 列表回显与过滤
    When 管理员查询渠道列表并按本次运行标识搜索
    Then 仅返回本批渠道，配置原样回显，创建/更新人名回填

@NT-S4 更新渠道约束
@auto:e2e
  Scenario: 更新渠道
    When 管理员改名并更新配置
    Then 列表回显新名称
    When 把渠道更新为另一种类型
    Then 返回 400（类型不可变更）
    When 更新为既有渠道名称
    Then 返回 400
    When 更新或删除不存在的渠道
    Then 返回 404

Feature: 渠道测试（同步试发，不落发送记录）

@NT-S5 七类渠道测试成功
@auto:e2e
  Scenario: 各渠道收到测试通知
    When 管理员对飞书渠道（按 id）执行测试
    Then 返回成功，桩收到 msg_type=text、timestamp 与验签通过的 sign
    When 用钉钉配置（未保存）试发
    Then 返回成功，请求 URL 附 timestamp/sign 且验签通过
    When 测试企业微信、Telegram、通用 webhook（默认结构与自定义头）、模板渲染
    Then 均返回成功且桩按各渠道协议收到正确载荷

@NT-S6 渠道测试失败回显
@auto:e2e
  Scenario: 外部接口异常原样回显
    When 飞书机器人返回 code!=0、webhook 返回 HTTP 500、Telegram 返回 ok=false
    Then 测试返回成功=false 且失败原因包含对方错误信息
    When 对不存在的渠道 id 执行测试
    Then 返回 404

@NT-S7 SMTP 渠道联动系统设置
@auto:e2e
  Scenario: SMTP 未启用时测试失败
    When 系统设置未启用 SMTP
    Then 测试返回成功=false，原因提示先配置 SMTP
  Scenario: 启用后测试成功
    When 管理员把 SMTP 设置指向本地桩并启用
    Then 测试返回成功，桩捕获到收件人与主题

@NT-S8 站内推送收件人解析
@auto:e2e
  Scenario: 推送全部管理员
    When 渠道配置开启「推送全部管理员」
    Then 测试返回成功，admin 用户可在站内信中看到测试标题

Feature: 广播投递与发送记录

@NT-S9 广播投递全部启用渠道
@auto:e2e
  Scenario: 异步落发送记录
    When 管理员发送广播
    Then 返回本次投递的启用渠道数量
    And 每个启用渠道各产生一条发送记录，最终均为成功（站内/SMTP/webhook/机器人）

@NT-S10 失败渠道终态与原因
@auto:e2e
  Scenario: 失败渠道记录原因
    When 广播包含返回 500 的 webhook 与签名失败的飞书渠道
    Then 对应记录终态为失败且失败原因非空
    And 记录快照包含渠道类型/级别/耗时

@NT-S11 发送记录过滤
@auto:e2e
  Scenario: 按渠道与状态过滤
    When 按渠道 id 过滤发送记录
    Then 仅返回该渠道的记录
    When 按失败状态过滤
    Then 仅返回失败记录

@NT-S12 停用渠道不投递
@auto:e2e
  Scenario: 停用后广播
    When 停用一个渠道后再次广播
    Then 返回的投递渠道数减一，该渠道无新发送记录

@NT-S13 站内推送渠道送达站内信
@auto:e2e
  Scenario: 广播经站内渠道触达管理员
    When 广播包含站内推送渠道
    Then admin 用户站内信中收到该广播标题

@NT-S14 删除渠道保留历史
@auto:e2e
  Scenario: 删除后历史可查
    When 删除渠道
    Then 渠道列表不再包含它，历史发送记录仍保留（渠道名快照可读）

@NT-S15 广播参数校验
@auto:e2e
  Scenario: 空标题被拒
    When 广播标题为空
    Then 返回 400
