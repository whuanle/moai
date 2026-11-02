# 站内信与事件推送（Message，MSG）行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@MSG-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/message-e2e.mjs](../../local-dev/message-e2e.mjs)、[tests/MoAI.Message.Tests](../../tests/MoAI.Message.Tests)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。

```gherkin
Feature: Hub 鉴权与连接
  平台事件推送 Hub /hub/moai 仅认证用户可连接，JWT 经 access_token 查询参数传递

  @MSG-S1 @auto:e2e
  Scenario: 未认证连接 Hub 被拒绝
    When 客户端不携带 token 请求 negotiate
    Then 返回 401

  @MSG-S2 @auto:e2e
  Scenario: 携带 access_token 连接成功
    Given 已登录用户
    When 以 access_token 完成协商与握手
    Then 连接建立并确认 json 协议

Feature: 站内信读侧接口（个人数据，仅本人）

  @MSG-S3 @auto:e2e
  Scenario: 未认证调用消息接口被拒绝
    When 未携带 token 查询未读数
    Then 返回 401

  @MSG-S4 @auto:e2e
  Scenario: 新用户空态
    Given 刚注册且从未收到消息的用户
    When 查询未读数与消息列表
    Then 未读数为 0 且列表总数为 0

  @MSG-S5 @auto:e2e
  Scenario: 参数校验
    When 标记已读或删除时提交空 id 集合
    Then 返回 400
    When 列表查询提交每页数量 500
    Then 返回 400

  @MSG-S9 @auto:e2e
  Scenario: 空库幂等
    Given 用户名下无消息
    When 全部标记已读或删除不存在的消息 id
    Then 均返回 200 且不产生副作用

Feature: MQ 异步事件推送（只推事件不推数据）

  @MSG-S6 @auto:e2e
  Scenario: 事件经 MQ 送达在线用户
    Given 用户 A 已连接 Hub
    When 向 message.push 队列投递针对 A 的事件
    Then A 的连接收到 onEvent 事件且事件名一致
    And 事件负载不含业务正文，仅有事件名与标识字段

  @MSG-S7 @auto:e2e
  Scenario: 事件只送达目标用户
    Given 用户 A 与用户 B 均在线
    When 投递仅针对 A 的事件
    Then A 收到事件
    And B 在窗口期内未收到该事件

  @MSG-S8 @auto:e2e
  Scenario: 同用户多端连接均收到
    Given 用户 A 同时保持两条连接
    When 投递针对 A 的事件
    Then 两条连接均收到该事件

Feature: 站内信领域服务（落库与解析）

  @MSG-S10 @auto:e2e
  Scenario: 发送给指定用户逐人落库并触发事件
    When 领域服务向用户 1/2/3 发送站内信
    Then 每个收件人各一行且未读
    And 事件推送服务收到一次 message.new 且目标为三个用户

  @MSG-S11 @auto:e2e
  Scenario: 团队发送的边界
    When 向不存在的团队发送站内信
    Then 返回 404 业务异常
    When 向存在且有两名成员的团队发送站内信
    Then 两名成员各落一行

  @MSG-S12 @auto:e2e
  Scenario: 全员发送跳过禁用用户
    Given 用户 b 已被禁用
    When 全员广播站内信
    Then 仅未禁用用户落行

  @MSG-S13 @auto:e2e
  Scenario: 已读与删除按收件人隔离
    When 用户 1 标记自己的消息已读
    Then 仅该行变为已读，他人消息不受影响
    When 用户 1 删除自己的消息
    Then 该行软删，其列表与未读数不再可见

Feature: 前端接入（SignalR 客户端 + 角标 + 通知 + 消息中心页）

  @MSG-S14 @auto:vitest
  Scenario: 侧边栏未读角标实时刷新
    Given 已登录用户停留在任意页面
    When 登录加载时拉取未读数
    Then 左下角铃铛显示未读角标
    When 收到 message.new 事件
    Then 角标数随服务端未读数实时刷新

  @MSG-S15 @auto:vitest
  Scenario: 新消息右上角通知
    Given 已登录用户建立了 /hub/moai 连接
    When 收到 message.new 事件
    Then 右上角 Notification 弹出并带最新消息标题与正文摘要
    And 通知持续悬浮不自动关闭，点击可跳转消息中心
    When 刷新页面
    Then 通知消失（内存态）

  @MSG-S16 @auto:vitest
  Scenario: 个人中心二级菜单
    When 用户打开 /account/messages 或 /account/settings
    Then 页面左侧显示二级菜单（消息中心 / 账号设置各分区）并正确高亮
    When 访问旧地址 /account 或 /account/settings
    Then 平滑重定向到对应分区

  @MSG-S17 @auto:vitest
  Scenario: 消息中心列表操作
    Given 用户存在已读与未读消息
    When 切换「全部 / 未读」过滤
    Then 列表按已读状态过滤并分页展示
    When 点击未读行或在行上点「标记已读」
    Then 该行变为已读且角标减一
    When 点「删除」并在气泡确认
    Then 该行软删不再显示
  # 变更记录：原「全部已读」按钮已随批量操作上线移除（见 @MSG-S19），read-all 接口保留

  @MSG-S18 @auto:e2e
  Scenario: 未读数接口按登录用户统计
    When 用户查询 /api/message/unread-count
    Then 返回数量恰好等于本人未读行数（收件人隔离，不因命令构造方式丢失登录态）

  @MSG-S19 @auto:vitest
  Scenario: 消息中心批量操作与固定视口布局
    Given 用户存在已读与未读消息
    When 勾选行复选框或点「全选」
    Then 卡片工具栏回显已选数量，页头右上角「批量已读/批量删除」按选中内容启用（批量已读仅选中含未读时可用）
    When 点右上角「批量已读」
    Then 选中消息按 id 集合标记已读（复用 read 接口，仅本人未读生效）
    When 点右上角「批量删除」并在气泡确认
    Then 仅选中的**已读**消息按 id 集合软删，选中的未读消息自动忽略不删除
    And 气泡按实际可删的已读条数提示（含未读时附「未读 N 条将自动忽略」），仅勾选未读时按钮禁用；删除成功提示含忽略条数
    When 翻页或切换「全部 / 未读」过滤
    Then 选择清空、批量按钮禁用
    And 默认每页 20 条；消息超出视口时列表在卡片内滚动（无页面滚动条），分页固定于视口底部
    And 页面头部不再渲染「消息中心」大标题（导航语义由左侧二级菜单承担），右上角操作区 = 过滤 Segmented + 批量已读 + 批量删除
```
