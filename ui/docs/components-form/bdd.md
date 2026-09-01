# 前端表单与反馈组件（components-form）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: FormPage 表单页壳
  @FE-CF-S1 @auto:vitest
  Scenario: 校验通过后提交
    Given 表单含必填与邮箱格式校验
    When 校验通过后点击提交（或原生提交）
    Then 提交回调收到表单值
    And 提交态为真时提交按钮呈加载状（抑制重复点击）

  @FE-CF-S2 @auto:vitest
  Scenario: 取消
    Given 传入了取消回调
    When 点击「取消」按钮
    Then 取消回调被触发

  @FE-CF-S3 @auto:vitest
  Scenario: 标题与结构
    When 传入标题
    Then 渲染表单级标题、提交/取消按钮与表单元素
    And 容器为表单阅读宽度居中，标签垂直排列

Feature: DetailPage 详情页
  @FE-CF-S4 @auto:vitest
  Scenario: 只读展示
    When 传入字段列表与 返回/编辑 回调
    Then 渲染标题、字段键值及「返回」「编辑」按钮

  @FE-CF-S5 @auto:vitest
  Scenario: 加载骨架
    When 加载态为真
    Then 描述区呈现骨架屏而非空表

  @FE-CF-S6 @auto:vitest
  Scenario: 操作位回调
    When 点击「返回」或「编辑」
    Then 对应回调各触发一次

Feature: Chat 对话页
  @FE-CF-S7 @auto:vitest
  Scenario: 渲染与发送
    Given 消息列表含用户消息与助手回复
    When 输入内容并点击发送
    Then 两条消息按角色分侧渲染（用户右侧主色气泡、助手左侧浅色气泡）
    And 发送回调触发；发送中时按钮呈加载状

  @FE-CF-S8 @manual
  Scenario: 空态
    Given 消息列表为空
    Then 渲染空态提示（缺省为内置文案，建议调用方传国际化文案）

Feature: Feedback 反馈子系统
  @FE-CF-S9 @manual
  Scenario: 未注册实例时安全降级
    Given 反馈桥尚未挂载
    When 调用任一反馈方法
    Then 控制台给出警告提示且应用不崩溃（空操作降级）

  @FE-CF-S10 @auto:vitest
  Scenario: 挂载桥后注册实例
    When 反馈桥挂到 antd App 内
    Then message/notification 实例注册成功，可正常弹出

  @FE-CF-S11 @auto:vitest
  Scenario: 错误分类与信息提取
    Given 各种形态的异常对象
    When 归一化处理
    Then 能从多种结构中识别 HTTP 状态码
    And 能判定网络异常
    And 业务 detail 优先于 message，字段级校验错误次之
    And 通用框架文案与网络文案被过滤

  @FE-CF-S12 @auto:vitest
  Scenario: 非成功响应体解析
    When 解析非 2xx 响应体
    Then 产出含状态码与 detail 的归一化错误，保留字段错误并回退 message
    And 空/非 JSON 响应体被容忍不抛错

  @FE-CF-S13 @auto:vitest
  Scenario: 错误路由
    Given 4xx 业务错误
    Then 走轻量消息通道（不触发系统通知）
    Given 5xx 服务端错误
    Then 走系统通知通道
    Given 网络异常
    Then 走系统通知通道（网络异常文案）
    Given 未知异常
    Then 走系统通知通道（系统异常文案）

  @FE-CF-S14 @auto:vitest
  Scenario: 成功与系统通知通道
    When 发送成功反馈
    Then 走轻量消息通道
    When 发送系统级警告通知
    Then 走系统通知通道
```
