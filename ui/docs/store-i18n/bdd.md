# 前端状态管理与 i18n（Store & i18n）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 主题状态与持久化
  @FE-SI-S1 @manual
  Scenario: 首次访问跟随系统深色偏好
    Given 浏览器 localStorage 无主题持久化键
    When 系统开启深色偏好的用户打开页面
    Then 全局主题初始为暗色
    And antd 组件以暗色算法渲染

  @FE-SI-S2 @manual
  Scenario: 手动切换主题并持久化
    Given 当前为亮色主题
    When 用户在侧边栏底部主题选择器选择"暗色"
    Then 主题持久化键写入 "dark"
    And 全站 antd 组件即时切换为暗色算法

  @FE-SI-S3 @manual
  Scenario: 刷新后恢复上次选择
    Given 主题持久化键已存 "dark"
    When 用户刷新页面
    Then 主题保持暗色
    And 系统偏好不再参与判定

  @FE-SI-S4 @auto:vitest
  Scenario: 亮暗双主题配置可用
    Given 主题配置模块已内置亮暗两套预设
    When 分别以亮色与暗色取主题配置
    Then 两套 antd 主题配置均可获得
    And 主题键类型受限于合法取值

Feature: 语言状态与切换链路
  @FE-SI-S5 @manual
  Scenario: 默认中文
    Given 语言持久化键为空
    When 用户首次打开页面
    Then 界面文案为中文
    And <html lang> 与 antd 内建文案均为中文

  @FE-SI-S6 @manual
  Scenario: 切换为英文
    Given 界面当前为中文
    When 用户在侧边栏语言选择器选择 "English"
    Then 语言持久化键写入 "en-US"
    And 全部界面文案切换为英文
    And <html lang> 与 antd 内建文案（分页、弹窗按钮等）同步为英文

  @FE-SI-S7 @manual
  Scenario: 缺失词条回退
    Given 英文语言包缺少某键而中文包存在
    When 以英文渲染该键
    Then 显示中文回退词条
    And 不出现裸键名或空白

  @FE-SI-S8 @manual
  Scenario: 刷新后保持语言
    Given 语言持久化键已存 "en-US"
    When 用户刷新页面
    Then 界面仍为英文

Feature: 服务器信息缓存
  @FE-SI-S9 @manual
  Scenario: 登录前获取并缓存服务器信息
    Given 全局状态中无服务器信息
    When 登录页获取服务器信息
    Then 请求公共服务器信息接口并写入全局状态
    And 该信息随持久化快照落盘

  @FE-SI-S10 @manual
  Scenario: 后续请求复用缓存
    Given 服务器信息已缓存
    When 再次获取服务器信息
    Then 不发起网络请求
    And 直接返回缓存值

  @FE-SI-S11 @manual
  Scenario: RSA 公钥供密码加密
    Given 服务器公钥已缓存
    When 用户提交登录/注册/改密
    Then 密码以该公钥加密后上报
    And 明文密码不出浏览器

  @FE-SI-S12 @manual
  Scenario: 头像与图标地址解析
    Given 服务器地址已缓存且头像为存储对象键（非绝对地址）
    When 页面解析头像地址
    Then 拼接为「服务器地址 + 静态资源路径 + 对象键」的展示地址

Feature: 登录用户态生命周期
  @FE-SI-S13 @manual
  Scenario: 登录写入
    Given 浏览器当前无登录态
    When 账号密码或第三方登录成功
    Then 令牌与用户标识写入全局状态并持久化
    And 后续接口请求自动携带 Bearer 令牌

  @FE-SI-S14 @manual
  Scenario: 档案合并刷新
    Given 用户已登录
    When 页面刷新用户档案
    Then 返回的档案与用户态字段和现有令牌字段合并后整体写回
    And 侧边栏等消费处即时更新

  @FE-SI-S15 @manual
  Scenario: 401 清理登录态
    Given 用户已登录
    When 任一非登录接口返回 401
    Then 登录态被清空
    And 浏览器重定向到登录页

  @FE-SI-S16 @manual
  Scenario: 退出登录
    Given 用户已登录
    When 用户点击侧边栏用户菜单"退出登录"
    Then 登录态被清空
    And 跳转到登录页

Feature: 访问约定
  @FE-SI-S17 @auto:vitest
  Scenario: 非 React 上下文读写全局状态
    Given API 层或测试需要读写令牌/服务器信息
    When 在组件外访问全局状态
    Then 使用一次性快照读取或整体写入
    And 不建立订阅

  @FE-SI-S18 @auto:vitest
  Scenario: 组件内按选择器订阅
    Given 组件只依赖语言字段
    When 其他无关字段发生变化
    Then 该组件不重渲染
    And 角色差异化的界面按订阅值正确呈现
```
