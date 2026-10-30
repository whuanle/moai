# 子系统接入（Subsystem，SUB）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@SUB-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)（存量库升级与对接指引） ｜ 上游：[../auth/sdd.md](../auth/sdd.md)（内部 JWT）、[../gateway/external-api-scope-system.md](../gateway/external-api-scope-system.md)（后续能力面扩展） ｜ 证据：[local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

让**独立部署的外部子系统**（如智能报表）以 `appid + 密钥` 注册并接入 MoAI：root 创建子系统并发放一次性密钥；子系统匿名换取短时机器面 JWT、心跳保活（在线状态）；团队授权控制"哪些团队可使用该子系统"（全员可用 / 指定团队）。用户体系由 MoAI 承载，子系统不自建用户表。

**本期范围**：注册、接入鉴权、心跳在线、团队授权、委托 SSO 登录。**不含**：AI 能力面（模型/插件/知识库复用）、临时 MCP 注入、OSS 代理授权——留待后续轮次（见「后续规划」）。

## 角色与门禁

| 角色 | 判定 | 能力 |
|---|---|---|
| root | `UserStateInfo.IsRoot` | 子系统增删改查、重置密钥、团队授权 |
| 普通用户 | 其余 | 管理面 403，页面重定向 /apps |
| 子系统（机器） | 机器面 JWT（audience=`Server\|subsystem`） | 心跳、info 自省 |
| 团队成员 | `ITeamService.GetMyRoleAsync != null` | 查询本团队可用子系统 |

管理面门禁在 Controller 层 `EnsureRootAsync`；团队归属门禁在 Handler（[@SUB-S1](./bdd.md#sub-s1)、[@SUB-S25](./bdd.md#sub-s25)）。

## 数据模型

- `subsystem`（`SubsystemEntity`，IFullAudited 软删除）：Id(Guid v7)/Key(appid，未删除记录内唯一索引)/Name/Description/Secret(PBKDF2 哈希)/SecretSalt/Status(enabled|disabled)/TeamScope(allTeams|specifiedTeams)。
- `subsystem_team_auth`（`SubsystemTeamAuthEntity`）：SubsystemId+TeamId（未删除记录内唯一索引），指定团队范围的授权清单，更新整体覆盖。
- 枚举 `SubsystemStatus`/`SubsystemTeamScope` 带 `[JsonPropertyName]`（enabled/disabled/allTeams/specifiedTeams）。
- `subsystem.CallbackUrl`：委托 SSO 登录回调地址（子系统侧完整 URL），空串=未开放登录接入；回跳仅允许该注册地址（防开放重定向）。
- 存量库升级：执行 [asserts/subsystem.sql](../../asserts/subsystem.sql)（新库由 EnsureCreated 自动建表，含 callback_url 幂等补列），见 [SOP](./sop.md)。

## 组件

```
src/subsystem/
├── MoAI.Subsystem.Shared/  Commands/{Create,Update,Delete,Reset}SubsystemCommand、SubsystemTokenCommand、SubsystemTokenRefreshCommand、SubsystemHeartbeatCommand
│                           Commands/{Create,Update,Delete,Reset}SystemAccessAppCommand、SystemAppTokenCommand(+Refresh)
│                           Queries/{QuerySubsystems,QuerySubsystemAuthorizedTeams,QuerySubsystemInfo,QueryTeamAvailableSubsystems}Command(+Responses)
│                           Services/{SubsystemAuthDefaults,ISubsystemTokenProvider,SubsystemSecretGenerator}
├── MoAI.Subsystem.Core/    Handlers×10、Services/{SubsystemTokenProvider,SubsystemOnlineService}
└── MoAI.Subsystem.Api/     SubsystemController（/subsystem 管理面，root）
                           SystemAccessAppController（/subsystem/system-app 管理面，root）
                           SubsystemOpenController（/subsys/{token,heartbeat,info,sso/*} 子系统协作面，独立文档 /openapi/subsys.json）
                           SystemOpenController（/subsys/system/{info,token,token/refresh} 系统接入协作面）
                           SystemUserController/SystemOrgController/SystemTeamController（/subsys/system/* 系统资源接口，scope 分档）
                           SystemAccessKeyMiddleware（守护 /api/subsys/system/**，appid 直连或 system_app_access JWT 双轨）
                           TeamSubsystemController（/team/{teamId}/subsystem/list 团队面）
                           SubsystemAuthenticationMiddleware（机器面鉴权，Program.cs 挂载于外部接入中间件之后）
ui/src/                     api/subsystem.ts、pages/admin/AdminSubsystems.tsx（/admin/subsystems，root）
SubsystemSsoController（/subsystem/sso/code 用户面，内部 JWT）
tests/MoAI.Subsystem.Tests/ Handler 单测（sqlite 内存库，31 例，含系统接入统一表/密钥哈希）
ui/src/pages/sso/SsoAuthorize.tsx（/sso/authorize 公开跳转页）
```

## 数据：应用接入统一表（access_app，Level 分域）

团队级（moai-ac-）与系统级（moai-sa-）应用接入**合用 `access_app` 表**，`Level`（team=0/system=1）决定资源域：团队级只能访问所属团队资源（/api/external/*、模型网关），系统级只能访问系统资源（/api/subsys/system/*，TeamId=0）。

- 凭据模型（两级一致）：appid（Key，前缀区分层级，明文存储等值命中）+ 密钥（Secret/SecretSalt，PBKDF2 哈希，原文仅创建/重置返回一次）。存量团队接入 Secret 为 null=未设密钥，token 兑换退化为仅 key；已设密钥则兑换必须携带并校验。
- Scopes 一列双枚举位空间隔离：团队级沿用低位（2=wiki_read…1024=app_a2a），系统级占高位（65536=org_read 131072=org_write 262144=user_read 524288=user_write 1048576=team_read 2097152=team_write，枚举 `SystemApiKeyScopes`）。
- 统一带 `Status`（停用即时拒绝）与 `KeyPrefix`（列表回显）。
- 存量库迁移执行 [asserts/access_app_unify.sql](../../asserts/access_app_unify.sql)（补列 + system_access_app 迁行并 scope 位左移 + 删旧表，幂等）。

## 系统资源协作面（/api/subsys/system/*）

`SystemAccessKeyMiddleware` 双轨鉴权：Bearer moai-sa- appid 直连，或 appid+密钥经 `POST /api/subsys/system/token` 换取的 system_app_access JWT（每次请求回库实时取当前 Scopes/Status）；`/token`、`/token/refresh` 匿名放行。资源接口按 `SystemFaceGuard` 做 scope 分档：

- 用户治理（user_read/user_write）：`POST /users` 按 userName 幂等 upsert（未提供密码自动生成 12 位随机初始密码一次性返回，email/phone 缺省生成唯一占位值与 OAuth 注册同口径）、`GET /users|/users/{id}`、`PUT /users/{id}/disable`（root 受保护）。
- 组织角色（org_read/org_write）：角色树查询/增删改、角色用户与用户角色分配——复用 Org 领域命令。
- 团队治理（team_read/team_write）：创建团队（无 Owner，成员另推）、更新、成员幂等 upsert（Admin/Member，重复推送更新角色）、移除（Owner 受保护）、团队列表。

## 关键决策

1. **密钥安全**：48 位随机密钥仅创建/重置时返回一次；库内 PBKDF2 哈希+盐（同用户密码规范），绝不回显、绝不落日志。
2. **机器面 token**：与内部 JWT 同 RSA 钥签发、独立 audience `Server|subsystem` + header `typ=access_token`，与内部/外部 token 双重隔离；DEBUG 7d / RELEASE 2h。**refresh 轮换（@SUB-S45）**：`POST /api/subsys/token/refresh` 以 machine_refresh typ 的 refresh token（7d）轮换新对，免重发密钥；无状态 JWT 不落库，吊销=刷新时回库重验（停用 403/删除 401）；重置密钥不吊销已发 token（与 access 既有语义一致）。
3. **实时吊销**：中间件每次请求按 token 主体回库校验（停用 403、删除/换 key 401），不依赖 token 自然过期；停用同时清除 Redis 在线键。
4. **在线状态**：Redis 在场键 `subsys:online:{id:N}` TTL 60s（心跳覆盖写），列表/自省读键存在性；无 DB 心跳写放大。
5. **团队授权**：`allTeams` 全员可见；`specifiedTeams` 按授权清单，Handler 校验团队存在（400）并整体覆盖保存。可用列表仅返回 enabled 的子系统；root 列表回传 `authorizedTeams`（团队 id+名称，按 id 升序，授权行一次拉全+团队名批量补齐，团队已删除回退 `#id`），前端展示完整团队名称清单。
6. **appid 语义**：root 自选（`^[a-zA-Z][a-zA-Z0-9_-]{1,39}$`），创建后不可改；软删除释放可复用。
7. **三面路由隔离**：子系统协作面独立成 `/api/subsys/*`（与外部接口 `/api/external/*` 平行，独立 openapi 文档 /openapi/subsys.json、Scalar 三页切换、生产环境全暴露），由专用中间件守卫（token 匿名放行），后续子系统操作资源的协作接口统一挂此前缀；管理面 `/api/subsystem/*` 与用户面 `/sso/code` 留在内部 v1 文档走内部 JWT，互不干扰。
8. **委托 SSO**：MoAI 当简化 IdP——登录用户 `POST /api/subsystem/sso/code` 换一次性委托码（Redis 60s，GetDelete 原子消费，绑定子系统+用户）；子系统后端以机器 token `POST /api/subsys/sso/exchange` 兑换用户委托 token（access 2h/refresh 7d，typ=subsystem_user_* 与机器 token 按 audience+typ 双重隔离），刷新按库内当前状态重验；签发前置校验：子系统启用、回调已配置、用户团队在授权范围（全员范围要求用户至少加入一个团队）。
9. **前端跳转页**：`/sso/authorize?client=<appid>&state=<可选>` 公开路由——未登录先去 `/login?redirect=...`（登录页仅接受本站相对路径回跳），已登录自动签发并 `location.replace` 到注册回调（code+state 追加）。

## 后续规划（本期不做）

- 用户委托 token 目前仅作身份凭证下发；对话/模型/知识库能力面接入（资源端点接受该 token + scope 扩展）、临时 MCP Provider 注入、OSS 预签名代理与配额。设计草案见 rounds-log 236 轮讨论记录，落地时另立轮次。

9. **系统应用接入与子系统分立**：子系统=业务子系统（委托 SSO/团队授权/在线心跳），系统应用接入=运维型凭证（组织/用户/团队等系统资源同步），同挂 /api/subsys 协作面但鉴权分档（子系统 JWT token vs 系统接入双轨：appid 直连或 system_app_access JWT）。
10. **统一表分域**：团队/系统两级接入同表 `access_app`，Level 决定资源域且 scope 位空间隔离（低位团队/高位系统），管理面路由各自保留（/api/access-app 团队 Admin、/api/subsystem/system-app root）；中间件各自按 Level 过滤互不可越域（系统 key 进不了 /api/external，团队 key 进不了 /api/subsys/system）。
11. **token 双轨保留**：系统协作面 key 直连与 access_token 并行可用，文档推荐 token（短时+密钥不随每次请求传输）；团队级 key 直连与既有 /api/external/token 生态不变，仅「已设密钥则兑换必带」强化。

## 已知问题

- token 换取端点无独立限流（靠 PBKDF2 校验成本 + 网关层兜底），暴力枚举面留待能力面轮次统一处理。
