# 系统设置（Settings）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。术语：root=超级管理员；admin=管理员（root 隐含）；member=普通用户。

```gherkin
Feature: 查询设置项
  Background:
    Given 系统内置设置项 oauth_auto_register（默认值 "false"）

  @SET-S1 @auto:e2e
  Scenario: 管理员查询设置项
    Given admin 已登录
    When 请求设置项列表
    Then 返回全部内置项，每项含 key/名称/描述/当前值
    And oauth_auto_register 的值为数据库当前值（无记录时为默认值）

  @SET-S2 @auto:e2e
  Scenario: 普通用户无权查询
    Given member 已登录
    When 请求设置项列表
    Then 返回禁止访问（403）提示只有管理员可以访问设置项

  @SET-S3 @auto:e2e
  Scenario: 未登录访问
    When 不带登录凭证请求设置项列表
    Then 返回未授权（401）

Feature: 保存设置项
  Background:
    Given root 已登录

  @SET-S4 @auto:e2e
  Scenario: root 保存设置项并回读生效
    When 保存 oauth_auto_register 为 "true"
    Then 操作成功
    And 再次查询时该项值为 "true"

  @SET-S5 @manual
  Scenario: root 关闭开关
    When 保存 oauth_auto_register 为 "false"
    Then 操作成功且回读为 "false"

  @SET-S6 @auto:e2e
  Scenario: 系统级 root key 受保护
    When 保存 key 为 "root" 的配置
    Then 返回请求错误（400）提示无效的配置项
    And 超级管理员指向未发生变化

  @SET-S7 @auto:e2e
  Scenario: 非法 key 被拒绝
    When 保存不存在的配置项 key
    Then 返回请求错误（400）提示无效的配置项

  @SET-S8 @manual
  Scenario: 首次写入自动建行
    Given 设置表中尚无该内置项记录
    When root 保存该 key
    Then 以内置定义的 key/名称/描述插入新记录且值为提交值

  @SET-S9 @manual
  Scenario: 管理员（非 root）不能保存
    Given admin 已登录但不是 root
    When 保存任一设置项
    Then 返回禁止访问（403）提示只有超级管理员可以修改设置项
    And 设置值未被修改

Feature: 设置项的业务效果
  @SET-S10 @auto:e2e
  Scenario: 开启第三方自动注册后直通登录
    Given oauth_auto_register 已设为 "true"
    When 未注册用户以第三方账号授权登录
    Then 自动创建账号并直接登录（不进入待绑定引导）

Feature: 前端设置页（/settings）
  @SET-S11 @manual
  Scenario: 管理员进入设置页
    Given admin 登录并进入系统设置页
    Then 展示内置项的名称、描述与开关
    And 开关状态与后端值一致

  @SET-S12 @manual
  Scenario: 保存按钮脏检查
    When 未修改任何值
    Then 保存按钮置灰不可点
    When 切换开关
    Then 保存按钮可用

  @SET-S13 @manual
  Scenario: root 保存成功
    Given root 登录
    When 切换开关并保存
    Then 提示成功且刷新页面后状态保持

  @SET-S14 @manual
  Scenario: 保存失败回滚
    Given admin 登录（后端将拒绝保存）
    When 切换开关并保存
    Then 前端重新加载，开关恢复为数据库真实值

  @SET-S15 @manual
  Scenario: 普通用户访问设置页被重定向
    Given member 登录
    When 直接访问设置页
    Then 被重定向到仪表盘
    And 接口层同时返回 403
```
