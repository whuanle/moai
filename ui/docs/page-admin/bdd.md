# 前端管理页（Settings / OauthConnect）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md) 第 3 节。两页无组件自动化，均为浏览器走查场景；后端门禁与接口行为以上游 BDD 为准。

```gherkin
Feature: 管理菜单可见性（AppSider）
  @FE-PG-S1 @manual
  Scenario: 管理员看到管理菜单
    Given 管理员已登录
    When 查看侧边栏
    Then 管理组在主导航下以分隔线展示：插件、用户、第三方登录、设置

  @FE-PG-S2 @manual
  Scenario: 普通用户看不到管理菜单
    Given 普通用户已登录
    When 查看侧边栏
    Then 只有主导航（概览/应用/知识库/团队）
    And 不出现管理组

  @FE-PG-S3 @manual
  Scenario: 占位菜单项的兜底
    Given 管理员已登录
    When 点击管理组「插件」
    Then 被重定向回概览页（该入口为占位导航）

Feature: 页面门禁
  @FE-PG-S4 @manual
  Scenario: 普通用户访问系统设置页被重定向
    Given 普通用户已登录
    When 直接访问系统设置页
    Then 被重定向回概览页

  @FE-PG-S5 @manual
  Scenario: 普通用户访问渠道页被重定向
    Given 普通用户已登录
    When 直接访问第三方登录渠道页
    Then 被重定向回概览页
    And 不发起渠道列表请求

  @FE-PG-S6 @manual
  Scenario: 后端接口门禁兜底
    Given 普通用户已登录
    When 绕过页面直接调用管理接口
    Then 后端返回没有权限（403）

Feature: OAuth 自动注册开关（/settings）
  Background:
    Given 管理员已登录并进入系统设置页

  @FE-PG-S7 @manual
  Scenario: 查看当前值
    When 页面加载完成
    Then 自动注册开关反映设置项当前值
    And 未修改时保存按钮不可点

  @FE-PG-S8 @manual
  Scenario: 打开开关并保存
    When 切换开关为开并点击保存
    Then 提示保存成功
    And 保存按钮恢复禁用
    And 后续第三方登录遇未注册用户时自动建号

  @FE-PG-S9 @manual
  Scenario: 保存失败回滚
    Given 开关当前为关
    When 切换为开后保存失败
    Then 页面自动恢复开关为数据库真实值
    And 界面不残留假状态

Feature: 渠道列表（/oauthconnect）
  @FE-PG-S10 @manual
  Scenario: 管理员进入看到渠道表格
    Given 管理员已登录
    When 进入第三方登录渠道页
    Then 展示渠道表格（名称/类型/Key/图标/授权地址/操作）
    And 支持刷新，无分页

Feature: 新建 OAuth 渠道
  Background:
    Given 管理员在渠道页点击「新建」

  @FE-PG-S11 @manual
  Scenario: 创建自定义 OIDC 渠道
    When 填写名称、类型为自定义、Key、Secret、图标（URL 或上传）与发现端点并保存
    Then 列表刷新出现新行
    And 类型列显示自定义标签
    And 授权地址列可复制

  @FE-PG-S12 @manual
  Scenario: 缺少必填项被拦截
    When 不填 Secret 或图标提交
    Then 表单就地提示必填
    And 不发起请求

  @FE-PG-S13 @manual
  Scenario: 选择飞书或钉钉
    When 类型切换为飞书或钉钉
    Then 发现端点字段隐藏且不校验（内置端点）
    And 图标为空时自动填充官方默认图标（已有值不覆盖）

Feature: 编辑 OAuth 渠道
  @FE-PG-S14 @manual
  Scenario: 打开编辑弹窗
    Given 渠道列表中存在至少一条渠道
    When 点击某行「编辑」
    Then 弹窗回填名称/类型/Key/图标/发现端点，Secret 为空
    And 类型选择器禁用（渠道类型不可改）
    And Secret 带"留空保持不变"提示（非必填）

  @FE-PG-S15 @manual
  Scenario: 编辑提交成功
    Given 编辑弹窗已打开并回填某渠道
    When 修改名称（Secret 留空）并保存
    Then 保存成功且列表刷新为新值
    And Secret 保持原值不变
    And 其他会话（含登录页图标）不受影响

Feature: 删除 OAuth 渠道
  @FE-PG-S16 @manual
  Scenario: 删除需二次确认
    Given 渠道列表中存在至少一条渠道
    When 点击某行「删除」
    Then 出现二次确认框
    When 确认删除
    Then 删除成功且列表刷新，该渠道从登录页消失
```
