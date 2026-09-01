# 账号自助（Account Self-Service）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。管理员治理场景见 [../user-management/bdd.md](../user-management/bdd.md)。

```gherkin
Feature: 查询用户信息
  Background:
    Given 用户 alice 已登录

  @ACC-S1 @auto:e2e
  Scenario: 查询自己的资料
    When 请求自己的用户信息
    Then 返回 userId/userName/email/nickName/phone/avatar/isDisable/isAdmin/isRoot/isDeleted
    And avatar 为空或为完整静态地址

  @ACC-S2 @auto:e2e
  Scenario: 未登录查询
    When 不带登录凭证请求自己的用户信息
    Then 返回未授权（401）

  @ACC-S3 @auto:e2e
  Scenario: 账号被禁用后访问
    Given alice 的账号刚被管理员禁用
    When alice 携带原令牌请求任意需登录接口
    Then 返回禁止访问（403）提示账号已被禁用

Feature: 修改用户资料
  @ACC-S4 @auto:e2e
  Scenario: 修改昵称与手机号
    When 提交新昵称与新手机号
    Then 操作成功且用户态缓存已失效
    And 再次查询时昵称与手机号为新值

  @ACC-S5 @manual
  Scenario: 昵称传空白时不覆盖
    Given alice 当前昵称为旧值
    When 提交仅含空白字符的昵称
    Then 操作成功且昵称保持旧值

  @ACC-S6 @manual
  Scenario: 手机号的空值语义
    When 提交手机号为 null
    Then 操作成功且手机号保持原值
    When 提交手机号为空字符串
    Then 操作成功且手机号被清空

Feature: 自助修改密码
  Background:
    Given alice 已登录
    And 新旧密码均已用服务器公钥加密

  @ACC-S7 @auto:e2e
  Scenario: 正确旧密码换新密码
    When 提交正确旧密码与合规新密码
    Then 操作成功且用户态缓存已失效
    And 新密码可登录、旧密码登录失败

  @ACC-S8 @auto:e2e
  Scenario: 旧密码错误
    When 提交错误旧密码与合规新密码
    Then 返回请求错误（400）提示原密码错误
    And 原密码仍然有效

  @ACC-S9 @auto:e2e
  Scenario: 旧密码不是合法密文
    When 提交的旧密码不是合法加密密文
    Then 返回请求错误（400）提示原密码错误

  @ACC-S10 @auto:e2e
  Scenario: 新密码强度不足
    When 提交正确旧密码与弱密码 "1234"
    Then 返回请求错误（400）提示密码需 8-20 长度且包含数字+字母
    And 原密码仍然有效

Feature: 更新头像
  @ACC-S11 @auto:e2e
  Scenario: 头像对象键为空
    When 提交空的对象键
    Then 返回请求错误（400）提示头像文件不能为空

  @ACC-S12 @auto:e2e
  Scenario: 伪造未完成上传的对象键
    When 提交文件表中不存在的对象键
    Then 返回未找到（404）提示头像文件不存在或未完成上传

  @ACC-S13 @manual
  Scenario: 登记已上传的头像
    Given 已通过存储预上传与直传取得对象键
    When 提交该对象键
    Then 操作成功且用户信息中的头像指向新地址
    And 用户态缓存已失效

Feature: 绑定第三方账号（授权码模式）
  Background:
    Given alice 已登录且系统配置了第三方认证方式

  @ACC-S14 @auto:e2e
  Scenario: 绑定成功
    When 以第三方回调的真实授权码发起绑定
    Then 操作成功且绑定列表出现该认证方式

  @ACC-S15 @manual
  Scenario: 重复绑定同一第三方账号（幂等）
    Given alice 已绑定该第三方身份
    When 再次完成同一绑定流程
    Then 操作成功且不产生重复记录

  @ACC-S16 @manual
  Scenario: 第三方账号已被其它用户绑定
    Given 该第三方身份已绑定到 bob
    When alice 发起绑定
    Then 返回请求错误（400）提示第三方账号已被其它账号绑定

  @ACC-S17 @manual
  Scenario: 同一认证方式下换绑不同账号
    Given alice 在该认证方式下已绑定另一第三方身份
    When alice 以新第三方身份发起绑定
    Then 返回请求错误（400）提示用户已绑定过其它账号

  @ACC-S18 @manual
  Scenario: 第三方接口故障
    When 提交无效授权码
    Then 返回服务器错误（500）提示第三方接口错误请联系管理员

Feature: 绑定第三方账号（临时标识模式）
  @ACC-S19 @manual
  Scenario: 用登录流程留下的临时绑定标识绑定
    Given alice 在登录页用未绑定第三方账号授权，产生 10 分钟有效的临时绑定标识
    And alice 已登录主站
    When 提交该临时绑定标识
    Then 操作成功且绑定关系建立

  @ACC-S20 @auto:e2e
  Scenario: 临时绑定标识过期或不存在
    When 提交不存在的临时绑定标识
    Then 返回禁止访问（403）提示第三方授权跳转登录已过期

Feature: 绑定列表与解绑
  @ACC-S21 @auto:e2e
  Scenario: 查询绑定列表
    Given alice 已绑定一个第三方认证方式
    When 请求绑定列表
    Then 返回各项含 oAuthId/name/provider/iconUrl/createTime
    When 未绑定任何认证方式的用户请求
    Then 返回空列表

  @ACC-S22 @auto:e2e
  Scenario: 解绑已绑定的认证方式
    When 解绑已绑定的认证方式
    Then 操作成功且绑定列表不再包含该项

  @ACC-S23 @auto:e2e
  Scenario: 解绑未绑定的认证方式
    When 解绑从未绑定过的认证方式
    Then 返回未找到（404）提示未绑定该第三方账号

Feature: 前端账号设置页（/account）
  @ACC-S24 @manual
  Scenario: 修改基本资料
    When 修改昵称保存
    Then 提示保存成功且顶部用户信息同步刷新

  @ACC-S25 @manual
  Scenario: 自助改密
    When 填写旧密码与两次一致的新密码并提交
    Then 提示成功且表单重置
    And 退出后用新密码可登录

  @ACC-S26 @manual
  Scenario: 上传头像
    When 选择小于 5MB 的图片上传
    Then 头像立即更新
    When 选择超过 5MB 或非图片文件
    Then 前端拦截并提示，不发起上传

  @ACC-S27 @manual
  Scenario: 绑定第三方（弹窗流）
    When 点击认证方式的绑定按钮
    Then 打开授权弹窗，授权完成后弹窗自动关闭并提示成功、刷新列表
    And 授权失败时主页面展示后端错误信息

  @ACC-S28 @manual
  Scenario: 解绑第三方
    When 点击已绑定项的解绑并确认
    Then 提示成功且该项恢复为绑定按钮
```
