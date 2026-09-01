# 前端 Dashboard 与测试基建（Dashboard & Testing）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: Dashboard 页面
  @FE-DT-S1 @manual
  Scenario: 登录后进入概览
    Given 用户已登录
    When 访问首页或概览页
    Then 页面头部显示「概览」与带用户名的欢迎语
    And 显示渐变欢迎横幅、四张统计卡、快捷入口卡、最近动态卡

  @FE-DT-S2 @manual
  Scenario: 用户档案刷新
    Given 用户已登录
    When 概览页挂载
    Then 自动刷新一次用户档案并合并进全局状态
    And 刷新失败时页面仍正常渲染（静默容错）

  @FE-DT-S3 @manual
  Scenario: 统计卡为静态展示
    Given 用户已进入概览页
    When 查看四张统计卡
    Then 数值固定为 应用程序 12 / 知识库 8 / 团队成员 24 / API 调用 2048
    And 不发起任何统计类请求（当前为占位实现）

  @FE-DT-S4 @manual
  Scenario: 快捷入口跳转落点
    Given 用户已进入概览页
    When 点击「新建应用」「上传文档」「邀请成员」任一快捷条目
    Then 最终被重定向回概览页（目标为占位路由，无业务页面）

  @FE-DT-S5 @manual
  Scenario: 未登录访问被拦截
    Given 浏览器无登录态
    When 直接访问概览页
    Then 被登录守卫拦截并跳转登录页

Feature: 设计系统样册页（/design-system）
  @FE-DT-S6 @manual
  Scenario: 免登录即可访问
    Given 浏览器未登录
    When 直接访问样册页地址
    Then 页面正常渲染（公开路由）

  @FE-DT-S7 @manual
  Scenario: 展示设计系统全量资产
    Given 样册页为公开可访问路由
    When 进入样册页
    Then 依次出现 13 个演示区块（色彩令牌、间距字号、按钮、表单控件、标签、统计卡、主题模式、查询区、表单页、表格、反馈、会话、页面模板）
    And 顶部标注控件高度/圆角/内边距基准值

  @FE-DT-S8 @manual
  Scenario: 样册内实时切换主题
    Given 用户已进入样册页
    When 在「主题模式」区块拨动开关
    Then 全站（含样册自身）即时切换明暗主题
    And 选择被持久化（刷新后保持）

Feature: 测试基建（命令行为）
  @FE-DT-S9 @auto:vitest
  Scenario: 全量测试通过
    Given 前端依赖已安装
    When 在 ui 目录执行全量测试命令
    Then 以 jsdom 环境运行全部组件测试
    And 13 个测试文件 42 个用例全部通过，退出码 0

  @FE-DT-S10 @manual
  Scenario: watch 模式增量重跑
    Given 前端依赖已安装
    When 以 watch 模式启动测试
    Then 改动源文件或测试文件即增量重跑对应用例

  @FE-DT-S11 @manual
  Scenario: 类型检查通过
    Given 前端依赖已安装
    When 执行类型检查命令
    Then 以项目引用模式检查全部源码（含测试文件的全局类型）
    And 无输出且退出码 0

  @FE-DT-S12 @auto:vitest
  Scenario: 测试中 matchMedia 可用
    Given 全局 setup 已提供 matchMedia 桩
    When 渲染依赖响应式判断的组件或读取主题初始值
    Then 不抛出 matchMedia 未定义错误

  @FE-DT-S13 @auto:vitest
  Scenario: 全局断言匹配器可用
    Given 全局 setup 已注册断言匹配器
    When 测试断言使用元素在文档等 jest-dom 匹配器
    Then 匹配器直接可用（无需在测试文件内引入）

Feature: 页面测试范式（以 Users 页测试为准）
  @FE-DT-S14 @auto:vitest
  Scenario: mock API 模块
    Given 测试完整 mock 了页面依赖的全部 API 模块并给出约定形状的返回值
    When 渲染页面
    Then 不发生真实网络请求
    And 列表数据来自 mock

  @FE-DT-S15 @auto:vitest
  Scenario: 注入登录态
    Given 用例向全局状态写入用户信息（含角色）
    When 分别以 root 与普通用户状态渲染
    Then 页面按角色渲染差异化界面（或触发重定向副作用）

  @FE-DT-S16 @auto:vitest
  Scenario: 异步加载断言
    Given 页面组件已按 mock 依赖渲染
    When 页面发起 mock 的异步加载
    Then 以等待查询方式获取数据出现后再断言

  @FE-DT-S17 @auto:vitest
  Scenario: 路由上下文
    Given 组件被内存路由器包裹
    When 页面内执行导航或重定向
    Then 不报错
    And 重定向以"目标内容未渲染"的副作用断言
```
