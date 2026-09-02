# 前端账号设置页（AccountSettings）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md) 第 3 节。本页无组件自动化，全部为浏览器走查场景；后端规则以 [../../../docs/account/bdd.md](../../../docs/account/bdd.md) 为准。

```gherkin
Feature: 页面进入与资料卡
  @FE-PA-S1 @manual
  Scenario: 从侧边栏进入账号设置
    Given 用户已登录
    When 点击侧边栏头像下拉菜单「设置」
    Then 进入账号设置页
    And 资料卡显示头像（无头像时显示首字符回退）、昵称/用户名、邮箱/用户名

  @FE-PA-S2 @manual
  Scenario: 未登录访问被拦截
    Given 浏览器无登录态
    When 直接访问账号设置页地址
    Then 被登录守卫拦截并跳转登录页

Feature: 修改基本资料
  @FE-PA-S3 @manual
  Scenario: 修改昵称与手机号
    Given 表单已回填当前昵称与手机号
    When 修改昵称为"新昵称"、手机号为"13800138000"并保存
    Then 提示保存成功
    And 侧边栏与资料卡的昵称同步更新

  @FE-PA-S4 @manual
  Scenario: 昵称为空被拦截
    Given 基本资料表单已打开
    When 清空昵称直接提交
    Then 表单就地提示必填错误
    And 不发起请求

  @FE-PA-S5 @manual
  Scenario: 手机号格式非法
    Given 基本资料表单已打开
    When 填写手机号 "abc!!"
    Then 表单就地提示格式错误
    And 不发起请求

Feature: 修改密码
  @FE-PA-S6 @manual
  Scenario: 自助改密成功
    Given 用户已登录且改密表单已打开
    When 填写旧密码、新密码 "NewPass123" 与一致的确认密码并提交
    Then 密码以服务器公钥加密传输，明文不出浏览器
    And 成功后表单清空并提示成功
    And 用户可用新密码重新登录且旧密码登录失败

  @FE-PA-S7 @manual
  Scenario: 新密码强度不足
    Given 改密表单已打开
    When 新密码填 "1234"（无字母或不足 8 位）
    Then 表单就地提示强度规则
    And 不发起请求

  @FE-PA-S8 @manual
  Scenario: 两次新密码不一致
    Given 改密表单已填入合规新密码
    When 确认密码与新密码不同
    Then 表单就地提示不一致
    And 不发起请求

  @FE-PA-S9 @manual
  Scenario: 旧密码错误
    Given 用户已登录且改密表单已打开
    When 旧密码填写错误提交
    Then 全局错误提示出现
    And 表单保留已填内容

Feature: 头像上传
  @FE-PA-S10 @manual
  Scenario: 上传合法图片
    Given 资料卡上传入口可用
    When 选择一张 5MB 以内的图片上传
    Then 文件经存储服务直传并登记对象键
    And 页面头像即时更新
    And 不发起秒传以外的重复上传

  @FE-PA-S11 @manual
  Scenario: 秒传命中
    Given 同一内容文件已在存储中
    When 再次上传该文件
    Then 跳过直传与完成回调，直接登记对象键并刷新头像

  @FE-PA-S12 @manual
  Scenario: 非图片文件被拦截
    Given 资料卡上传入口可用
    When 选择 .txt 文件
    Then 提示类型错误
    And 不进入上传流程

  @FE-PA-S13 @manual
  Scenario: 超大文件被拦截
    Given 资料卡上传入口可用
    When 选择大于 5MB 的图片
    Then 提示超出大小限制
    And 不进入上传流程

Feature: 第三方账号绑定
  @FE-PA-S14 @manual
  Scenario: 查看绑定列表
    Given 系统已配置至少一个第三方登录渠道
    When 进入账号设置页
    Then 第三方账号卡按提供商逐行显示图标、名称、绑定状态
    And 已绑定行显示「解绑」危险按钮，未绑定行显示「绑定」主按钮

  @FE-PA-S15 @manual
  Scenario: 无任何提供商的空态
    Given 系统未配置任何第三方渠道
    When 进入账号设置页
    Then 第三方账号卡显示空态文案

  @FE-PA-S16 @manual
  Scenario: 发起绑定
    Given 列表中存在未绑定的提供商
    When 点击其「绑定」按钮
    Then 以绑定模式打开第三方授权弹窗
    And 弹窗进入第三方授权页

  @FE-PA-S17 @manual
  Scenario: 绑定成功回调
    Given 用户在弹窗完成第三方授权
    When 弹窗报告绑定成功
    Then 本页提示绑定成功并刷新绑定列表

  @FE-PA-S18 @manual
  Scenario: 绑定失败回调
    Given 绑定弹窗已打开并回传结果
    When 弹窗报告绑定失败及原因
    Then 本页提示失败原因（无原因时用默认文案）

  @FE-PA-S19 @manual
  Scenario: 跨源消息被忽略
    Given 账号设置页已打开
    When 非同源窗口向本页发送任意消息
    Then 本页不做任何处理

  @FE-PA-S20 @manual
  Scenario: 解绑
    Given 某提供商已绑定
    When 点击「解绑」并在二次确认框确认
    Then 解绑成功并刷新列表
    And 该行变为「绑定」按钮
```
