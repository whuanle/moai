# 系统级接入（子系统+应用接入合并模型，SUB）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@SUB-Sxx / @SAA-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)（存量库升级与对接指引） ｜ 上游：[../auth/sdd.md](../auth/sdd.md)（内部 JWT）、[../gateway/external-api-scope-system.md](../gateway/external-api-scope-system.md)（团队外部接口凭证体系） ｜ 证据：[local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)、[local-dev/system-access-app-e2e.mjs](../../local-dev/system-access-app-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

让**独立部署的外部系统**以 `appid + 密钥` 接入 MoAI，**子系统与应用接入同表、同列表、同接口**：

- **子系统**：MoAI 生态内的独立部署服务（如智能报表），appid 由 root 自填；可配置**委托 SSO 回调登录**与**团队/组织授权**（SSO 门禁），具备**心跳在线监测**（多实例存活探针）。
- **应用接入**：交给外部系统（ERP/OA 等）的凭证，appid 自动生成（`moai-sa-` 前缀）；无回调与授权配置，**可全量访问 `/api/subsys` 协作面资源**（组织/用户/团队治理，资源范围分档已下线）。

**不含**：AI 能力面（模型/插件/知识库复用）、临时 MCP 注入、OSS 代理授权（见「后续规划」）。

## 角色与门禁

| 角色 | 判定 | 能力 |
|---|---|---|
| root | `UserStateInfo.IsRoot` | 接入增删改查、重置密钥、团队/组织授权 |
| 普通用户 | 其余 | 管理面 403，页面重定向 /apps |
| 接入（机器） | 机器面 JWT（audience=`Server\|subsystem`）或 moai-sa- appid 直连 | token/心跳/info/资源协作面 |
| 团队成员 | `ITeamService.GetMyRoleAsync != null` | 查询本团队可用子系统 |

管理面门禁在 Controller 层 `EnsureRootAsync`；团队归属门禁在 Handler（[@SUB-S1](./bdd.md#sub-s1)、[@SUB-S20](./bdd.md#sub-s20)）。

## 数据模型（应用接入统一表 access_app，Level+AppType 分域分型）

- `access_app`：Id(Guid v7)/Name/Description/TeamId（系统级固定 0）/**Level**（team=0 团队级 moai-ac- / system=1 系统级）/**AppType**（系统级分型：0=不适用 1=subsystem 子系统 2=app 应用接入）/Key（appid 明文等值命中）/KeyPrefix（展示前缀）/Secret（系统级：`v2:` 前缀 AES 密文=可回显，无前缀=存量 PBKDF2 哈希；团队级：PBKDF2 哈希）/**SecretMask**（密钥展示掩码，仅系统级可逆行）+SecretSalt（存量哈希行用）/Status（停用即时拒绝）/**TeamScope**（仅子系统行，SSO 门禁）/**CallbackUrls**（仅子系统行，JSON 地址数组=登录回调白名单，空=未开放登录）/Scopes（**已退役**：2026-11-04 团队级去范围化后整列 DROP（[asserts/team-resource-key.sql](../../asserts/team-resource-key.sql)），系统级/团队级均不再有范围概念、全等级全量，实体字段已删）/**AvatarPath**（接入头像 objectKey，空串=未设置；列表回显、前端拼 `/static` 地址展示）。
- `subsystem_team_auth`：SubsystemId（=接入 id）+TeamId，指定团队授权清单，更新整体覆盖。
- 组织授权复用统一组织授权表 `org_grant`（见 [../org/sdd.md](../org/sdd.md)），资源类型 `OrgGrantResourceType.Subsystem`（主键 Guid=接入 id），经 `IOrgGrantService` 读写。
- 存量库升级：[asserts/access_app_merge_subsystem.sql](../../asserts/access_app_merge_subsystem.sql)（补 app_type/team_scope/callback_url 列 + name 加宽 50 + subsystem 行迁入（id 原样迁移，团队/组织授权引用不断）+ 系统级 scopes 归 0 + 删 subsystem 表，幂等），见 [SOP](./sop.md)。

## 组件

```
src/subsystem/
├── MoAI.Subsystem.Shared/  Commands/{Create,Update,Delete,Reset,Reveal}SubsysAppCommand（合并 CRUD+密钥回显，按 AppType 分支；回调地址列表 callbackUrls ≤10 个）
│                           SubsystemTokenCommand（appId+secret，两类通用）、SubsystemTokenRefreshCommand、SubsystemHeartbeatCommand（instanceId 必填）
│                           Queries/{QuerySubsysApps,QuerySubsysAppInfo,QuerySubsystemAuthorizedTeams,QuerySubsystemOrgGrants,QueryTeamAvailableSubsystems}Command
│                           Commands/SetSubsystemOrgGrantsCommand、Models/SubsysCallerContext（统一调用方上下文）、Services/{SubsystemAuthDefaults,ISubsystemTokenProvider,SubsystemSecretGenerator,SystemAccessAppKeyGenerator,ISubsystemSecretCipher（AES 可逆存储/掩码/校验）,SubsystemSecretEnvelope（回显信封 AES-GCM）}
├── MoAI.Subsystem.Core/    Handlers（SubsysAppCommandsHandler 合并 CRUD、Query 系列改查 access_app、SSO/团队授权/组织授权改指向）、
│                           Services/{SubsystemTokenProvider（单一机器 token typ=access_token/machine_refresh）,SubsystemOnlineService（心跳 v2 ZSET）,SubsystemSsoCodeStore}
└── MoAI.Subsystem.Api/     SubsystemController（/subsystem 管理面合并，root；含 {id}/reveal-secret 密钥信封回显）
                            SubsystemOpenController（/subsys/{token,token/refresh,heartbeat,info,sso/*} 统一协作面，独立文档 /openapi/subsys.json）
                            SubsysAccessMiddleware（守护 /api/subsys/**：moai-sa- 直连 或 机器 token，实时回库校验）
                            SystemFaceGuard（资源面身份守卫，无 scope 分档）
                            SystemUserController/SystemOrgController/SystemTeamController（/subsys/system/* 资源接口，任一接入全量可用）
                            SubsystemSsoController（/subsystem/sso/code 用户面）、TeamSubsystemController（/team/{teamId}/subsystem/list 团队面）
ui/src/                      api/subsystem.ts、pages/admin/AdminAccessApps.tsx（/admin/integrations 单列表，root）
tests/MoAI.Subsystem.Tests/  Handler 单测（sqlite 内存库，40 例）
ui/src/pages/sso/SsoAuthorize.tsx（/sso/authorize 公开跳转页）
```

## 统一鉴权（SubsysAccessMiddleware，/api/subsys/**）

- 匿名放行仅 `POST /api/subsys/token`、`POST /api/subsys/token/refresh`。
- 双轨凭据：① Bearer `moai-sa-` appid 直连（仅应用接入行可命中；子系统自填 appid 可读性高**不允许直连**；按格式三分派：结构化 35 字符本地 HMAC 校验通过才按 Id 点查 / 存量 40 字符随机格式 Key 等值点查 / 其余直接 401 不打数据库——公网扫描与伪造请求不消耗数据库）；② Bearer JWT（`appId+secret` 经统一 `POST /api/subsys/token` 换取，typ=access_token/machine_refresh）。
- 每请求回库实时解析（停用 403/删除或 appid 不匹配 401），解析结果写统一 `SubsysCallerContext`（Id/Key/AppType/Status/CallbackUrl）。
- 资源面 `/api/subsys/system/*`（用户/组织/团队治理）经 `SystemFaceGuard` 仅做身份校验，**无 scope 分档**；子系统专属端点（teams/org-grants/SSO 签发）按 AppType 过滤，应用接入行 404。

## 存活探针：多实例心跳 v2

- **协议**：接入方每个实例启动时生成 `instanceId`（进程内稳定），每 ≤20s `POST /api/subsys/heartbeat {instanceId, version?}`（instanceId 必填 1..64）；判定窗口 60s。
- **存储**：Redis ZSET `subsys:hb:{appId:N}`，member=instanceId、score=心跳时刻。心跳用 Lua 原子脚本：`TIME`（**Redis 单时钟源**，多 MoAI 实例间时钟差不敏感）→ `ZADD` + `PEXPIRE 120s`（2 倍窗口兜底 TTL）；判定读脚本：`TIME` → `ZREMRANGEBYSCORE` 清窗外实例 → `ZCARD`，>0 即在线、值即**在线实例数**（列表/自省回显 `online`+`onlineInstances`）。
- **多实例语义**：服务级在线=任一实例存活；实例崩溃 60s 后自动掉出集合；MoAI 侧多实例部署时心跳可落到任一实例（状态全在共享 Redis，无粘滞）；停用/删除 `DEL` 整键立即下线；旧 string 键 `subsys:online:*`（v1）瞬态不迁移。
- **部署建议**：接入方自身 K8s liveness 用本地 /healthz（查自身依赖），与 MoAI 侧「在线」互相独立；心跳间隔 ≤ 窗口 1/3（容忍 2 次丢失）。

## 关键决策

1. **密钥安全（AES 可逆存储 + 信封回显，飞书式随时查看）**：48 位随机密钥以 AES 密文落库（`v2:` 前缀，key 取 SystemOptions.AES，复用变量模块同款 IAESProvider）；列表仅回显掩码（`secret_mask` 列，前 4+`****`+后 4），查看走 `POST /api/subsystem/{id}/reveal-secret`（root，**RSA+AES 信封**：前端随机 AES-256 key 经 RSA 公钥加密上送，服务端以该 key 的 AES-GCM 密文返回原文，端到端加密传输、信封 key 不留存）。存量 PBKDF2 哈希行（无前缀）不可回显（reveal 400 提示重置），验证仍走哈希；重置/新建即转 v2 可回显格式。验证常量时间比对，密文与掩码绝不落日志。
2. **机器面 token**：与内部 JWT 同 RSA 钥、独立 audience `Server|subsystem` + header typ 双重隔离；DEBUG 7d/RELEASE 2h；refresh（machine_refresh 7d）轮换免重发密钥，无状态不落库，吊销=刷新时回库重验。
3. **实时吊销**：中间件每请求回库校验（停用 403/删除 401）；停用同时清除在线集合。
4. **合并模型（原分立决策作废）**：子系统与应用接入同表 `access_app`（Level=system，AppType 分型）、同管理面 `/api/subsystem/*`、同 token 换取 `/api/subsys/token`、同协作面 `/api/subsys/**`；差异仅：子系统=自填 appid+回调+团队/组织授权+在线监测，应用接入=自动 appid+全量资源无配置。**资源范围分档（SystemApiKeyScopes）已删除**——任一系统级接入全量访问协作面资源。
5. **appid 语义**：子系统 root 自选（`^[a-zA-Z][a-zA-Z0-9_-]{1,39}$`，禁 `moai-` 保留前缀）创建后不可改；**应用接入为结构化自校验 appid**（公网防扫描）：`moai-sa-` + Base64Url(实体 Guid 16B + HMAC-SHA256 截断 4B) = 35 字符，HMAC key 由 SystemOptions.AES 派生（`ISubsystemAppIdCodec`，更换 AES 会使全部结构化 appid 失效）——中间件本地校验真伪并反解主键按 Id 点查，伪造/打错/扫描请求不打库（蓄意构造合法 appid 需 2^32 次本地尝试/条）；**存量随机格式（40 字符）按长度分派**继续走 Key 等值查询，两格式并存长期兼容；软删除释放可复用；列表中应用接入仅回显 keyPrefix（appid=直连凭证不回显原文）。
6. **授权范围=团队维度∪组织维度**（仅子系统，SSO 门禁）：`allTeams`/`specifiedTeams`（空清单=仅组织授权）+ org_grant 方向 self/up/down，任一命中即放通；root 列表回显 authorizedTeams/authorizedOrgs（名称缺失回退 #id）。
7. **三面路由隔离**：协作面 `/api/subsys/*` 独立 openapi（/openapi/subsys.json 全环境暴露）；管理面与用户面走内部 v1 JWT。
8. **委托 SSO**：MoAI 当简化 IdP——登录用户 `POST /api/subsystem/sso/code` 换一次性委托码（Redis 60s 原子消费）；机器面 `POST /api/subsys/sso/exchange` 兑换用户委托 token（access 2h/refresh 7d，typ=subsystem_user_*）；签发前置校验「团队∪组织授权并集」，兑换/刷新不复查授权。**仅子系统行可参与**（应用接入行 appType 过滤 404，且 CallbackUrls 恒空）。**多回调地址白名单**：子系统可注册多个入口（测试/生产等，≤10 个），引导跳转 `/sso/authorize?client=<appid>&redirect_uri=<可选>` 指定本次入口——传入须精确命中注册列表（否则 403），缺省取列表第一个。
9. **前端单列表**：`/admin/integrations` 一个列表承载两类接入（类型列区分），新建时类型 Radio（编辑锁定）；应用接入新建即用，子系统展开回调与授权配置。
10. **接入头像**（子系统与应用接入通用，[@SUB-S67](./bdd.md#sub-s67)~[@SUB-S70](./bdd.md#sub-s70)）：照插件/应用头像同款模式——前端 `AvatarUpload` 选图即压缩直传存储（pre_upload_image→PUT→complate_url），弹窗内暂存 objectKey，**随 Create/Update 命令一并登记**（无独立 avatar 端点，取消弹窗即放弃）；Handler 经 `SubsysAvatarGuard` 校验 objectKey 已完成上传登记（伪造/未完成 404）；Update 语义 null=不修改（停用切换等调用不受影响）、非空=校验后替换。列表名称列 36px 头像展示，`SubsysAppItem.avatarPath` 回显。存量库补列：[asserts/access_app_avatar.sql](../../asserts/access_app_avatar.sql) + 启动幂等 DDL。

## 后续规划（本期不做）

- 用户委托 token 的对话/模型/知识库能力面接入、临时 MCP 注入、OSS 预签名代理与配额。

## 已知问题

- token 换取端点无独立限流（靠 PBKDF2 校验成本 + 网关层兜底）。
- 心跳 ZSET 读脚本为读时惰性清理，极端高频读下多一次 ZREMRANGEBYSCORE（量级可忽略，无后台任务）。
