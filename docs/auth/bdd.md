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

  @AUTH-S24 @auto:e2e
  Scenario: 刷新令牌续签携带用户类型
    When 内部用户以有效刷新令牌请求续签
    Then 新访问令牌携带用户类型 normal（缺失会使会话/日志把内部用户误标为外部用户）
    And 新访问令牌仍携带用户标识，登录令牌同样携带用户类型

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
    When 匿名请求第三方登录方式列表（登录页携带自身来源 redirectUrl）
    Then 返回每个提供商按各自协议拼好的授权跳转地址（state=oauthId、redirect_uri={登录页来源}/oauth_login，未携带 redirectUrl 时回退 {WebUI}/oauth_login；参数差异：默认 OIDC 用 client_id/response_type/scope，钉钉附 scope=openid corpid 与 prompt=consent，企业微信用 login_type=CorpApp/appid=CorpID/agentid）

  @AUTH-S14 @auto:e2e
  Scenario: 回跳地址跟随登录页来源
    When 请求列表时携带 redirectUrl（内网穿透等场景，前端访问域名可与 WebUI 配置不同）
    Then 回调地址使用该来源拼装（仅接受 http/https），发起登录后飞书换 token 的 redirect_uri 与授权时保持一致
    But 非 http/https 协议的 redirectUrl 返回请求错误（400）"不合法的跳转地址"

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

  @AUTH-S25 @manual
  Scenario: 企业微信授权码换取用户身份
    Given 企业微信连接器已配置（CorpID/Secret/AgentId，应用可信域名含 WebUI）
    When 以企业微信扫码授权回跳的 code 发起登录
    Then 按 gettoken→getuserinfo→user/get 取 userid 作为 Sub 并尽力补全姓名与头像
    And 应用无通讯录读权限时回退以 userid 作展示名，不阻断登录
    And 后续绑定/一键注册流程与 @AUTH-S15 ~ @AUTH-S19 一致

Feature: 前端登录链路
  @AUTH-S20 @manual
  Scenario: 登录页密码加密与第三方入口
    Given 用户打开登录页
    Then 展示账号密码表单与第三方登录图标（来自提供商列表）
    And 提交前密码经服务器公钥加密，成功后进入应用市场

  @AUTH-S21 @manual
  Scenario: 会话过期拦截与静默续期
    Given 用户已登录且访问令牌即将过期
    Then 刷新令牌仍有效时自动静默续期
    And 任一非登录接口返回 401 时清除登录态并跳转登录页

  @AUTH-S22 @manual
  Scenario: 第三方回调页三分支
    When 第三方授权回跳到回调页
    Then 已绑定则直接进入应用市场；未绑定展示一键注册确认；弹窗模式完成账号绑定后通知主窗口并自关闭

  @AUTH-S23 @manual
  Scenario: 注册页前端校验
    When 用户在注册页提交两次密码不一致或必填缺失
    Then 表单就地提示错误且不发起请求

Feature: 邮箱验证码（注册与找回密码）
  Background:
    Given SMTP 邮件服务与注册验证码开关由超级管理员在系统设置中控制
    And 客户端可匿名查询服务器信息获取两个能力开关

  @AUTH-S26 @auto:e2e
  Scenario: 找回密码入口随 SMTP 启用而开放
    When 匿名查询服务器信息
    Then SMTP 邮件服务启用时 passwordResetEnabled 为真，登录页展示忘记密码入口
    And SMTP 邮件服务停用后该开关复位为假

  @AUTH-S27 @auto:e2e
  Scenario: 未启用 SMTP 邮件服务时不可自助找回密码
    Given SMTP 邮件服务未启用
    When 请求发送找回密码验证码或提交重置密码
    Then 均返回请求错误（400）提示联系管理员重置（管理员可在用户管理中重置用户密码）

  @AUTH-S28 @auto:e2e
  Scenario: 找回密码发送验证码
    When 对未注册邮箱请求找回密码验证码
    Then 返回资源不存在（404）提示邮箱未注册
    When 对已注册邮箱请求找回密码验证码
    Then 6 位数字验证码邮件经 SMTP 服务发出，10 分钟内有效

  @AUTH-S29 @auto:e2e
  Scenario: 验证码发送冷却
    When 同一邮箱同一用途 60 秒内再次请求发送验证码
    Then 返回请求过多（429）提示发送过于频繁；发送失败时不占用冷却可立即重试

  @AUTH-S30 @auto:e2e
  Scenario: 验证码一次性与错误上限
    When 验证码被成功消费一次后再次使用
    Then 返回请求错误（400）提示已过期或未发送
    When 同一验证码连续 5 次输入错误
    Then 验证码作废，后续即使输入正确也返回请求错误（400）提示次数过多

  @AUTH-S31 @auto:e2e
  Scenario: 验证码邮件模板渲染
    When 发送验证码邮件
    Then 主题与正文按系统设置绑定的通知中心模板渲染（{{code}}/{{siteName}}/{{expireMinutes}} 双花括号占位符替换为实际值，HTML 模板按 HTML 邮件发送）
    And 绑定为空时按用途回退内置模板（注册/登录/找回密码验证码邮件，HTML），绑定模板缺失或渲染失败时同样回退
    And 无内置模板可用时使用代码内置纯文本模板兜底，模板中未知变量按字面保留

  @AUTH-S32 @auto:e2e
  Scenario: 验证码重置密码
    When 携带错误验证码提交重置
    Then 返回请求错误（400）
    When 携带正确验证码与 RSA 加密新密码提交重置
    Then 密码更新成功，旧密码登录被拒（401）且新密码可登录

  @AUTH-S33 @auto:e2e
  Scenario: 注册验证码开关回显
    When 匿名查询服务器信息
    Then 仅 SMTP 邮件服务启用且注册验证码开关开启时 registerEmailCodeRequired 为真，注册页展示验证码输入与发送按钮
    And 关闭开关或停用 SMTP 后该标记为假

  @AUTH-S34 @auto:e2e
  Scenario: 开启后注册必须携带验证码
    When 注册验证码开关开启后提交注册
    Then 缺少验证码或验证码错误均返回请求错误（400）
    And 携带正确验证码注册成功并可正常登录

  @AUTH-S35 @auto:e2e
  Scenario: 未开启注册验证码时直接注册
    When 注册验证码开关关闭
    Then 注册无需验证码（存量注册链路行为不变）

  @AUTH-S36 @auto:e2e
  Scenario: 登录安全策略基线
    When 登录邮箱验证码策略为 off 且未开启强制两步验证
    Then 用户名密码校验通过后直接颁发令牌（secondFactor=none、mfaPending=false）

  @AUTH-S37 @auto:e2e
  Scenario: 两步验证（TOTP）绑定全流程
    When 登录用户查询两步验证状态
    Then 返回 enabled/forced 与系统强制开关一致
    When 调 setup 生成密钥
    Then 返回 base32 密钥与 otpauth:// URI（issuer 为站点名），密钥 AES 加密落库但未启用
    When 携带错误或非 6 位动态码调 enable
    Then 返回请求错误（400）提示动态码错误
    When 携带认证器当前动态码调 enable
    Then 绑定激活（enabled=true），重复 setup 返回 400，改用户数据后用户态缓存失效

  @AUTH-S38 @auto:e2e
  Scenario: 登录 TOTP 挑战与一次性票据
    When 已绑定两步验证的用户提交正确用户名密码
    Then 不颁发令牌，返回 secondFactor=totp 与 5 分钟有效的一次性登录票据
    When 携带错误动态码或已消费票据调 login/verify
    Then 分别返回请求错误（400）与登录会话过期（401）
    When 携带正确动态码调 login/verify
    Then 颁发令牌；同一时间步长的动态码再次使用被拒（防重放），下一时间步动态码可用

  @AUTH-S39 @auto:e2e
  Scenario: 关闭两步验证
    When 携带错误动态码调 disable
    Then 返回请求错误（400）
    When 携带当前动态码调 disable
    Then 解绑成功（密钥与启用标记清空），后续登录直达不再挑战
    When 未启用时调 disable
    Then 返回请求错误（400）提示尚未开启

  @AUTH-S40 @auto:e2e
  Scenario: 强制两步验证
    When 系统开启强制 2FA 后未绑定用户登录
    Then 照常颁发令牌但 mfaPending=true（token 携带 mfa_pending 声明）
    When 该令牌访问绑定流程外接口
    Then 返回 403 提示请先绑定两步验证
    When 访问白名单接口（/auth/two_factor/*、/account/userinfo）
    Then 正常可用，userinfo 回显 mfaPending=true
    When 完成绑定后用原令牌再访问业务接口
    Then 立即恢复完整访问（用户态缓存已失效重查），无需重新登录
    When 强制期间调 disable
    Then 返回请求错误（400）提示系统已强制开启

  @AUTH-S41 @auto:e2e
  Scenario: 管理员重置用户两步验证
    When 管理员对目标用户调 reset_2fa
    Then 清空其 TOTP 绑定，用户下次登录回到强制绑定流程
    When 对 root 自身或未绑定用户调 reset_2fa
    Then 分别返回 400（root 受保护 / 尚未绑定）
    When 非管理员调 reset_2fa
    Then 返回 403

  @AUTH-S42 @auto:e2e
  Scenario: 登录邮箱验证码策略
    When 策略为 always 且 SMTP 已启用
    Then 每次密码校验通过后自动发送登录验证码并返回 secondFactor=emailCode 挑战；冷却期内重登不重发邮件但签发新票据；正确验证码换颁发令牌
    When 策略为 after_fail
    Then 无失败记录时直接登录；密码错误一次后的登录需要验证码；验证成功后失败计数清零恢复直达
    When 匿名公共发码端点收到 purpose=login
    Then 拒绝（400），登录验证码只能凭一次性票据经 login_code/send 重发（60 秒冷却 429）
    When 用户已绑定两步验证
    Then TOTP 挑战优先于邮箱验证码
    When SMTP 未启用
    Then 验证码策略失效，登录直达
```
