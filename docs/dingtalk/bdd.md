# dingtalk 钉钉渠道模块 行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 钉钉应用连接管理
  Background:
    Given 已注册用户 owner 与 member，owner 创建团队 T 并拉入 member
    And owner 在团队 T 下创建了应用 A 与应用 B

  @DT-S1 @auto:e2e
  Scenario: 未登录访问被拒绝
    When 未登录用户查询团队 T 的钉钉应用列表
    Then 返回 401

  @DT-S2 @auto:e2e
  Scenario: 创建请求参数校验
    When owner 以空名称、空钉钉 AppKey、空 AppSecret 或非法团队 id 创建钉钉应用连接
    Then 返回 400 与字段级错误信息

  @DT-S3 @auto:e2e
  Scenario: 普通成员不能创建
    When member 在团队 T 创建钉钉应用连接
    Then 返回 403

  @DT-S4 @auto:e2e
  Scenario: 管理员创建连接
    When owner 以名称、钉钉 AppKey 与 AppSecret 创建连接
    Then 创建成功并返回连接 id
    And 团队成员查询列表可见该连接，包含名称、AppKey 与绑定信息
    And 列表不回显 AppSecret
    And 假凭证连接的在线状态为 false

  @DT-S5 @auto:e2e
  Scenario: 唯一性校验
    When owner 再次以相同钉钉 AppKey 创建连接
    Then 返回 409
    When owner 再次以相同名称创建连接
    Then 返回 409

  @DT-S6 @auto:e2e
  Scenario: 其它团队用户视角不存在
    When 其它团队的 Admin 对团队 T 的连接发起更新或删除
    Then 返回 404

  @DT-S12 @auto:e2e
  Scenario: 更新连接
    When owner 修改连接名称并禁用
    Then 列表中名称已更新且禁用状态生效
    And 禁用连接的在线状态为 false
    And 更新时 AppSecret 留空则保持原值
    And member 更新连接返回 403
    And owner 编辑连接修改钉钉 AppKey 后列表回显新 AppKey（填错凭证无需删除重建）
    But 修改为其它连接已占用的 AppKey 返回 409
    And 更新时 AppKey 留空则保持原值

Feature: 应用渠道接入（创建即绑定）
  接入 = 创建连接即绑定到目标应用（单次调用、同一事务）；移除 = 删除连接（级联解绑、彻底移除）。

  @DT-S7 @auto:e2e
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

  @DT-S8 @auto:e2e
  Scenario: 创建即绑定成功
    When owner 创建连接并携带应用 A 的渠道信息
    Then 创建成功，连接与绑定同一事务生效
    And 列表回显该连接的绑定渠道类型、渠道 id 与绑定时间

  @DT-S9 @auto:e2e
  Scenario: 同一钉钉应用不可重复接入
    Given 连接已创建并绑定应用 A
    When owner 以相同钉钉 AppKey 再次创建连接（带任意渠道）
    Then 返回 409
    And 应用渠道互斥由此结构性保证：新接入必然是全新连接，不存在把已有连接改绑到其它应用的路径

  @DT-S10 @auto:e2e
  Scenario: 不同钉钉应用可分别接入不同应用
    Given 连接一已接入应用 A，owner 又创建了连接二并携带应用 B 的渠道信息
    Then 创建即绑定成功
    And 列表回显连接二绑定应用 B

  @DT-S11 @auto:e2e
  Scenario: 普通成员不能创建渠道接入
    When member 创建连接并携带渠道信息
    Then 返回 403

  @DT-S13 @auto:e2e
  Scenario: 删除即彻底移除
    Given 连接二已接入应用 B
    When owner 删除连接二
    Then 删除成功，再次删除返回 404
    And 以相同钉钉 AppKey 重新创建并接入应用 B 成功（不保留可复用的连接）

  @DT-S14 @auto:e2e
  Scenario: 删除带绑定的连接同时解除绑定
    Given 连接一已接入应用 A
    When owner 删除连接一
    Then 删除成功，列表不再包含该连接
    And 应用 A 的渠道可立即被新连接接入

Feature: Stream 推送接收与转发
  @DT-S15 @manual
  Scenario: 启动建连
    Given 数据库中存在若干未禁用的钉钉应用连接
    When 平台进程启动
    Then 每个连接各建立一条 Stream 长连接（订阅机器人消息回调 Topic）
    And 单个连接初始化失败只记日志，不影响其它连接与宿主启动

  @DT-S16 @manual
  Scenario: 推送确认与按绑定转发
    Given 连接已绑定应用渠道
    When 钉钉推送机器人消息到该连接
    Then 平台在 5 秒内回复 ack（避免钉钉重发）
    And 推送消息（含原文与会话 webhook）投递给应用渠道的事件处理器
    And 同一 message_id 的重复投递只处理一次

  @DT-S17 @manual
  Scenario: 未绑定或渠道不可用的推送丢弃
    Given 连接未绑定任何渠道，或绑定的应用已删除
    When 钉钉推送到达
    Then 推送被确认后丢弃并记日志，不投递给任何业务模块

  @DT-S18 @manual
  Scenario: 禁用后停止消费
    Given 连接已被禁用或删除
    When 钉钉推送到达
    Then 推送被丢弃

Feature: 单聊/群聊机器人消息回复
  @DT-S19 @manual
  Scenario: 文本消息得到应用回复
    Given 连接已绑定到已发布且配置了对话模型的内部 Agent 应用
    And 钉钉应用已添加机器人能力、消息接收模式为 Stream 模式并已发布
    When 钉钉用户单聊机器人或在群里 @机器人 发送文本消息
    Then 平台按「钉钉应用 + 会话」定位应用 Agent 会话（不存在则创建）
    And 运行应用 Agent 生成回复并经会话 webhook 发回该会话

  @DT-S20 @manual
  Scenario: 非文本消息忽略
    When 钉钉用户发送图片等非文本消息
    Then 平台不运行应用 Agent 也不回复

  @DT-S21 @manual
  Scenario: 应用不可用时静默忽略
    Given 绑定的应用未发布或已被禁用
    When 钉钉用户发送文本消息
    Then 平台不运行应用 Agent 也不回复，仅记日志

  @DT-S22 @manual
  Scenario: 配置类失败对用户可见
    Given 绑定的应用尚未配置对话模型
    When 钉钉用户发送文本消息
    Then 平台把失败原因作为文本回复发给该会话

  @DT-S23 @manual
  Scenario: 会话上下文连续
    Given 同一钉钉会话中已连续提问
    When 再次发送文本消息
    Then 应用 Agent 带上该会话历史生成回复
    And 会话消息与用量与站内对话一致落库

Feature: 渠道用户识别与会话归属
  发送者若已通过第三方登录绑定钉钉（user_oauth_connection 记录 unionId，推送体 senderId 即 unionId），渠道对话自动归属其 MoAI 账号：会话在网页端应用会话列表可见、可续聊；未绑定/歧义/停用一律保持匿名（现状不变）。

  @DT-S31 @manual
  Scenario: 已绑定第三方登录的发送者会话自动归属
    Given 发送者已用钉钉第三方登录绑定 MoAI 账号（绑定记录含 unionId）
    When 该用户向机器人发送文本消息
    Then 新会话归属到该用户（归属用户、用户类型为站内用户）
    And 该用户在网页端应用会话列表可见该会话并可继续对话

  @DT-S32 @manual
  Scenario: 未绑定或无法唯一定位时保持匿名
    Given 发送者未绑定第三方登录，或绑定标识命中多个不同账号，或命中的账号已被停用
    When 发送文本消息
    Then 会话保持匿名（不归属任何用户），任何站内用户的会话列表都不可见

  @DT-S33 @manual
  Scenario: 群聊中已绑定成员各自独立会话
    Given 同一群聊中多个成员各自绑定过第三方登录
    When 各自 @机器人 发消息
    Then 每个成员拥有独立会话（互不串上下文），归属各自的账号
    And 未绑定成员仍共享匿名会话

  @DT-S34 @manual
  Scenario: 流程应用经渠道对话
    Given 连接已绑定到已发布的内部流程应用
    When 钉钉用户向机器人发送文本消息
    Then 平台运行流程应用并把结束节点回复文本发回该会话
    And 会话归属规则与 Agent 应用一致

Feature: 应用工作台外部渠道页（钉钉分区）
  @DT-S24 @auto:vitest
  Scenario: 钉钉分区展示与过滤
    Given 管理员打开内部 Agent 应用的外部渠道页
    Then 页面出现「钉钉渠道」卡片，「接入钉钉应用」与「刷新」按钮位于卡片标题右侧
    And 钉钉接入指引默认收缩于卡片内，点击展开后按步骤逐条分行展示
    And 渠道表格只回显接入到当前应用的钉钉连接（名称、AppKey、状态、接入时间）
    And 应用未发布时提示钉钉消息不会被处理

  @DT-S25 @auto:vitest
  Scenario: 从应用页接入钉钉应用
    When 管理员填写连接名称、钉钉 AppKey 与 AppSecret 后提交接入
    Then 平台创建钉钉应用连接即绑定到当前应用（单次调用，创建与绑定原子生效）

  @DT-S26 @auto:vitest
  Scenario: 钉钉渠道连接管理操作
    Given 钉钉分区存在接入到当前应用的连接
    When 管理员停用或删除该连接
    Then 均需二次确认后生效
    When 管理员启用已停用的连接
    Then 直接生效无需确认
    But 普通成员进入页面时不可见任何操作入口
```

```gherkin
Feature: 应用列表外部渠道入口（钉钉机器人单聊）
  钉钉拉起机器人单聊的链接需要机器人的 dingtalk id（回调报文 chatbotUserId），平台在收到首条机器人消息后自动回填；应用列表据此在卡片右上角露出钉钉入口。

  @DT-S27 @auto:e2e
  Scenario: 应用列表钉钉渠道入口
    Given 钉钉连接已绑定到团队应用且连接启用
    Then 应用列表项即携带 externalChannels：channelType=dingtalk 与 jumprobot 单聊地址
    And 机器人 dingtalk id 尚未回填（未收到过机器人消息）时 chatUrl 用 AppKey（robotCode）兜底
    When 机器人 dingtalk id 已回填
    Then chatUrl 自动切换为 dingtalk://dingtalkclient/action/jumprobot?dingtalkid={chatbotUserId}

  @DT-S28 @manual
  Scenario: 首条机器人消息回填 dingtalk id
    Given 钉钉连接凭证有效且已建 Stream 长连接
    When 任意钉钉用户首次向该机器人发送消息
    Then 平台从推送报文提取 chatbotUserId 回填到 dingtalk_app（已一致时不重复写库）
    And 后续应用列表即可生成该机器人的单聊跳转链接
```

  @DT-S29 @manual
  Scenario: 多实例部署按应用抢占负载均衡与自动接管
    Given 平台以两个实例部署且 Redis 可用
    When 两个实例同时启动
    Then 每个钉钉应用由其中一个实例独占建立长连接，应用分散到不同实例（负载均衡）
    When 持有部分应用的实例下线
    Then 它持有的应用租约到期后由存活实例争抢接管（约 35 秒内）
    And 接管期间平台持续可用，消息由接管实例处理

  @DT-S30 @manual
  Scenario: 多实例下跨实例的连接变更自动同步
    Given 平台以两个实例部署，钉钉应用的连接由其中部分实例持有
    When 管理员经由任一实例的接口接入、停用或删除钉钉连接
    Then 持有对应连接的实例在一个对账周期内按数据库建连/断连，无需重启
    And 持有数超过公平份额的实例自动释放多余应用由其它实例争抢（负载再均衡）
