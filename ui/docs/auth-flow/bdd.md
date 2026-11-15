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
    And 登录响应（仅含 token 与基本标识）写入登录态后跳转 /apps
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

  @FE-AUTH-S22 @manual
  Scenario: 进入应用时重取用户资料
    Given 用户刚完成登录（登录响应仅含 token，不含角色与资料）
    When 首次通过守卫进入受保护路由
    Then 守卫在放行前向服务端重取用户资料并合并进登录态
    And 管理员身份（含第三方登录绑定管理员的场景）立即生效，侧边栏出现管理菜单

  @FE-AUTH-S23 @manual
  Scenario: 刷新页面校正本地滞留快照
    Given 本地持久化的用户资料已过期（如头像昵称被改、被授予或撤销管理员）
    When 用户刷新页面进入受保护路由
    Then 登录态以服务端重取的资料为准（角色/头像/昵称即时校正）
    And 资料接口偶发失败时沿用本地快照进入应用（不作为登出理由）

  @FE-AUTH-S10 @manual
  Scenario: access 即将过期、refresh 有效
    When 挂载或每 60 秒周期检查发现 access token 剩余有效期不足 5 分钟
    Then 提前静默换新 token 并覆盖登录态（用户无感，页面不跳转）
    And 续期响应不含的角色/头像/昵称沿用本地登录态，不被覆盖成空

  @FE-AUTH-S24 @manual
  Scenario: 不在 token 过期后才续期（线上 30 分钟必掉线修复）
    Given 线上 Release 构建签发的 access token 有效期为 30 分钟（DEBUG 构建为 7 天）
    When 页面长时间停留且持续有业务请求
    Then 续期发生在令牌到期前（剩余 5 分钟内），业务请求不会带着过期 token 命中 401
    And 不存在"每 30 分钟被强制跳回 /login"的窗口

  @FE-AUTH-S25 @manual
  Scenario: 休眠/标签页挂起恢复后补检
    Given 浏览器标签页长时间不可见或整机休眠，周期定时器已停滞
    When 用户回到页面（可见性变化/窗口聚焦/恢复联网/页面恢复）
    Then 立即补做一次 token 检查（5 秒内不重复触发）
    And access token 已过期时先静默续期再交给业务请求

  @FE-AUTH-S11 @manual
  Scenario: refresh token 也过期或刷新失败
    When token 检查返回失败
    Then 清空登录态并跳转 /login

  @FE-AUTH-S26 @manual
  Scenario: 续期请求偶发失败不误登出
    Given access token 还有剩余有效期、refresh token 未过期
    When 续期请求遇到网络异常/服务端 5xx（后端重启、反代 502、断网）
    Then 本次检查沿用现有登录态，下个周期再续，不跳转 /login
    And access token 已真正过期且续期仍失败时才清态跳 /login

  @FE-AUTH-S27 @manual
  Scenario: 业务请求 401 时先续期再重试
    Given 本地仍有可用的 refresh token
    When 任一鉴权业务请求返回 401（令牌刚过期/休眠唤醒/浏览器时钟偏差）
    Then 请求层先静默续期，并用新 token 自动重试该请求一次
    And 重试成功时用户无感、页面不跳转；重试仍 401 才清态跳 /login
    And 登录/续期接口自身的 401 不参与该重试，避免递归

Feature: 第三方登录回调（/oauth_login）
  @FE-AUTH-S12 @manual
  Scenario: 已绑定账号
    Given 回调 query 含有效 code 与 state（state={OAuthId}）
    When 页面加载执行第三方登录
    Then 识别为已绑定用户，落登录态并跳 /apps（资料由守卫重取，见 [@FE-AUTH-S22](#fe-auth-s22)）

  @FE-AUTH-S13 @manual
  Scenario: 未绑定账号
    When 第三方登录返回临时绑定标识
    Then 显示「一键注册并登录」确认卡（展示第三方昵称）
    And 确认后注册并落态跳 /apps；失败（含临时键过期 403）提示并跳 /login

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
    And 登录后固定进 /apps，不回跳来源页
```
