# 用户管理（User Management）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 用户列表与详情
  Background:
    Given root 账号 admin 已登录
    And 系统中存在普通用户 bob 与 alice

  @UM-S1 @auto:e2e
  Scenario: 管理员分页查看用户列表
    When 请求用户列表第 1 页
    Then 返回用户清单与总数
    And 每个用户含 用户名/昵称/邮箱/手机号/角色/状态/创建时间

  @UM-S2 @auto:e2e
  Scenario: 超级管理员被正确标识
    When 查看用户列表中的 admin 记录
    Then 其角色为超级管理员

  @UM-S3 @auto:e2e
  Scenario: 关键字搜索
    When 以 "bob" 搜索用户列表
    Then 结果仅包含用户名/昵称/邮箱命中 bob 的用户

  @UM-S4 @auto:e2e
  Scenario: 普通用户无权访问列表
    Given bob 已登录
    When 请求用户列表
    Then 返回没有权限（403）

  @UM-S5 @auto:e2e
  Scenario: 查看用户详情
    When 管理员查看 bob 的信息
    Then 返回 bob 的完整用户信息

  @UM-S6 @auto:e2e
  Scenario: 查看不存在的用户
    When 管理员查看 id 为 999999 的用户
    Then 返回用户不存在（404）

Feature: 设置管理员（仅 root）
  @UM-S7 @auto:e2e
  Scenario: root 授予管理员角色
    When root 将 bob 设为管理员
    Then 操作成功
    And bob 重新登录后可访问管理功能

  @UM-S8 @auto:e2e
  Scenario: root 撤销管理员角色
    Given bob 当前是管理员
    When root 取消 bob 的管理员角色
    Then bob 不再是管理员

  @UM-S9 @auto:e2e
  Scenario: 管理员不能授权他人
    Given bob 是管理员但不是 root
    When bob 将 alice 设为管理员
    Then 返回没有权限（403）
    And alice 角色不变

  @UM-S10 @auto:e2e
  Scenario: 不能操作 root 账号的角色
    When root 尝试取消 admin(root) 的管理员角色
    Then 返回请求错误（400）
    And root 权限不变

  @UM-S11 @auto:e2e
  Scenario: 不能操作自己的角色
    When root 尝试取消自己的管理员角色
    Then 返回请求错误（400）

Feature: 禁用与启用
  @UM-S12 @auto:e2e
  Scenario: 管理员禁用普通用户
    When 管理员禁用 bob
    Then 操作成功
    And bob 的下一个请求被拦截并提示账号已被禁用

  @UM-S13 @auto:e2e
  Scenario: 启用恢复访问
    Given bob 处于禁用状态
    When 管理员启用 bob
    Then bob 恢复正常访问

  @UM-S14 @auto:e2e
  Scenario: 管理员不能禁用另一个管理员
    Given bob 与 alice 都是管理员（均非 root）
    When bob 禁用 alice
    Then 返回没有权限（403）

  @UM-S15 @auto:e2e
  Scenario: root 可以禁用管理员
    Given alice 是管理员
    When root 禁用 alice
    Then 操作成功

  @UM-S16 @auto:e2e
  Scenario: 不能禁用 root
    When 任何管理员尝试禁用 admin(root)
    Then 返回请求错误（400）

  @UM-S17 @auto:e2e
  Scenario: 不能禁用自己
    When 管理员禁用自己的账号
    Then 返回请求错误（400）

Feature: 重置密码
  @UM-S18 @auto:e2e
  Scenario: 管理员重置普通用户密码
    Given 新密码已用服务器公钥加密
    When 管理员重置 bob 的密码
    Then 操作成功
    And bob 用新密码登录成功、旧密码登录失败

  @UM-S19 @auto:e2e
  Scenario: 新密码强度不足
    When 管理员提交弱密码 "1234"
    Then 返回请求错误（400）
    And bob 的原密码仍然有效

  @UM-S20 @auto:e2e
  Scenario: 管理员不能重置其他管理员密码
    Given bob 与 alice 都是管理员（均非 root）
    When bob 重置 alice 的密码
    Then 返回没有权限（403）

  @UM-S21 @auto:e2e
  Scenario: 不能重置 root 密码
    When 管理员重置 admin(root) 的密码
    Then 返回请求错误（400）

Feature: 前端用户页（/users）
  @UM-S22 @auto:vitest
  Scenario: 管理员看到用户表格
    Given admin 登录并进入用户页
    Then 展示分页表格与搜索栏
    And 每行提供 查看/禁用/重置密码 操作

  @UM-S23 @auto:vitest
  Scenario: 仅 root 可见授权操作
    Given root 登录
    Then 普通用户行额外显示 设为管理员/取消管理员
    And root 自己所在行不渲染任何危险操作

  @UM-S24 @manual
  Scenario: 普通用户访问用户页被重定向
    Given bob 登录
    When 直接访问 /users
    Then 被重定向到仪表盘
    And 接口层同时返回 403

  @UM-S25 @manual
  Scenario: 重置密码弹窗前端校验
    When 管理员输入两次不一致或强度不足的新密码
    Then 表单就地提示错误且不发起请求

  @UM-S26 @manual
  Scenario: 禁用/启用二次确认
    When 管理员点击禁用
    Then 弹出确认框，确认后才执行并刷新列表
```
