# 认证（Auth）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 密码登录
  Background:
    Given 系统存在用户 admin（密码 abcd123456）
    And 客户端已从服务器信息接口获取 RSA 公钥

  @AUTH-S1 @auto:e2e
  Scenario: 正确凭据登录
    When 以用户名 admin 与加密密码请求登录
    Then 返回访问令牌、刷新令牌、用户标识与过期时刻
    And 该用户的登录失败计数被清除

  @AUTH-S2 @auto:e2e
  Scenario: 用户名或密码错误
    When 以错误密码请求登录
    Then 返回未授权（401）提示用户名或密码错误
    And 该用户名的失败计数加一（5 分钟窗口）

  @AUTH-S3 @auto:e2e
  Scenario: 连续失败锁定
    When 同一用户名连续 5 次密码错误
    Then 第 6 次即使密码正确也返回禁止访问（403）提示登录失败次数过多
    And 清除计数后可立即恢复登录

  @AUTH-S4 @manual
  Scenario: 被禁用账号登录
    Given admin 已被管理员禁用
    When 使用正确密码请求登录
    Then 返回未授权（401）提示用户已被禁用

Feature: 注册
  @AUTH-S5 @auto:e2e
  Scenario: 正常注册
    When 提交用户名/邮箱/昵称/手机号与加密密码（8-20 位含字母数字）
    Then 注册成功并返回新用户标识

  @AUTH-S6 @auto:e2e
  Scenario: 弱密码
    When 提交密码 "1234"
    Then 返回请求错误（400）提示密码需 8-20 长度且包含数字+字母

  @AUTH-S7 @auto:e2e
  Scenario Outline: 注册信息重复
    When 提交的 <字段> 已被其他账号占用
    Then 返回冲突（409）并提示该 <字段> 已被注册

    Examples:
      | 字段   |
      | 用户名 |
      | 邮箱   |
      | 手机号 |

  @AUTH-S8 @auto:e2e
  Scenario: 缺少必填项
    When 提交注册时缺少手机号或昵称
    Then 返回请求错误（400）且错误信息指出具体字段不能为空

Feature: 令牌刷新
  @AUTH-S9 @auto:e2e
  Scenario: 有效刷新令牌续签
    When 以有效刷新令牌请求续签
    Then 返回全新的访问令牌与刷新令牌

  @AUTH-S10 @auto:e2e
  Scenario: 用访问令牌冒充刷新令牌
    When 以访问令牌请求续签
    Then 返回未授权（401）提示非刷新令牌

  @AUTH-S11 @manual
  Scenario: 刷新令牌过期
    Given 刷新令牌已超过 7 天有效期
    When 以其请求续签
    Then 返回未授权（401）提示令牌验证失败

  @AUTH-S12 @manual
  Scenario: 被禁用用户续签
    Given 刷新令牌对应用户已被禁用
    When 以其请求续签
    Then 返回未授权（401）提示用户已被禁用

Feature: 第三方登录（OAuth）
  @AUTH-S13 @auto:e2e
  Scenario: 获取第三方登录方式列表
    When 匿名请求第三方登录方式列表
    Then 返回每个提供商拼好的授权跳转地址（含 client_id/response_type/scope/state/redirect_uri）

  @AUTH-S14 @manual
  Scenario: 回跳地址不合法被拒绝（实际行为：任意地址均放行，缺陷记录）
    When 请求列表时携带回跳地址 http://evil.com
    Then 实际返回 200 且地址未校验（设计应为 400「不合法的跳转地址」；Controller 空参构造 Query 导致校验为死代码，见 [SDD 已知问题](./sdd.md)）

  @AUTH-S15 @auto:e2e
  Scenario: 已绑定的第三方账号直通登录
    When 以有效授权码发起第三方登录且该第三方账号已绑定本地账号
    Then 返回已绑定标记并直接携带登录令牌

  @AUTH-S16 @auto:e2e
  Scenario: 未绑定的第三方账号进入待绑定
    When 以有效授权码发起第三方登录且未绑定本地账号
    Then 返回未绑定标记与临时绑定标识（10 分钟内有效）及第三方昵称

  @AUTH-S17 @auto:e2e
  Scenario: 一键注册并绑定
    Given 临时绑定标识在 10 分钟内有效
    When 确认一键注册
    Then 创建用户名为 u{id} 的账号并完成绑定，返回登录令牌

  @AUTH-S18 @manual
  Scenario: 临时绑定过期
    Given 临时绑定标识已超过 10 分钟
    When 确认一键注册
    Then 返回禁止访问（403）提示第三方授权跳转登录已过期

  @AUTH-S19 @auto:e2e
  Scenario: 同一第三方账号重复注册
    Given 该第三方账号（ProviderId+Sub）已绑定
    When 再次对其一键注册
    Then 返回冲突（409）提示该 OAuth 用户已被注册

Feature: 前端登录链路
  @AUTH-S20 @manual
  Scenario: 登录页密码加密与第三方入口
    Given 用户打开登录页
    Then 展示账号密码表单与第三方登录图标（来自提供商列表）
    And 提交前密码经服务器公钥加密，成功后进入仪表盘

  @AUTH-S21 @manual
  Scenario: 会话过期拦截与静默续期
    Given 用户已登录且访问令牌即将过期
    Then 刷新令牌仍有效时自动静默续期
    And 任一非登录接口返回 401 时清除登录态并跳转登录页

  @AUTH-S22 @manual
  Scenario: 第三方回调页三分支
    When 第三方授权回跳到回调页
    Then 已绑定则直接进入仪表盘；未绑定展示一键注册确认；弹窗模式完成账号绑定后通知主窗口并自关闭

  @AUTH-S23 @manual
  Scenario: 注册页前端校验
    When 用户在注册页提交两次密码不一致或必填缺失
    Then 表单就地提示错误且不发起请求
```
