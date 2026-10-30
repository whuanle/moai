# OAuth 连接器（OauthConnect，OC）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@OC-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../auth/sdd.md](../auth/sdd.md)（登录侧消费方） ｜ 证据：[local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

为**管理员**提供第三方 OAuth 2.0 连接器（自定义 OIDC / 飞书 / 钉钉 / 企业微信）的增删改查：维护应用 Key/Secret、图标、发现端点（WellKnown）；Custom 连接器在创建/更新时经 Refit 动态客户端实时拉取 OIDC 发现文档解析授权端点（AuthorizeUrl）。本模块只管理 `oauth_connection` 表；登录/注册流程由 auth 模块 `oauth_login` / `oauth_register` / `oauth_prividers` 消费（见「与 auth 的关系」）。

## 角色与门禁

| 角色 | 判定 | 能力 |
|---|---|---|
| admin（含 root） | `user.IsAdmin == true` | 连接器增删改查 |
| member | 其余 | 接口 403，页面重定向 /apps 应用广场 |

门禁在 Controller 层 `EnsureAdminAsync`（读 `IUserAccountService.GetUserStateAsync` 的 IsAdmin），不下沉 Handler（[@OC-S1](./bdd.md#oc-s1)）。

## 数据模型

- `oauth_connection`（`OauthConnectionEntity`，IFullAudited 软删除）：Id(Guid)/Name(唯一性由 Handler 查重)/Provider/Key/Secret/IconUrl/AuthorizeUrl/WellKnown/AgentId(可空，仅企业微信存应用 agentid)。无种子数据（空表起步）。AuthorizeUrl 是**提供方授权端点**（auth 模块拼跳转地址用），对管理员无配置价值；列表展示的是**回调地址** `{WebUI}/oauth_login`（`QueryAllOAuthConnectionCommandResponse.RedirectUri`，来自 SystemOptions），管理员把它填到提供方后台放通。实际登录时回调地址跟随登录页来源（见 [../auth/sdd.md](../auth/sdd.md) 关键决策 4），内网穿透等前端域名 ≠ WebUI 的场景无需改后端配置。
- `user_oauth_connection`：用户绑定（UserId + ProviderId→oauth_connection.Id + Sub + UnionId(可空)）。由 auth/account 模块在注册/绑定时写入：飞书记 Sub=open_id 且 UnionId=union_id（租户级跨应用一致，供渠道对话跨应用识别归属，见 [../feishu/sdd.md](../feishu/sdd.md)）；钉钉记 Sub=union_id；企业微信连接器原生登录记 Sub=userid。唯一键 (provider_id, sub, is_deleted)，附 (provider_id, union_id) 普通索引。
- 枚举 `OAuthPrivider`（历史拼写）：Custom=0、Feishu=1、DingTalk=2、WeixinWork=3。**wire 值由全局 `JsonStringEnumConverter(CamelCase)` 决定**（custom/feishu/dingTalk/weixinWork），成员上的 `[JsonPropertyName]` 对枚举不生效，仅作标注。
- 存量库升级：`EnsureCreated` 不为已存在的表补列，`DatabasPostgresModule` 启动时执行幂等 `ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS agent_id`（2026-10-27 后加列）、`ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS union_id` + union_id 索引（2026-10-28 后加列）。存量绑定无 union_id（无法离线回填），重新绑定一次即获得。

## 组件

```
src/oauthconnect/
├── MoAI.OauthConnect.Shared/   Commands/{Create,Update,Delete}OAuthConnectionCommand
│                               Queries/QueryAllOAuthConnectionCommand(+Responses)
├── MoAI.OauthConnect.Core/     3 个 CommandHandler + QueryAllOAuthConnectionCommandHandler
└── MoAI.OauthConnect.Api/      OauthConnectController（/oauthconnect/connections，GET/POST/PUT/DELETE）
src/infra/MoAI.Infra.ExternalHttp/OAuth/   IOAuthClient(Factory)：动态 BaseAddress 的 OIDC 客户端
ui/src/                          api/oauthconnect.ts、pages/oauthconnect/OauthConnect.tsx、路由 /oauthconnect
```

## 关键决策

1. **校验**（IModelValidator）：Name 非空 ≤50；Provider 合法枚举；Key/Secret/IconUrl 非空；Custom 时 WellKnown 必填；weixinWork 时 AgentId 必填 ≤100。
2. **名称唯一**：Create 查重含软删除记录；Update 仅改名时排除自身查重，冲突 400。
3. **AuthorizeUrl 推导**（Create/Update 共用）：Custom 经 `IOAuthClientFactory.Create(wellKnown 的 Authority)` 拉发现文档取 `authorization_endpoint`（真实外部 HTTP，失败 500，见已知问题）；Feishu 固定 `https://accounts.feishu.cn/open-apis/authen/v1/authorize`、WellKnown 默认 `https://open.feishu.cn`；DingTalk 固定 `https://login.dingtalk.com/oauth2/auth`；WeixinWork 固定 `https://login.work.weixin.qq.com/wwlogin/sso/login`、WellKnown 默认 `https://work.weixin.qq.com`。
4. **Update 语义**：Secret 为空保持原值；切换内置 Provider 重置内置 WellKnown/AuthorizeUrl；路由 id 回填 `OAuthConnectionId`（其校验规则已移除，见修复史）。
5. **Delete 软删除**：保留 `user_oauth_connection` 历史绑定；记录不存在 400。
6. **Query 全量**：返回 `IsDeleted==0`，不分页。
7. **登录取资料按 Provider 分发**（auth 侧 `OAuthUserProfileService`）：Feishu 走 authen 接口（Sub=open_id，同时带出 union_id）、DingTalk 走 contact 接口（Sub=union_id）、**WeixinWork 走原生 qyapi 接口（Sub=userid）**——企微原生路由 2026-10-28 前未接线（落入 custom OIDC 分支而企业微信无标准 OIDC 发现端点，登录实际不可用），接线后绑定标识与渠道回调 FromUserName 同源；Custom 走 WellKnown OIDC。Sub 语义见数据节。
7. 前端：非 admin 重定向 /apps 应用广场；Provider 编辑锁定；飞书/钉钉隐藏发现端点并自动填充默认图标；企业微信隐藏发现端点、展示 AgentId 必填项（无稳定公共图标源，不做默认图标，管理员上传）；列表「回调地址」列与新建/编辑弹窗提供商下方常驻回调提示——企微「授权回调域」提示只填域名部分（复制仅复制域名），其余提供商提示完整 `{WebUI}/oauth_login`；图标经 `IconPicker`（URL 或上传 ObjectKey，见 [../storage/sdd.md](../storage/sdd.md)）。登录页第三方图标按正方形展示：图标项右侧常驻方形提示，上传本地图片先经 `ImageSquareCropModal`（`ui/src/components/`）正方形裁剪——初始完整显示并居中，全向拖动 + 滑块/滚轮缩放（放大铺满视口紧致裁剪、缩小查看全图，未铺满处导出透明留白），Canvas 导出 ≤512px PNG 后走 `uploadImage`，手动填 URL 不裁剪（[@OC-S25](./bdd.md#oc-s25)）。

## 与 auth 的关系（消费方契约）

- `GET /auth/oauth_prividers`：按提供商拼跳转地址，`state={OAuthId}`、`redirect_uri={WebUI}/oauth_login`；默认 OAuth 用 `client_id/response_type/scope`；企业微信用 `login_type=CorpApp&appid={Key}&agentid={AgentId}`（企业微信 Web 登录要求 redirect_uri 域名在应用可信域名内，不要求精确回跳匹配）；redirectUri 须与 `SystemOptions.WebUI` 同 Host。
- `POST /auth/oauth_login`：按 OAuthId 找连接器 → 换取 Sub → 无绑定则资料写 Redis `oauth:bind:{guid}`（10 分钟）返回 `TempOAuthBindId`。
- `POST /auth/oauth_register`：消费临时绑定建号并写 `user_oauth_connection`；同 Sub 重复注册 409。
- 删除连接器：渠道立即从登录页消失、无法再发起登录；已绑定用户历史保留（[@OC-S19](./bdd.md#oc-s19)）。

## 已知问题

- **修复史**：`PUT /connections/{id}` 曾因 SharpGrip 对路由回填字段自动验证恒 400——**2026-09-02 已修复**（Validate 移除 `OAuthConnectionId` 规则），实测 200（[@OC-S12](./bdd.md#oc-s12)）；第二轮已为各 BusinessException 补 StatusCode（此前裸 500）。
- Custom 发现端点不可达/格式非法时抛 Refit 异常 → 500，未转业务异常（[@OC-S11](./bdd.md#oc-s11)）。
- 拼写历史遗留：枚举 `OAuthPrivider`、auth 端点 `oauth_prividers`（少 i），文档与代码保持一致。
- 企业微信客户端修正史（2026-10-27）：`IWeixinWorkClient` 此前注册但从未被消费，getuserinfo/getuserdetail 旧签名（query 传参 + camelCase 模型）与真实 API（`access_token` query + snake_case JSON + code 走请求体）不符，接入登录时一并修正并补 `GET /cgi-bin/user/get`。
