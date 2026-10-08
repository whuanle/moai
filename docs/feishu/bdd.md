# feishu 飞书通知模块 行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 飞书应用连接管理
  Background:
    Given 已注册用户 owner 与 member，owner 创建团队 T 并拉入 member
    And owner 在团队 T 下创建了应用 A 与应用 B

  @FS-S1 @auto:e2e
  Scenario: 未登录访问被拒绝
    When 未登录用户查询团队 T 的飞书应用列表
    Then 返回 401

  @FS-S2 @auto:e2e
  Scenario: 创建请求参数校验
    When owner 以空名称或空飞书 AppID 创建飞书应用连接
    Then 返回 400 与字段级错误信息

  @FS-S3 @auto:e2e
  Scenario: 普通成员不能创建
    When member 在团队 T 创建飞书应用连接
    Then 返回 403

  @FS-S4 @auto:e2e
  Scenario: 管理员创建连接
    When owner 以名称、飞书 AppID 与 AppSecret 创建连接
    Then 创建成功并返回连接 id
    And 团队成员查询列表可见该连接，包含名称、AppID、接入域名与绑定信息
    And 列表不回显 AppSecret
    And 假凭证连接的在线状态为 false

  @FS-S5 @auto:e2e
  Scenario: 唯一性校验
    When owner 再次以相同飞书 AppID 创建连接
    Then 返回 409
    When owner 再次以相同名称创建连接
    Then 返回 409

  @FS-S12 @auto:e2e
  Scenario: 更新连接
    When owner 修改连接名称并禁用
    Then 列表中名称已更新且禁用状态生效
    And 禁用连接的在线状态为 false
    And 更新时 AppSecret 留空则保持原值

Feature: 应用渠道接入（创建即绑定）
  应用渠道不提供独立的绑定/解绑操作：接入 = 创建连接即绑定到目标应用（单次调用、同一事务）；移除 = 删除连接（级联解绑、彻底移除）。

  @FS-S6 @auto:e2e
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

  @FS-S7 @auto:e2e
  Scenario: 创建即绑定成功
    When owner 创建连接并携带应用 A 的渠道信息
    Then 创建成功，连接与绑定同一事务生效
    And 列表回显该连接的绑定渠道类型、渠道 id 与绑定时间

  @FS-S8 @auto:e2e
  Scenario: 同一飞书应用不可重复接入
    Given 连接已创建并绑定应用 A
    When owner 以相同飞书 AppID 再次创建连接（带任意渠道）
    Then 返回 409
    And 应用渠道互斥由此结构性保证：新接入必然是全新连接，不存在把已有连接改绑到其它应用的路径

  @FS-S9 @auto:e2e
  Scenario: 不同飞书应用可分别接入不同应用
    Given 连接一已接入应用 A，owner 又创建了连接二并携带应用 B 的渠道信息
    Then 创建即绑定成功
    And 列表回显连接二绑定应用 B

  @FS-S10 @auto:e2e
  Scenario: 普通成员不能创建渠道接入
    When member 创建连接并携带渠道信息
    Then 返回 403

  @FS-S11 @auto:e2e
  Scenario: 绑定/解绑 HTTP 端点已下线
    When owner 对连接发起 POST /api/feishu_app/{id}/bind 或 /unbind
    Then 返回 405（路由不再提供该动作）
    And 绑定仅随创建发生、解绑仅随删除发生；绑定/解绑命令保留给知识库外部源等业务模块内部复用

  @FS-S13 @auto:e2e
  Scenario: 删除即彻底移除
    Given 连接二已接入应用 B
    When owner 删除连接二
    Then 删除成功，再次删除返回 404
    And 以相同飞书 AppID 重新创建并接入应用 B 成功（不保留可复用的连接）

  @FS-S14 @auto:e2e
  Scenario: 删除带绑定的连接同时解除绑定
    Given 连接一已接入应用 A
    When owner 删除连接一
    Then 删除成功，列表不再包含该连接
    And 应用 A 的渠道可立即被新连接接入

Feature: 事件接收与转发
  @FS-S15 @manual
  Scenario: 启动建连
    Given 数据库中存在若干未禁用的飞书应用连接
    When 平台进程启动
    Then 每个连接各建立一条长连接
    And 单个连接初始化失败只记日志，不影响其它连接与宿主启动

  @FS-S16 @manual
  Scenario: 事件按绑定转发
    Given 连接已绑定应用渠道
    When 飞书用户触发该飞书应用订阅的事件
    Then 事件被立即确认
    And 事件消息（含事件原文与渠道信息）投递给应用渠道的事件处理器
    And 同一 event_id 的重复投递只处理一次

  @FS-S17 @manual
  Scenario: 未绑定或渠道不可用的事件丢弃
    Given 连接未绑定任何渠道，或绑定的应用已删除
    When 飞书事件到达
    Then 事件被确认后丢弃并记日志，不投递给任何业务模块

  @FS-S18 @manual
  Scenario: 禁用后停止消费
    Given 连接已被禁用或删除
    When 飞书事件到达
    Then 事件被丢弃

Feature: 群聊/私聊消息回复
  @FS-S19 @manual
  Scenario: 文本消息得到应用回复
    Given 连接已绑定到已发布且配置了对话模型的内部 Agent 应用
    And 飞书应用的机器人能力与 im:message 权限已开通，事件已订阅 im.message.receive_v1
    When 飞书用户在私聊或群聊中向机器人发送文本消息
    Then 平台按「飞书应用 + 会话」定位应用 Agent 会话（不存在则创建）
    And 运行应用 Agent 生成回复并原样发回该会话

  @FS-S20 @manual
  Scenario: 非目标消息忽略
    When 飞书用户发送图片等非文本消息，或事件来自机器人账号
    Then 平台不运行应用 Agent 也不回复

  @FS-S21 @manual
  Scenario: 应用不可用时静默忽略
    Given 绑定的应用未发布或已被禁用
    When 飞书用户发送文本消息
    Then 平台不运行应用 Agent 也不回复，仅记日志

  @FS-S22 @manual
  Scenario: 配置类失败对用户可见
    Given 绑定的应用尚未配置对话模型
    When 飞书用户发送文本消息
    Then 平台把失败原因作为文本回复发给该会话

  @FS-S23 @manual
  Scenario: 会话上下文连续
    Given 同一飞书用户在同一会话中已连续提问
    When 再次发送文本消息
    Then 应用 Agent 带上该会话历史生成回复
    And 会话消息与用量与站内对话一致落库

Feature: 应用工作台外部渠道页
  # @FS-S26（绑定已有连接与解除绑定）已随 2026-10-08 外部渠道简化下线，编号永久退役不复用。

  @FS-S24 @auto:vitest
  Scenario: 外部渠道菜单可见性
    Given 团队管理员打开内部 Agent 应用的应用工作台
    Then 左侧菜单出现「外部渠道」入口
    But 普通成员、外部应用与流程应用的工作台不出现该入口

  @FS-S25 @auto:vitest
  Scenario: 从应用页接入飞书应用
    Given 管理员打开内部 Agent 应用的外部渠道页
    When 填写连接名称、飞书 AppID 与 AppSecret 后提交接入
    Then 平台创建飞书应用连接即绑定到当前应用（单次调用，创建与绑定原子生效）
    And 渠道表格只回显接入到当前应用的连接（名称、AppID、状态、接入时间）
    And 页面不提供「绑定已有连接」与「解绑」入口

  @FS-S27 @auto:vitest
  Scenario: 渠道连接管理操作
    Given 外部渠道页存在接入到当前应用的飞书连接
    When 管理员停用或删除该连接
    Then 均需二次确认后生效（删除即彻底移除，不保留可复用连接）
    When 管理员启用已停用的连接
    Then 直接生效无需确认
    But 普通成员进入页面时不可见任何操作入口

  @FS-S28 @auto:vitest
  Scenario: 未发布应用提示
    Given 内部 Agent 应用尚未发布
    When 管理员打开外部渠道页
    Then 页面提示飞书消息不会被处理，需先发布应用
