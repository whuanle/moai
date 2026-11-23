# 系统级接入（子系统+应用接入合并模型，SUB/SAA）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表（SUB）

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SUB-S1 | [local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)（SUB-01/02） | PASS 101/101（2026-11-01） |
| @SUB-S2 | @manual HTTP 走查（[SOP 第 4 节](./sop.md)） | PASS（2026-11-01） |
| @SUB-S3 ~ @SUB-S8 | 同上（SUB-03 ~ SUB-08：创建一次性密钥/非法 appid 400/重复 409/错密钥与不存在 appid 401/正确换取） | PASS 101/101（2026-11-01） |
| @SUB-S9 ~ @SUB-S13 | 同上（SUB-09 ~ SUB-15：心跳鉴权/保活在线/离线实例数 0/自省/无 token 401） | PASS 101/101（2026-11-01） |
| @SUB-S14 ~ @SUB-S18 | 同上（SUB-16 ~ SUB-22：停用即时生效/清除在线/重置密钥/新旧密钥换取） | PASS 101/101（2026-11-01） |
| @SUB-S19 ~ @SUB-S23 | 同上（SUB-24 ~ SUB-31：团队授权全员/指定/回显/不存在 400/非成员 404） | PASS 101/101（2026-11-01） |
| @SUB-S24 ~ @SUB-S26 | 同上（SUB-33 ~ SUB-35：删除/密钥失效/appid 释放复用） | PASS 101/101（2026-11-01） |
| @SUB-S27 ~ @SUB-S31 | [AdminAccessApps.test.tsx](../../ui/src/pages/admin/__tests__/AdminAccessApps.test.tsx)（合并列表/创建流/编辑覆盖/停用/重置/删除） | PASS 7/7（2026-11-01） |
| @SUB-S32 ~ @SUB-S42 | [local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)（SUB-36 ~ SUB-50：委托 SSO 全链路） | PASS 101/101（2026-11-01） |
| @SUB-S45 | 同上（SUB-51 ~ SUB-53：refresh 轮换） | PASS 101/101（2026-11-01） |
| @SUB-S46 ~ @SUB-S52 | 同上（SUB-54 ~ SUB-69：组织授权全链路） | PASS 101/101（2026-11-01） |
| @SUB-S43、@SUB-S44 | [SsoAuthorize.test.tsx](../../ui/src/pages/sso/__tests__/SsoAuthorize.test.tsx) | PASS 6/6（2026-10-28） |
| @SUB-S61、@SUB-S62 | 同上（SUB-70 ~ SUB-75：缺实例标识 400/单实例 1/双实例 2/自省实例数） | PASS 101/101（2026-11-01） |
| @SUB-S53 ~ @SUB-S60、@SUB-S63 ~ @SUB-S66 | 同上（SUB-76 ~ SUB-94：多地址 redirect_uri 指定入口/未注册 403/缺省取第一个为 SUB-91~94；appid 结构化自校验 SUB-82a~d：格式不符/非法字符/差一位/篡改 HMAC 均 401 不打库；公网限流 SUB-95/96：同 (IP, appid) 失败 10 次 429 + 维度隔离：应用接入自动 appid/保留前缀 400/同表回显不漏原文/类型过滤/统一 token/直连轨界限/资源面全量/旧端点下线/专属端点 404/SSO 过滤 404/应用接入心跳/删除失效） | PASS 101/101（2026-11-01） |
| @SUB-S63（单测侧） | RevealSubsysAppSecretCommandHandlerTests（v2 信封可解密/存量 400/信封长度 400/404） | PASS 45/45（2026-11-01） |
| @SUB-S67 ~ @SUB-S70（单测侧） | CreateSubsysAppCommandHandlerTests（已登记 objectKey 落库/未登记与未完成上传 404/缺省空串/255 上限校验）+ SubsysAppCommandsHandlerTests（更新 null=不修改/伪造 404/已登记替换） | PASS 53/53（2026-11-04） |
| @SUB-S53 ~ @SUB-S55（UI 侧） | [AdminIntegrations.test.tsx](../../ui/src/pages/admin/__tests__/AdminIntegrations.test.tsx)（单列表无 Tab/类型列/实例数展示/非 root 重定向） | PASS 3/3（2026-11-01） |
| @SUB-S67 ~ @SUB-S70 | [local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)（SUB-97 ~ SUB-103：创建携带头像落库回显/创建与更新携带未登记 objectKey 404/替换头像回显新 key/更新不携带字段头像不变/缺省空串与应用接入同构回显；走 pre_upload_image→PUT→complate_url 真实存储链路，存储不可用时整段跳过） | PASS 109/109（2026-11-04） |
| @SUB-S71、@SUB-S73 | [local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)（SUB-104 ~ SUB-110：未开放登录/不存在统一 unavailable 不 401、匿名可用 login_required、redirect_uri 指定入口、未注册 callback_mismatch、授权用户 ok、未授权 not_authorized） | PASS 117/117（2026-11-15） |
| @SUB-S72 | 同上（SUB-111：指定团队且团队/组织授权均为空直接 no_grants） | PASS 117/117（2026-11-15） |
| @SUB-S74 | [SsoAuthorize.test.tsx](../../ui/src/pages/sso/__tests__/SsoAuthorize.test.tsx)（11 例：预检不可用/未授权停在本页不跳登录不回跳、需登录先登录并清理过期登录态、放行后才签发委托码、预检/签发失败提示） | PASS 11/11（2026-11-15） |
| @SUB-S90 | [OAuthLogin.test.tsx](../../ui/src/pages/auth/__tests__/OAuthLogin.test.tsx)（5 例：无暂存回应用广场/消费暂存回 SSO 跳转页且读取即清除/不安全目标兜底应用广场/未绑定一键注册成功同口径消费/登录失败不清暂存）+ [oauth.test.ts](../../ui/src/utils/__tests__/oauth.test.ts)（暂存工具 9 例：安全校验/暂存消费/清除/state 编解码回归） | PASS 14/14（2026-11-23） |
| @SUB-S75 | MoAI.Subsystem.Tests（CreateSubsysAppCommandHandlerTests 指定团队范围落库断言；SubsystemSsoHandlerTests 预检 10 例） | PASS 63/63（2026-11-15） |
| @SUB-S82 | MoAI.Subsystem.Tests（QueryMySubsystemsCommandHandlerTests 5 例：停用/未配置回调/应用接入行过滤、指定团队授权、组织授权并集、匿名空列表、头像回显）+ [SubsystemPortal.test.tsx](../../ui/src/pages/subsystems/__tests__/SubsystemPortal.test.tsx)（4 例：列表/空态/进入携带 appid/加载失败空态）+ [local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)（SUB-112 ~ SUB-116） | PASS（2026-11-15，单测 71/71、vitest 4/4、E2E 122/122） |

## 场景映射表（SAA）

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SAA-S1 ~ @SAA-S2 | [local-dev/system-access-app-e2e.mjs](../../local-dev/system-access-app-e2e.mjs)（SAA-S1 ~ SAA-S6：门禁/创建一次性 appid+密钥/列表前缀） | PASS 58/58（2026-11-01） |
| @SAA-S4、@SAA-S5 | 同上（SAA-S7 ~ SAA-S13：直连自省/更新即时生效/停用恢复；自省无范围字段） | PASS 58/58（2026-11-01） |
| @SAA-S7 ~ @SAA-S8 | 同上（SAA-S14 ~ SAA-S16：按前缀搜索/删除即失效/列表不可见） | PASS 58/58（2026-11-01） |
| @SAA-S12 ~ @SAA-S17 | 同上（SAA-S17 ~ SAA-S29：appid+密钥/token 双轨/refresh 轮换/停用双轨 403/重置密钥旧失效） | PASS 58/58（2026-11-01） |
| @SAA-S19 ~ @SAA-S23 | 同上（SAA-S34 ~ SAA-S42：用户 upsert 幂等/校验/root 保护/禁用启用） | PASS 58/58（2026-11-01） |
| @SAA-S24 ~ @SAA-S26 | 同上（SAA-S43 ~ SAA-S49：组织角色） | PASS 58/58（2026-11-01） |
| @SAA-S27 ~ @SAA-S31 | 同上（SAA-S50 ~ SAA-S57：团队治理/删除后全链失效） | PASS 58/58（2026-11-01） |
| @SAA-S60 ~ @SAA-S62 | 同上（SAA-S58 ~ SAA-S61：资源面全量/子系统 token 互通/应用接入心跳在线） | PASS 58/58（2026-11-01） |
| @SAA-S63 | local-dev/internal-app-access-e2e.mjs（IA-S4a~c 创建/幂等/直连读用户、IA-S5a~d 冲突与参数校验；冲突前置校验不产生半成品用户） | PASS 56/56（2026-11-13） |
| @SAA-S64、@SAA-S65、@SAA-S66 | local-dev/internal-app-access-e2e.mjs（IA-S2c subsystem 与 app 同权；IA-S7a~g 未绑定一次调用自动建户/绑定/幂等/未知渠道/参数/多匹配、IA-S8a~f 目标校验与失败不建户、IA-S9a/b 访问点关闭、IA-S15 单应用绑定、IA-S16/S17 刷新与禁用吊销） | 待执行（2026-11-13） |
| @SAA-S3、@SAA-S18、@SAA-S58 | 已退役（范围分档下线，编号不复用，见 BDD 退役标注） | —（2026-11-01） |
| @SAA-S59 | [TeamAccessApps.test.tsx](../../ui/src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx)（团队级创建/重置，合并模型未触及） | PASS 6/6（2026-10-29） |

## 后端单测补充

| 用例 | 验证物 | 结果（日期） |
|---|---|---|
| 创建子系统（自填 appid/哈希/重复 409/团队 400/校验器）+ 创建应用接入（自动 appid/忽略子系统字段） | [MoAI.Subsystem.Tests](../../tests/MoAI.Subsystem.Tests/)（CreateSubsysAppCommandHandlerTests 6 例；断言改掩码+v2 密文可解+结构化 appid 35 字符 HMAC 可反解主键） | PASS 45/45（2026-11-01） |
| 合并 CRUD（更新按类型分字段/软删/列表回显与类型过滤/在线实例数） | 同上（SubsysAppCommandsHandlerTests 6 例） | PASS 45/45（2026-11-01） |
| 统一 token 换取（子系统/应用接入/停用 403/重置失效） | 同上（SubsystemTokenCommandHandlerTests 7 例，含存量哈希行校验） | PASS 45/45（2026-11-01） |
| 组织授权并集门禁/委托/回显、SSO 全链（含应用接入行 404）、进入前预检（匿名/登录态/回调/无授权） | 同上（SubsystemSsoHandlerTests 25 例） | PASS 63/63（2026-11-15） |
| 团队可用列表（仅 appType=subsystem） | 同上（QueryTeamAvailableSubsystemsCommandHandlerTests 4 例） | PASS 45/45（2026-11-01） |
| 统一表回归 | teamres-key-e2e.mjs（TR-S8 应用接入 key 换 token 携 secret、TR-S27 资源 key 不可换 token、TR-S29 伪造/旧前缀 401）+ org-e2e 回归 | 全绿（2026-11-04，TR 31/31、ORG 53/53；原 team-apikey-scope-e2e TA-40~42 随 scope 体系退役删除，TA 编号不复用） |
| @SUB-S76 | [local-dev/subsys-event-e2e.mjs](../../local-dev/subsys-event-e2e.mjs)（EVT-03/26/27） | PASS 30/30（2026-11-15） |
| @SUB-S77 | 同上（EVT-05~07/10/29/30） | PASS 30/30（2026-11-15） |
| @SUB-S78 | 同上（EVT-09/12/13） | PASS 30/30（2026-11-15） |
| @SUB-S79 | 同上（EVT-15/18/19/24/25/28） | PASS 30/30（2026-11-15） |
| @SUB-S80 | 同上（EVT-16/17/20/21） | PASS 30/30（2026-11-15） |
| @SUB-S81 | 同上（EVT-14/15/16/17） | PASS 30/30（2026-11-15） |
| @SUB-S83 | [local-dev/subsys-event-e2e.mjs](../../local-dev/subsys-event-e2e.mjs)（EVT-31/34：token 与 info 回显 events.hubPath/restPath/catalogPath/schemaVersion/retentionDays/maxReplay） | PASS 38/38（2026-11-15） |
| @SUB-S84 | 同上（EVT-32：事件目录 ≥20 项、统一前缀、resync_required 为 control） | PASS 38/38（2026-11-15） |
| @SUB-S85 | 同上（EVT-35~38：前缀/精确过滤先于截断、limit 越界 400、nextCursor） | PASS 38/38（2026-11-15） |
| @SUB-S86 | 同上（EVT-33/34：心跳回显 onlineInstances/windowSeconds=60/recommendedIntervalSeconds=20） | PASS 38/38（2026-11-15） |
| @SUB-S87 | [MoAI.Subsystem.Client.Tests](../../tests/MoAI.Subsystem.Client.Tests/)（SubsysEventItemExtensionsTests 4 例 + SubsysEventDeduplicatorTests 6 例 + SubsysEventWorkerTests 1 例 + SubsysHeartbeatWorkerTests 2 例：双形态 ID/数组/truncated/去重窗口/过滤/退避/宿主启停/心跳上报） | PASS 34/34（2026-11-15） |
| @SUB-S88 | 同上（SubsysResourceClientTests 13 例：自省 events 回显/事件过滤与 nextCursor/翻页/错误语义/心跳请求体/分页搜索/团队与目录/异步游标） | PASS 34/34（2026-11-15） |
| @SUB-S89 | [local-dev/subsys-event-e2e.mjs](../../local-dev/subsys-event-e2e.mjs)（EVT-39/40：status_changed 带 authVersion、强制下线收到 session_revoked 且版本递增） | PASS 40/40（2026-11-15） |
| 事件契约单测 | [MoAI.Subsystem.Tests](../../tests/MoAI.Subsystem.Tests/)（SubsystemEventContractTests 3 例：目录唯一性与前缀、目录查询版本/窗口、心跳响应口径） | PASS 71/71（2026-11-15） |

## 回归命令

```bash
node local-dev/subsystem-e2e.mjs              # 需后端运行中（默认 :5000），零外部依赖
node local-dev/subsys-event-e2e.mjs           # @SUB-S76~S86 事件推送 38 断言（SignalR + Redis 分代事件索引 + 协作面对账 + 事件目录/过滤/心跳/自描述）
node local-dev/system-access-app-e2e.mjs http://127.0.0.1:5000   # 应用接入 + 系统资源接口（合并模型后同为 :5000）
dotnet test tests/MoAI.Subsystem.Tests        # 后端单测（71 例）
node local-dev/subsys-demo-seed.mjs           # demo 数据重建 + 功能验证（11 例）
cd ui && npx vitest run src/pages/admin/__tests__/AdminAccessApps.test.tsx src/pages/admin/__tests__/AdminIntegrations.test.tsx src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx
```

## 覆盖率说明

- 自动化覆盖全部核心写路径与机器面鉴权；@SUB-S2（未登录 401）由全局 JWT 拦截保证，走查确认。
- 心跳窗口过期转离线（60s 无心跳实例掉出 ZSET）未做等待型自动化（等 60s 成本高），由 Lua 脚本窗口语义与 SUB-11（初始离线）/SUB-72/74（实例数计数）双向佐证。
- 存量子系统 token（旧 typ/claim）与旧端点在合并部署后失效，需重新换取；存量 moai-sa- 直连凭据与密钥哈希迁移后继续有效（asserts/access_app_merge_subsystem.sql 迁移保 id/密钥不变）。
- 密钥存储已转 AES 可逆（v2: 前缀）；存量 PBKDF2 哈希行不可回显（reveal 400），验证不受影响，重置后转可查看格式。
