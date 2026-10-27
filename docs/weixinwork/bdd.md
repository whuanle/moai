# weixinwork 企业微信渠道模块 行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 企业微信应用连接管理
  Background:
    Given 已注册用户 owner 与 member，owner 创建团队 T 并拉入 member
    And owner 在团队 T 下创建了应用 A 与应用 B

  @WX-S1 @auto:e2e
  Scenario: 未登录访问被拒绝
    When 未登录用户查询团队 T 的企业微信应用列表
    Then 返回 401

  @WX-S2 @auto:e2e
  Scenario: 创建请求参数校验
    When owner 以空名称、空企业 ID、空 Secret、AgentId 非正数、空回调 Token、非 43 位 EncodingAESKey 或非 http(s) API 地址创建连接
    Then 返回 400 与字段级错误信息

  @WX-S3 @auto:e2e
  Scenario: 普通成员不能创建
    When member 在团队 T 创建企业微信应用连接
    Then 返回 403

  @WX-S4 @auto:e2e
  Scenario: 管理员创建连接
    When owner 以名称、企业 ID、Secret、AgentId、回调 Token 与 EncodingAESKey 创建连接
    Then 创建成功并返回连接 id
    And 团队成员查询列表可见该连接，包含企业 ID、AgentId 与绑定信息
    And 列表不回显 Secret、回调 Token 与 EncodingAESKey
    And 启用的连接在线状态为 true（回调模式下连接启用即可接收推送）

  @WX-S5 @auto:e2e
  Scenario: 唯一性校验
    When owner 再次以相同企业 ID + AgentId 创建连接
    Then 返回 409
    When owner 再次以相同名称创建连接
    Then 返回 409

  @WX-S6 @auto:e2e
  Scenario: 其它团队用户视角不存在
    When 其它团队的 Admin 对团队 T 的连接发起更新或删除
    Then 返回 404

  @WX-S7 @auto:e2e
  Scenario: 更新连接
    When owner 修改连接名称并禁用
    Then 列表中名称已更新且禁用状态生效
    And 禁用连接的在线状态为 false
    And 更新时 Secret/回调 Token/EncodingAESKey 留空则保持原值
    And member 更新连接返回 403
    And owner 编辑连接修改企业 ID 与 AgentId 后列表回显新值（填错凭证无需删除重建）
    But 修改为其它连接已占用的企业 ID + AgentId 返回 409

  @WX-S8 @auto:e2e
  Scenario: 删除即彻底移除
    Given 已存在接入应用 B 的连接与接入应用 A 的连接
    When owner 删除接入应用 B 的连接
    Then 删除成功，再次删除返回 404
    And 以相同企业 ID + AgentId 重新创建并接入应用 B 成功（不保留可复用的连接）
    When owner 删除接入应用 A 的连接
    Then 删除成功，列表不再包含该连接
    And 应用 A 的渠道可立即被新连接接入

Feature: 应用渠道接入（创建即绑定）
  接入 = 创建连接即绑定到目标应用（单次调用、同一事务）；移除 = 删除连接（级联解绑、彻底移除）。

  @WX-S9 @auto:e2e
  Scenario: 创建即绑定的渠道校验
    When owner 创建连接并绑定到不存在的应用渠道
    Then 返回 404
    When owner 创建连接并绑定到其它团队的应用渠道
    Then 返回 403
    When owner 以格式错误的渠道 id 创建即绑定
    Then 返回 400
    When owner 以非法或不支持的渠道类型创建即绑定
    Then 返回 400
    When owner 只传渠道 id 不传渠道类型
    Then 返回 400

  @WX-S10 @auto:e2e
  Scenario: 创建即绑定成功
    When owner 创建连接并携带应用 A 的渠道信息
    Then 创建成功，连接与绑定同一事务生效
    And 列表回显该连接的绑定渠道类型、渠道 id 与绑定时间

  @WX-S11 @auto:e2e
  Scenario: 同一企业微信应用不可重复接入
    Given 连接已创建并绑定应用 A
    When owner 以相同企业 ID + AgentId 再次创建连接（带任意渠道）
    Then 返回 409
    And 应用渠道互斥由此结构性保证：新接入必然是全新连接，不存在把已有连接改绑到其它应用的路径

  @WX-S12 @auto:e2e
  Scenario: 不同企业微信应用可分别接入不同应用
    Given 连接一已接入应用 A，owner 又创建了连接二并携带应用 B 的渠道信息
    Then 创建即绑定成功
    And 列表回显连接二绑定应用 B

  @WX-S13 @auto:e2e
  Scenario: 普通成员不能创建渠道接入
    When member 创建连接并携带渠道信息
    Then 返回 403

Feature: 回调接收与验证
  企业微信自建应用以 HTTP 回调模式接收消息：管理端「接收消息 → 设置 API 接收」配置回调 URL、Token 与 EncodingAESKey，
  回调地址为 {平台地址}/api/weixinwork_app/{连接 id}/callback。

  @WX-S14 @auto:e2e
  Scenario: URL 有效性验证
    Given 连接的回调配置 Token 与 EncodingAESKey 已保存
    When 企业微信以正确签名请求验证（GET echostr）
    Then 平台校验签名并解密 echostr，原样回显明文

  @WX-S15 @auto:e2e
  Scenario: 验证签名不符被拒绝
    When 企业微信以错误签名请求验证
    Then 返回 400

  @WX-S16 @auto:e2e
  Scenario: 回调指向不存在的连接
    When 回调请求的连接 id 不存在
    Then 返回 404

  @WX-S22 @auto:e2e
  Scenario: 推送签名不符被拒绝
    When 推送报文的签名与连接配置 Token 不符
    Then 返回 400 且不处理消息

  @WX-S23 @auto:e2e
  Scenario: 报文归属企业与连接不符被拒绝
    When 推送报文解密后的 receiveid 与连接配置的企业 ID 不一致
    Then 返回 400 且不处理消息

  @WX-S24 @auto:e2e
  Scenario: 推送到不存在的连接
    When 推送到达时连接已被删除
    Then 返回 404

Feature: 单聊/群聊消息回复（回调推送全链路）
  @WX-S17 @auto:e2e
  Scenario: 单聊文本消息得到应用回复
    Given 连接已绑定到已发布且配置了对话模型的内部 Agent 应用
    When 企业微信成员单聊应用发送文本消息（加密推送到达回调地址）
    Then 平台校验签名解密后立即应答 success（业务异步处理）
    And 平台按「企业微信应用 + 成员 userid」定位应用 Agent 会话（不存在则创建）
    And 运行应用 Agent 生成回复并经应用消息接口（message/send）发给该成员

  @WX-S18 @auto:e2e
  Scenario: 群聊 @应用消息回复到群
    Given 连接已绑定到已发布且配置了对话模型的内部 Agent 应用
    When 企业微信成员在群里 @应用 发送文本消息（推送携带 ChatId）
    Then 平台按「企业微信应用 + 群聊 id」定位会话并运行应用 Agent
    And 回复经应用推送群聊接口（appchat/send）发回该群

  @WX-S19 @auto:e2e
  Scenario: 同一推送重复投递去重
    When 同一 MsgId 的推送重复到达（应答超时重发）
    Then 只处理一次，只回复一次

  @WX-S20 @auto:e2e
  Scenario: 会话上下文连续
    Given 同一成员已连续提问
    When 再次发送文本消息
    Then 应用 Agent 带上该会话历史生成回复

  @WX-S21 @auto:e2e
  Scenario: 非文本消息忽略
    When 企业微信成员发送图片等非文本消息
    Then 平台受理推送但不运行应用 Agent 也不回复

  @WX-S25 @auto:e2e
  Scenario: 禁用连接停止处理
    Given 连接已被禁用
    When 推送到达
    Then 平台受理（应答 success 避免重发）但不转发不回复

  @WX-S26 @auto:e2e
  Scenario: 应用不可用时静默忽略
    Given 绑定的应用未发布或已被禁用
    When 企业微信成员发送文本消息
    Then 平台不运行应用 Agent 也不回复，仅记日志

Feature: 应用工作台外部渠道页（企业微信分区）
  @WX-S27 @auto:vitest
  Scenario: 企业微信分区展示与过滤
    Given 管理员打开内部 Agent 应用的外部渠道页
    Then 页面出现「企业微信渠道」卡片，「接入企业微信应用」与「刷新」按钮位于卡片标题右侧
    And 企业微信接入指引默认收缩于卡片内，点击展开后按步骤逐条分行展示
    And 渠道表格只回显接入到当前应用的连接（名称、企业 ID/AgentId、状态、接入时间）
    And 应用未发布时提示企业微信消息不会被处理

  @WX-S27 @auto:vitest
  Scenario: 从应用页接入企业微信应用
    When 管理员填写连接名称、企业 ID、AgentId、Secret、回调 Token 与 EncodingAESKey 后提交接入
    Then 平台创建企业微信应用连接即绑定到当前应用（单次调用，创建与绑定原子生效）

  @WX-S27 @auto:vitest
  Scenario: 编辑回显与回调地址
    Given 企业微信分区存在接入到当前应用的连接
    When 管理员打开编辑弹窗
    Then 表单回显名称/企业 ID/AgentId 并展示本连接的回调地址
    But 密钥类字段留空表示保持不变，不改变当前启停状态

  @WX-S27 @auto:vitest
  Scenario: 企业微信渠道连接管理操作
    Given 企业微信分区存在接入到当前应用的连接
    When 管理员停用或删除该连接
    Then 均需二次确认后生效
    When 管理员启用已停用的连接
    Then 直接生效无需确认
    But 普通成员进入页面时不可见任何操作入口
```
