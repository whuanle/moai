# OAuth 连接器（OauthConnect，OC）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@OC-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../auth/sdd.md](../auth/sdd.md)（登录侧消费方） ｜ 证据：[local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

为**管理员**提供第三方 OAuth 2.0 连接器（自定义 OIDC / 飞书 / 钉钉）的增删改查：维护应用 Key/Secret、图标、发现端点（WellKnown）；Custom 连接器在创建/更新时经 Refit 动态客户端实时拉取 OIDC 发现文档解析授权端点（AuthorizeUrl）。本模块只管理 `oauth_connection` 表；登录/注册流程由 auth 模块 `oauth_login` / `oauth_register` / `oauth_prividers` 消费（见「与 auth 的关系」）。

## 角色与门禁

| 角色 | 判定 | 能力 |
|---|---|---|
| admin（含 root） | `user.IsAdmin == true` | 连接器增删改查 |
| member | 其余 | 接口 403，页面重定向 /dashboard |

门禁在 Controller 层 `EnsureAdminAsync`（读 `IUserAccountService.GetUserStateAsync` 的 IsAdmin），不下沉 Handler（[@OC-S1](./bdd.md#oc-s1)）。

## 数据模型

- `oauth_connection`（`OauthConnectionEntity`，IFullAudited 软删除）：Id(Guid)/Name(唯一性由 Handler 查重)/Provider/Key/Secret/IconUrl/AuthorizeUrl/WellKnown。无种子数据（空表起步）。
- `user_oauth_connection`：用户绑定（UserId + ProviderId→oauth_connection.Id + Sub），由 auth 模块写入，本模块不操作。
- 枚举 `OAuthPrivider`（历史拼写）：Custom=0("custom")、Feishu=1("feishu")、DingTalk=2("dingtalk")。
- 相关设置项：`setting.key="oauth_auto_register"`（默认 false，见 [../settings.md](../settings.md)）。

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

1. **校验**（IModelValidator）：Name 非空 ≤50；Provider 合法枚举；Key/Secret/IconUrl 非空；Custom 时 WellKnown 必填。
2. **名称唯一**：Create 查重含软删除记录；Update 仅改名时排除自身查重，冲突 400。
3. **AuthorizeUrl 推导**（Create/Update 共用）：Custom 经 `IOAuthClientFactory.Create(wellKnown 的 Authority)` 拉发现文档取 `authorization_endpoint`（真实外部 HTTP，失败 500，见已知问题）；Feishu 固定 `https://accounts.feishu.cn/open-apis/authen/v1/authorize`、WellKnown 默认 `https://open.feishu.cn`；DingTalk 固定 `https://login.dingtalk.com/oauth2/auth`。
4. **Update 语义**：Secret 为空保持原值；切换内置 Provider 重置内置 WellKnown/AuthorizeUrl；路由 id 回填 `OAuthConnectionId`（其校验规则已移除，见修复史）。
5. **Delete 软删除**：保留 `user_oauth_connection` 历史绑定；记录不存在 400。
6. **Query 全量**：返回 `IsDeleted==0`，不分页。
7. 前端：非 admin 重定向 /dashboard；Provider 编辑锁定；飞书/钉钉隐藏发现端点并自动填充默认图标；图标经 `IconPicker`（URL 或上传 ObjectKey，见 [../storage/sdd.md](../storage/sdd.md)）。

## 与 auth 的关系（消费方契约）

- `GET /auth/oauth_prividers`：按 `{AuthorizeUrl}?client_id={Key}&response_type=code&scope=openid%20profile&state={OAuthId}&redirect_uri={WebUI}/oauth_login` 拼跳转地址；redirectUri 须与 `SystemOptions.WebUI` 同 Host。
- `POST /auth/oauth_login`：按 OAuthId 找连接器 → 换取 Sub → 无绑定则资料写 Redis `oauth:bind:{guid}`（10 分钟）返回 `TempOAuthBindId`。
- `POST /auth/oauth_register`：消费临时绑定建号并写 `user_oauth_connection`；同 Sub 重复注册 409。
- 删除连接器：渠道立即从登录页消失、无法再发起登录；已绑定用户历史保留（[@OC-S19](./bdd.md#oc-s19)）。

## 已知问题

- **修复史**：`PUT /connections/{id}` 曾因 SharpGrip 对路由回填字段自动验证恒 400——**2026-09-02 已修复**（Validate 移除 `OAuthConnectionId` 规则），实测 200（[@OC-S12](./bdd.md#oc-s12)）；第二轮已为各 BusinessException 补 StatusCode（此前裸 500）。
- Custom 发现端点不可达/格式非法时抛 Refit 异常 → 500，未转业务异常（[@OC-S11](./bdd.md#oc-s11)）。
- 拼写历史遗留：枚举 `OAuthPrivider`、auth 端点 `oauth_prividers`（少 i），文档与代码保持一致。
