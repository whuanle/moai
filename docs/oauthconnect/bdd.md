# OAuth 连接器（OauthConnect，OC）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 管理门禁
  @OC-S1 @manual
  Scenario: 普通用户无权管理连接器
    Given member 已登录
    When member 请求连接器的列表/新建/更新/删除
    Then 返回没有权限（403）"只有管理员可以管理第三方登录"

  @OC-S2 @manual
  Scenario: 未登录访问
    When 不带 token 请求连接器列表
    Then 返回未认证（401）

Feature: 查询连接器列表
  Background:
    Given admin 已登录
    And 系统中存在连接器 GitHub、Feishu

  @OC-S3 @auto:e2e
  Scenario: 管理员查询全部连接器
    When 请求连接器列表
    Then 返回全部未删除的连接器（不分页）
    And 每项含 id/name/iconUrl/provider/key/wellKnown/authorizeUrl 及审计字段

  @OC-S4 @auto:e2e
  Scenario: 已软删除的连接器不出现在列表
    Given 连接器 "audit" 已被删除
    When 请求连接器列表
    Then 结果中不包含该连接器

Feature: 创建连接器
  Background:
    Given admin 已登录

  @OC-S5 @auto:e2e
  Scenario: 创建飞书连接器
    When 提交 { name, provider: "feishu", key, secret, iconUrl }
    Then 操作成功
    And 授权地址为内置的 https://accounts.feishu.cn/open-apis/authen/v1/authorize
    And 列表中可见该连接器

  @OC-S6 @manual
  Scenario: 创建钉钉连接器
    When 提交 { name, provider: "dingtalk", key, secret, iconUrl }
    Then 操作成功
    And 授权地址为内置的 https://login.dingtalk.com/oauth2/auth

  @OC-S7 @manual
  Scenario: 创建 Custom 提供商连接器
    Given wellKnown 指向可访问的 OIDC 发现文档
    When 提交 { name, provider: "custom", key, secret, iconUrl, wellKnown }
    Then 操作成功
    And 授权地址为发现文档解析出的 authorization_endpoint

  @OC-S8 @auto:e2e
  Scenario: 认证名称重复被拒
    Given 已存在同名连接器
    When 再提交相同 name
    Then 返回请求错误（400）"认证名称已存在，请更换后重试."

  @OC-S9 @manual
  Scenario: Custom 缺少发现端点被拒
    When 提交 provider: "custom" 且 wellKnown 为空
    Then 返回请求错误（400）"发现端点不能为空."

  @OC-S10 @manual
  Scenario: 字段校验失败
    When 提交 name 为空（或超 50 字、provider 非法、key/secret/iconUrl 为空）
    Then 返回请求错误（400）且提示对应校验消息

  @OC-S11 @manual
  Scenario: 发现端点不可达
    When 提交 provider: "custom" 且 wellKnown 指向无法访问的地址
    Then 返回服务器错误 500（缺陷记录：外部 HTTP 失败未转业务异常）

Feature: 更新连接器
  Background:
    Given admin 已登录
    And 连接器 "audit" 已存在

  @OC-S12 @auto:e2e
  Scenario: 更新连接器
    When 提交新 name/key/iconUrl
    Then 操作成功（已修复：2026-09-02 前曾因路由参数自动验证恒 400，实测 200）

  @OC-S13 @manual
  Scenario: Secret 为空保持不变
    When 更新时 secret 提交为空
    Then key/iconUrl 已更新而 secret 保持原值

  @OC-S14 @manual
  Scenario: 名称查重仅发生在改名时
    When 将名称改为其他连接器已占用的名称
    Then 返回请求错误（400）"认证名称已存在，请更换后重试."
    When 保持名称不变仅更新其他字段
    Then 操作成功（不做名称查重）

  @OC-S15 @manual
  Scenario: 更新不存在的连接器被拒
    When 提交 id 为全零 Guid
    Then 返回请求错误（400）"未找到认证方式，请检查名称是否正确."

Feature: 删除连接器
  Background:
    Given admin 已登录

  @OC-S16 @auto:e2e
  Scenario: 删除连接器为软删除
    When 删除连接器 "audit"
    Then 操作成功且列表中不再出现（IsDeleted=1）
    And 历史用户绑定记录 user_oauth_connection 仍保留

  @OC-S17 @manual
  Scenario: 删除不存在的连接器被拒
    When 删除 id 为全零 Guid
    Then 返回请求错误（400）"未找到认证方式，请检查名称是否正确."

Feature: 登录侧联动（上游 auth 消费，见 ../auth/bdd.md）
  @OC-S18 @manual
  Scenario: 登录页展示已配置渠道
    Given 存在未删除的连接器
    When 请求登录渠道列表
    Then 返回各连接器的 key/oauthId/name/iconUrl/provider 及拼装好的跳转地址

  @OC-S19 @manual
  Scenario: 删除后渠道从登录页消失
    Given 连接器已被软删除
    When 请求登录渠道列表
    Then 该连接器不在返回结果中，无法再发起该渠道登录

  @OC-S20 @manual
  Scenario: 未绑定用户的 OAuth 登录走临时绑定
    When 以第三方身份登录且该 Sub 无绑定记录
    Then 返回 isBindUser=false 与 tempOAuthBindId（10 分钟内有效）
    And 完成注册后自动建号、写入绑定并直接返回登录 token
    And 同一 Sub 再次注册返回冲突（409）

Feature: 前端连接器页（/oauthconnect）
  @OC-S21 @manual
  Scenario: 管理员看到连接器表格
    Given admin 登录并进入 /oauthconnect
    Then 展示连接器表格（刷新 + 新建按钮）
    And 每行提供 编辑 / 删除（二次确认）操作

  @OC-S22 @manual
  Scenario: 新建表单按提供商联动
    When 选择 provider 为 feishu 或 dingTalk
    Then 隐藏"发现端点"输入并自动填充默认图标
    When 选择 custom
    Then "发现端点"必填

  @OC-S23 @manual
  Scenario: 编辑时提供商锁定
    When 打开编辑弹窗
    Then Provider 下拉被禁用
    And Secret 留空表示保持不变

  @OC-S24 @manual
  Scenario: 普通用户访问被重定向
    Given member 登录
    When 直接访问 /oauthconnect
    Then 重定向到 /dashboard（接口层同时返回 403）
```
