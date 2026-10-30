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
    And 返回全局回调地址 redirectUri（{WebUI}/oauth_login），管理员需将其填到提供方后台的重定向/回调配置中放通

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

  @OC-S26 @manual
  Scenario: 创建企业微信连接器
    When 提交 { name, provider: "weixinWork", key(企业 CorpID), secret, iconUrl, agentId }
    Then 操作成功
    And 授权地址为内置的 https://login.work.weixin.qq.com/wwlogin/sso/login
    And 列表中回显 agentId

  @OC-S27 @manual
  Scenario: 企业微信缺少 AgentId 被拒
    When 提交 provider: "weixinWork" 且 agentId 为空
    Then 返回请求错误（400）"企业微信应用 AgentId 不能为空."

  @OC-S28 @manual
  Scenario: 更新企业微信连接器
    When 修改 agentId 并保存（secret 留空）
    Then 操作成功且 agentId 已更新，secret 保持原值
    And 登录页跳转地址中的 agentid 参数同步为新值

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
    And 每行展示回调地址（{WebUI}/oauth_login，可复制），而非提供方授权端点
    And 每行提供 编辑 / 删除（二次确认）操作

  @OC-S22 @manual
  Scenario: 新建表单按提供商联动
    When 选择 provider 为 feishu 或 dingTalk
    Then 隐藏"发现端点"输入并自动填充默认图标
    And 提供商下方展示回调地址提示（完整 `{WebUI}/oauth_login`，可复制）
    When 选择 weixinWork
    Then 隐藏"发现端点"输入并展示"应用 AgentId"必填项（占位 `1000002`，附填写指引）
    And 回调提示切换为企业微信「授权回调域」只需填域名部分（复制仅复制域名）
    When 选择 custom
    Then "发现端点"必填，输入框占位提示 `https://.../.well-known/openid-configuration`

  @OC-S23 @manual
  Scenario: 编辑时提供商锁定
    When 打开编辑弹窗
    Then Provider 下拉被禁用
    And Secret 留空表示保持不变

  @OC-S24 @manual
  Scenario: 普通用户访问被重定向
    Given member 登录
    When 直接访问 /oauthconnect
    Then 重定向到 /apps 应用广场（接口层同时返回 403）

  @OC-S25 @manual
  Scenario: 图标上传带正方形提示与裁剪
    Given admin 打开新建/编辑连接器表单
    When 查看图标项
    Then 图标预览右侧提示"登录页图标按正方形展示，建议上传 1:1 正方形图片"
    When 点击上传图标并选择本地图片
    Then 弹出正方形裁剪窗（初始完整显示并居中，全向拖动定位，滑块/滚轮缩放：放大可铺满视口、缩小查看全图）
    And 确认后按视口内容导出正方形 PNG（未铺满处留白透明）并上传，回填图标地址
    And 取消/关闭裁剪窗时不产生上传

Feature: 渠道用户识别联动（绑定记录增强与企微原生登录）
  渠道对话反查 user_oauth_connection 归属用户的前提：绑定记录尽量多存跨应用一致的标识。飞书绑定时同时记录 union_id（租户级唯一，跨应用一致）；企业微信连接器登录改走原生接口（sub=userid，与渠道回调 FromUserName 同源）。

  @OC-S29 @manual
  Scenario: 飞书绑定记录 union_id
    Given 用户以飞书第三方登录注册或绑定账号
    When 换取用户资料成功（资料含 open_id 与 union_id）
    Then 绑定记录同时落 sub=open_id 与 union_id
    And 同一绑定再次登录仍按 sub 匹配（登录匹配逻辑不变）

  @OC-S30 @manual
  Scenario: 企业微信连接器走原生登录
    Given 存在 provider=weixinWork 的连接器
    When 用户以该连接器发起第三方登录并携带有效 code
    Then 平台调企业微信接口换取 userid（sub=userid），与机器人回调的 FromUserName 同源
    And 渠道对话可凭 userid 反查绑定归属其账号
