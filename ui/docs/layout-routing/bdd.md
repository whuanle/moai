# 前端布局导航与路由（layout-routing）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 第 3 节。认证语义（token 生命周期）见上游 [../../docs/auth-flow/bdd.md](../../docs/auth-flow/bdd.md)。

```gherkin
Feature: 路由守卫（RequireAuth）
  @FE-LR-S1 @manual
  Scenario: 未登录访问受保护路由
    Given 本地无访问令牌
    When 直接打开受保护页面
    Then 同步重定向到登录页（不发起任何请求）

  @FE-LR-S2 @manual
  Scenario: 首次进入的令牌检查
    Given 已持有访问令牌
    When 受保护布局挂载
    Then 先执行一次令牌校验，期间渲染全屏加载指示
    And 校验通过后渲染布局（侧栏 + 内容区）
    And 校验失败（含异常）时清空登录态并跳转登录页

  @FE-LR-S3 @manual
  Scenario: 周期性令牌续期
    Given 已登录停留在任意受保护页
    Then 每 60 秒自动执行一次令牌校验
    When 校验失败
    Then 清空登录态并跳转登录页

  @FE-LR-S4 @manual
  Scenario: 令牌过期后的请求侧拦截
    When 某业务接口返回未授权（401，且非登录接口）
    Then 请求中间件清空登录态并整页跳转登录页（不发提示）

Feature: 路由表与兜底
  @FE-LR-S5 @manual
  Scenario: 公开页直通
    When 访问 登录/注册/OAuth 登录/设计系统展示台 任一
    Then 不经过登录守卫直接渲染

  @FE-LR-S6 @manual
  Scenario: 根路径与未知路径兜底
    When 访问根路径或任意未注册路径
    Then 均重定向到概览页
    And 侧栏高亮回到「概览」

  @FE-LR-S7 @manual
  Scenario: 业务子路由
    Given 已登录
    When 访问 概览/账号设置/用户/系统设置/第三方登录 任一
    Then 经统一布局的内容区渲染对应页面

Feature: 侧边栏导航（AppSider）
  @FE-LR-S8 @manual
  Scenario: 普通用户不见管理区
    Given 当前用户非管理员
    Then 只见主导航（概览/应用/知识库/团队），无管理区

  @FE-LR-S9 @manual
  Scenario: 管理员可见管理区
    Given 当前用户是管理员
    Then 主导航下出现分隔线与管理区（插件/用户/第三方登录/设置）

  @FE-LR-S10 @manual
  Scenario: 点击菜单导航并高亮
    When 点击「用户」菜单
    Then 跳转用户页且菜单高亮跟随

  @FE-LR-S11 @manual
  Scenario: 未实现菜单项兜底回概览
    When 点击未实现的菜单项（应用/知识库/团队/插件）
    Then 路由落到兜底规则，回到概览页并高亮「概览」（已知体验问题）

  @FE-LR-S12 @manual
  Scenario: 用户卡信息展示
    When 查看侧栏顶部用户卡
    Then 头像取用户资料（缺省以首字母兜底）
    And 主行显示昵称（缺省用户名），副行显示邮箱（缺省用户名）

Feature: 用户卡操作
  @FE-LR-S13 @manual
  Scenario: 打开账号设置
    When 点击头像区菜单中的「设置」
    Then 跳转账号设置页

  @FE-LR-S14 @manual
  Scenario: 退出登录
    When 点击「退出登录」
    Then 清空登录态并跳转登录页

Feature: 布局级偏好切换入口（Sider 底部）
  @FE-LR-S15 @manual
  Scenario: 底部切换主题
    When 在侧栏底部选择 暗色/亮色
    Then 主题状态更新并写入本地存储，全站即时生效（行为规格见主题模块）

  @FE-LR-S16 @manual
  Scenario: 底部切换语言
    When 在侧栏底部选择 简体中文/English
    Then 语言状态更新并写入本地存储，界面文案与内置组件文案同步（行为规格见状态模块）

Feature: 设计系统展示台
  @FE-LR-S17 @manual
  Scenario: 匿名查看设计系统活文档
    When 未登录访问展示台
    Then 渲染令牌色板/间距字号/组件/模板的全部演示段
    And 顶部提示控件高度、圆角与常规内边距基准值

Feature: 页面级权限（页面内自判）
  @FE-LR-S18 @auto:vitest
  Scenario: 非管理员访问管理页被重定向
    Given 普通用户已登录
    When 直接访问用户管理页
    Then 被重定向到仪表盘（且不拉取数据）
```
