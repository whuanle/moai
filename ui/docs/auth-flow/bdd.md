# 前端认证流（auth-flow）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md) 第 3 节。页面行为以浏览器走查为主 → @manual；RSA 兼容链路由 E2E 脚本覆盖 → @auto:e2e；已知问题如实写成场景并标注（缺陷记录）。

```gherkin
Feature: 密码登录页（/login）
  @FE-AUTH-S1 @manual
  Scenario: 登录成功
    Given 管理员在 /login 输入正确凭据
    When 提交表单
    Then 密码经服务器公钥 RSA 加密后提交
    And 登录态（token 与用户资料）写入后跳转 /dashboard
    And 页面挂载时已请求第三方渠道列表并渲染图标（无渠道时不渲染 OAuth 分隔区）

  @FE-AUTH-S2 @manual
  Scenario: 凭据错误
    Given 输入错误密码
    When 提交表单
    Then 全局中间件展示后端 401 文案（用户名或密码错误）
    And 因 URL 含 "login" 不触发清态跳转，停留本页

  @FE-AUTH-S3 @manual
  Scenario: 用户名/密码为空
    When 直接提交空表单
    Then 表单必填校验拦截，不发请求

  @FE-AUTH-S4 @manual
  Scenario: rsaPublic 缓存遇密钥轮换报解密失败（缺陷记录：公钥持久缓存无时效）
    Given 本地缓存了旧的 rsaPublic 且后端已轮换密钥
    When 用户提交正确密码登录
    Then 后端解密失败报错
    And 清除本地缓存后重新登录即恢复

Feature: 注册页（/register）
  @FE-AUTH-S5 @manual
  Scenario: 两次密码不一致
    Given 已填 password 与不同值的 confirmPassword
    When 提交
    Then confirmPassword 的一致性校验拦截（不发请求）

  @FE-AUTH-S6 @manual
  Scenario: 密码强度由后端裁决
    When 提交 6-7 位纯数字等弱密码
    Then 前端仅做最小长度提示，后端解密后校验 8-20 位含字母+数字并返回 400

  @FE-AUTH-S7 @manual
  Scenario: 注册成功
    When 后端返回新用户 id
    Then 成功提示并跳 /login（新账号需先登录）

Feature: RequireAuth 守卫（受保护路由 /）
  @FE-AUTH-S8 @manual
  Scenario: 无 token 直接访问受路由
    Given 本地存储中无登录 token
    When 渲染 /
    Then 渲染期立即重定向 /login（不进入检查流程）

  @FE-AUTH-S9 @manual
  Scenario: token 仍有效
    When 挂载执行 token 检查
    Then access token 未过期直接放行，结束加载态

  @FE-AUTH-S10 @manual
  Scenario: access 过期、refresh 有效
    When 挂载或每 60 秒周期检查
    Then 静默换新 token 并覆盖登录态（用户无感，页面不跳转）

  @FE-AUTH-S11 @manual
  Scenario: refresh token 也过期或刷新失败
    When token 检查返回失败
    Then 清空登录态并跳转 /login

Feature: 第三方登录回调（/oauth_login）
  @FE-AUTH-S12 @manual
  Scenario: 已绑定账号
    Given 回调 query 含有效 code 与 state（state={OAuthId}）
    When 页面加载执行第三方登录
    Then 识别为已绑定用户，落登录态并跳 dashboard

  @FE-AUTH-S13 @manual
  Scenario: 未绑定账号
    When 第三方登录返回临时绑定标识
    Then 显示「一键注册并登录」确认卡（展示第三方昵称）
    And 确认后注册并落态跳 dashboard；失败（含临时键过期 403）提示并跳 /login

  @FE-AUTH-S14 @manual
  Scenario: 缺 code/state
    When query 无 code 或无法解析 oAuthId
    Then 直接跳回 /login

  @FE-AUTH-S15 @manual
  Scenario: StrictMode 双挂载
    When 开发模式下回调页 effect 执行两次
    Then code 只被消费一次（ref 守卫防重）

Feature: 绑定弹窗（AccountSettings 经 window.open 发起，state 附加 :bind）
  @FE-AUTH-S16 @manual
  Scenario: 授权成功
    Given 弹窗 opener 存在且同源、state 带 :bind
    When 回调页用 code 发起绑定
    Then 成功消息通知主窗口，弹窗自关闭，主窗口刷新绑定列表

  @FE-AUTH-S17 @manual
  Scenario: 授权失败
    When 绑定接口报错（如渠道已被占用）
    Then 失败消息（含后端文案）通知主窗口后关闭弹窗，主窗口展示错误

  @FE-AUTH-S18 @manual
  Scenario: 用户取消（无 code）
    When 缺 code 或 oAuthId
    Then 取消消息通知主窗口后关闭弹窗，主列表不变

  @FE-AUTH-S19 @manual
  Scenario: opener 跨源
    When opener.origin 与当前页不同源
    Then 不进入绑定模式，退化为顶层登录流程

Feature: RSA 兼容与 401 拦截边界
  @FE-AUTH-S20 @auto:e2e
  Scenario: 前端 RSA 加密产物可被后端解密
    Given 服务器公钥（Base64 DER）
    When 以与前端一致的 RSA PKCS1 方式加密密码并登录/注册/重置
    Then 后端解密还原明文进入密码比对，链路全通

  @FE-AUTH-S21 @manual
  Scenario: 业务 401 整页跳转且无回跳参数（缺陷记录：window.location.href 跳转）
    Given 已登录用户的业务接口返回 401（URL 不含 "login"）
    When 中间件捕获
    Then 清空登录态并整页跳转 /login（丢失 SPA 状态）
    And 登录后固定进 /dashboard，不回跳来源页
```
