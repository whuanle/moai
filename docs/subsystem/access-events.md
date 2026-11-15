# 系统级接入事件推送（Access Events）设计

> 关联：[子系统 SDD](./sdd.md) ｜ [BDD](./bdd.md)（@SUB-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[消息与 SignalR](../message/sdd.md)、[统一鉴权](./sdd.md) ｜ 规范：[DOC-STANDARD](../DOC-STANDARD.md)
> 状态：已实现（2026-11-15）。本文是事件契约真源，BDD 场景从 `@SUB-S76` 起编号，不复用旧号；证据见 [local-dev/subsys-event-e2e.mjs](../../local-dev/subsys-event-e2e.mjs)。

## 1. 结论：SignalR 适配性

**适配，且优于轮询/Webhook。** 但必须明确它的边界：SignalR 是**实时提示通道**，不是可靠消息队列。

| 需求 | SignalR 对应能力 |
|---|---|
| MoAI 不知道子系统地址 | 子系统主动外连 MoAI，只需出站 443/WSS |
| 多实例 MoAI | 已有 Redis backplane（`moai:sr`），任一实例推送可到达连接所在实例 |
| 鉴权 | 复用机器 token（`appid+secret` 换取，`typ=access_token`） |
| 按接入隔离 | 连接时按 `appId` 加 SignalR Group，推送只进目标组 |
| 断线重连 | 客户端自带 `WithAutomaticReconnect`；服务端提供 `lastEventId` 补发 |
| 自定义协议 | 不需要，JSON 事件封装即可；配套 .NET 客户端可复用 `clients/MoAI.Subsystem.Client` |

不选轮询：延迟与无效请求不可接受。不选 Webhook：MoAI 要维护子系统地址、出站白名单/SSRF 防护、签名、重试与死信，成本明显更高；可作为后续离线兜底，不作为主通道。

**边界**：SignalR 断线期间不保证送达。本文用「Redis Stream 最近事件补发 + 全量对账」兜底，语义是 **at-most-once 实时推送 + 可恢复**，不是 exactly-once。事件是变更信号 + 轻量上下文（可带主体/关联 ID），**接收方按需回查 MoAI 取详情**，不把事件当数据快照。

## 2. 总体架构

```
业务模块 Handler（account/org/team/subsystem/auth）
        │  IAccessEventPublisher.PublishAsync(...)
        ▼
AccessEventPublisher ──XADD──▶ Redis Stream: subsys:events（MAXLEN≈10000）
        │ Maomi.MQ  subsys.event
        ▼
AccessEventPushConsumer ──IHubContext<SubsysEventHub>──▶ Redis backplane
        │
        ▼
/hub/subsys   Group: subsys:{appId}   ──▶ 子系统 / 系统级应用接入（多实例各自连接）
```

组件落位：

```
src/infra/MoAI.Infra.Shared/Events/   纯契约：IAccessEventPublisher、AccessEvent、AccessEventNames、AccessEventAudience、扩展方法
src/subsystem/MoAI.Subsystem.Shared/  Events/AccessEventPushMessage（MQ）、Queries/QuerySubsysGrants|Events（+Responses）
src/subsystem/MoAI.Subsystem.Core/    Services/AccessEventPublisher、AccessEventStream；Consumers/AccessEventPushConsumer；
                                      Hubs/SubsysEventHub、SubsysHubAuthenticationHandler；Handlers/QuerySubsys*
src/subsystem/MoAI.Subsystem.Api/     SubsystemOpenController（/subsys/grants、/subsys/events）、SystemTeamController（{teamId}/users）
src/MoAI/                            SubsystemCoreModule（Hub 认证方案 + MemoryCache）、Program.cs（MapHub /hub/subsys）、
                                      ConfigureAuthorizaModule（机器面 JWT 失败降为 Debug）
clients/MoAI.Subsystem.Client/        Events/SubsysEventClient（SignalR、lastEventId、重连对账）、Resources/SubsysResourceClient
local-dev/subsys-event-e2e.mjs        28 断言（建连/用户/组织/团队/授权/补发/过期游标/停用最终事件）
```

> 契约放在 `MoAI.Infra.Shared/Events`，account/org/team/auth/subsystem 均只依赖该共享程序集，避免反向项目依赖成环。
> 不复用 `IEventPushService`：它的受众是站内用户（`Clients.Users`），本场景受众是接入（Group + 启用状态 + 定向/广播 + 补发游标），生命周期与路由规则不同；复用会把子系统语义塞进 message 模块。

## 3. 连接与鉴权（/hub/subsys）

- **独立 Hub，不复用 `/hub/moai`**：用户端 Hub 用内部用户 JWT（audience=`Server`），机器端用接入 token（audience=`Server|subsystem`）。混用会让用户/机器两套主体、禁用语义、生命周期互相污染。
- **独立认证方案 `SubsysHub`**（`SubsysHubAuthenticationHandler`）：建连时解析凭据并**回库校验** `access_app.level=system` 且 `status=enabled`；支持两条轨道：
  - 机器 token（`typ=access_token`，audience=`Server|subsystem`）：与 `/api/subsys` 同一套 `ISubsystemTokenProvider` 校验；
  - `moai-sa-` appid 直连：结构化 appid 本地 HMAC 校验后按 Id 点查，兼容应用接入不换 token 的用法。
  团队级 `moai-ac-`、用户 JWT、用户委托 token 均不能建连。
- **凭据传递**：优先 `Authorization: Bearer <token>` 头；浏览器/无法设头的客户端用 `?access_token=` 查询参数（SignalR 官方做法）。默认内部 JWT 方案对机器面验签失败降为 Debug 日志，不刷错误。
- **停用/删除**：建连回库拒绝；推送消费端按启用状态过滤，停用/删除事件走 target 直发作为最后一次投递。客户端应结合最后一次 `status_changed/deleted` 事件与 REST 403/401 判断，不要无限重试。
- **分组**：`subsys:{appId:N}`。不依赖全局 `IUserIdProvider`，避免与用户 id 归组语义耦合。
- **服务端下行方法**：
  - `onReady`：连接就绪握手（`appId`、`serverTime`、`schemaVersion`、`requiresFullSync`、`replayFrom`）。
  - `onEvent`：单条事件（与 `/hub/moai` 同名，客户端心智一致）。
- **客户端不可调用任何 Hub 方法**：单向推送，减少攻击面。
- **HTTP 日志**：Hub 标 `[HttpLogging(HttpLoggingFields.None)]`；反代需对 `access_token` 查询参数做脱敏/不记录。
- **中间件顺序**：`CustomAuthorizaMiddleware` 对 `/hub/subsys` 直接放行（机器主体没有站内用户态）；鉴权由 Hub 的 `[Authorize(AuthenticationSchemes=SubsysHub)]` 完成。`MapHub` 不要再叠加不带 scheme 的 `RequireAuthorization()`。
- **令牌过期**：SignalR 连接握手后不因 token 过期自动断开；重连时由 `AccessTokenProvider` 换取新 token。停用/删除后 REST 立即 403/401，推送侧也会停止投递（见 §7）。
- **多实例子系统**：每个实例可各连一条；事件会广播到该 app 的所有连接。若业务副作用必须只执行一次，客户端用 `event.id` 做共享去重（Redis/DB 唯一键），或只让 leader 实例建连。
- **反向代理**：需放行 `/hub/*` 的 WebSocket Upgrade，读超时大于 SignalR keepalive 周期；已有 `/hub/moai` 的部署配置可直接复用。

## 4. 事件契约

### 4.1 信封

```json
{
  "id": "1731670000000-0",
  "event": "subsys.user.updated",
  "time": "2026-11-15T08:00:00+00:00",
  "schemaVersion": 1,
  "source": "019a1f0c-7f3e-7c2a-9d61-2b0f6f1c9a11",
  "data": {
    "userId": "10001",
    "userName": "alice",
    "fields": ["nickName", "email"]
  }
}
```

- `id`：不透明字符串，客户端只用于**去重**和**回传 `lastEventId`**，不解析。正常为 Redis Stream ID；Redis 不可用时回落 Guid v7。
- `event`：小写点分，统一 `subsys.` 前缀，见 §5。**追加式演进，永不复用/改名**。
- `time`：事件发生时间（UTC，`DateTimeOffset`）。
- `schemaVersion`：信封版本，当前 `1`；只做加法演进。
- `source`：可选，发起变更的系统级接入 appId；由服务端从 `SubsysCallerContext` 自动识别，仅用于观测与回声抑制。**不要仅按 `source == 自己 appId` 忽略事件**——同一接入多实例会漏事件；批量写入方如确实不需要回声，应在请求头显式声明 `X-MoAI-Event-Echo: false`，由服务端按 `source` 过滤。
- `data`：**主体/关联 ID 与轻量变更元数据；禁止业务正文、实体快照与大列表**；单条序列化后 ≤2 KB（硬上限 8 KB），详见 §4.4。

### 4.2 方法

| 方法 | 方向 | 用途 |
|---|---|---|
| `onReady` | 服务端→客户端 | 建连成功：appId、serverTime、schemaVersion、是否需要全量对账 |
| `onEvent` | 服务端→客户端 | 单条业务事件；客户端收到后调 REST 取详情/最新状态 |
| `subsys.resync_required` | 服务端→客户端（作为 `onEvent` 的事件） | 补发游标失效/积压超限/Redis 不可用，要求全量对账 |

控制事件示例：

```json
{
  "id": "1731670000123-0",
  "event": "subsys.resync_required",
  "time": "2026-11-15T08:01:00+00:00",
  "schemaVersion": 1,
  "data": { "reason": "cursor_expired" }
}
```

`reason`：`initial_sync` / `cursor_expired` / `overflow` / `replay_unavailable`。

### 4.3 重连补发

- 客户端连接 URL 可带 `lastEventId=<上次收到的事件 id>`。
- 服务端 `OnConnectedAsync` 从 Redis Stream `XRANGE subsys:events (lastId + COUNT 200`，过滤 `audience=all || target=appId` 后逐条 `onEvent` 补发；单次最多扫描 2000 条、补发 200 条。
- 游标已不在 Stream 窗口内 / 补发超限 / Redis 不可用 → 发 `subsys.resync_required`，客户端走全量对账。
- 首次连接（无 `lastEventId`）→ `onReady.requiresFullSync=true`。
- REST 兜底：`GET /api/subsys/events?afterId=&limit=` 返回同一 Stream 的过滤结果，供不使用 SignalR 的客户端或排障使用。
- **配套查询接口**（本次一并补）：
  - `GET /api/subsys/grants` → `{ teamScope, teamIds: [], orgRoleIds: [] }`，供自身授权对账（现有 `info` 只有计数）。
  - `GET /api/subsys/system/teams/{teamId}/users` → `{ items: [{ userId, userName, role }] }`，补齐团队成员对账面。
  - `POST /api/subsys/token` / `GET /api/subsys/info` 响应增加 `events: { hubPath: "/hub/subsys", schemaVersion: 1 }`，客户端无需硬编码路径。

### 4.4 载荷规则（可带关联 ID，不推大对象）

事件是**变更信号 + 轻量上下文**，不是数据同步通道。可以带关联 ID 和必要信息，帮助接收方少回查；但不能把实体快照、大列表或业务正文塞进事件。

**允许：**

- 主体 ID 与关联 ID：`userId`/`teamId`/`roleId`/`providerId`/`appId`；
- **有限的关联 ID 数组**：如 `roleIds`、`teamIds`、`orgRoleIds`、`userIds`，单个数组 ≤50 个；
- 状态、角色、变更类型、`changed`、计数、版本号等标量；
- 固定字段名数组（如 `fields: ["nickName","email"]`，≤8 项）。

**禁止：**

- 实体快照、完整用户/成员/角色/授权/团队列表、分页结果；
- 文件、文档、知识库、聊天内容等业务正文；
- 超过 50 个 ID 的数组，或批量变更的全量结果；
- 可变业务字段的值（名称、邮箱、头像、简介等）——只给字段名，值由接收方回查，避免事件变成过期副本。

**硬限制与降级：**

- `data` 序列化后 ≤2 KB 记 warning，>8 KB 拒绝发送并记 error（实时信号丢失由对账兜底）。
- 关联 ID 数组 >50 个时，**不要截断发送**；改为只发主体 ID + 计数（如 `teamCount: 1200`）+ `truncated: true`，由接收方回查完整列表。
- 批量操作：优先逐条小事件；若数组在 50 以内可合并为一条（如 `{ teamId, userIds: [...], role }`），超过则发汇总信号 + 计数。
- 任何新增事件在评审时都要回答一句：**“接收方拿到这个事件后，哪些字段直接用、哪些调哪个 REST 回查？”** 答不上来就说明事件设计不完整。
- REST 回查接口可以返回大列表（如 `/grants` 的 `teamIds`），事件只带**受上限约束的摘要**；两者不要混淆。

**示例：**

```json
// 好：带关联 ID 和轻量上下文，接收方可直接更新本地关系
{ "userId": "1001", "roleIds": ["1", "3"] }
{ "teamId": "12", "userIds": ["1001", "1002"], "role": "member" }
{ "changed": "teams", "teamScope": "specifiedTeams", "teamIds": ["12", "18"] }

// 好：超过上限，退化为计数 + 回查
{ "changed": "teams", "teamScope": "specifiedTeams", "teamCount": 1200, "truncated": true }

// 禁止：实体快照 / 大列表 / 业务正文
{ "users": [{ "userId": "1001", "email": "...", "avatar": "..." }] }
{ "teamIds": ["... 1200 个 ..."] }
```

## 5. 事件目录

约定：`all` = 所有启用中的系统级接入（`AppType=subsystem` 与 `app` 都收）；`target` = 仅事件主体接入。所有 ID 以字符串传输；关联 ID 数组受 ≤50 上限约束，超限退化计数（见 §4.4）。

### 5.1 接入自身（target）

| 事件 | 触发 | data | 客户端反应 |
|---|---|---|---|
| `subsys.access_app.updated` | 名称/简介/头像/SSO 回调地址变更 | `{ fields: ["name","description","avatar","callbackUrls"] }` | 刷新自身信息；回调变更时重载 SSO 配置 |
| `subsys.access_app.status_changed` | 启用/停用 | `{ status: "enabled" }` / `{ status: "disabled" }` | 停用：立即停止业务并断开连接 |
| `subsys.access_app.grants_changed` | 团队范围/授权团队/组织授权变更 | `{ changed, teamScope?, teamIds?, orgRoleIds? }`；数组 ≤50，超限给 `teamCount/orgRoleCount + truncated` | 可直接用关联 ID 更新本地授权；超限/缺失时调 `GET /api/subsys/grants` |
| `subsys.access_app.secret_reset` | 密钥重置 | `{}` | 立即用新密钥重新换 token；旧连接重连 |
| `subsys.access_app.deleted` | 接入被删除 | `{}` | 停止业务、断开连接、清理本地凭证 |

> `status_changed=disabled` 与 `deleted` 是**最后一次投递**：即使库内已停用/删除，消费者仍向 target 组直发（见 §7）。
> `updated` 只列实际变化字段；仅状态变化时只发 `status_changed`，避免同一保存动作重复推送。

### 5.2 用户（all）

| 事件 | 触发 | data |
|---|---|---|
| `subsys.user.created` | 注册、系统 upsert 新建、OAuth 首次建户、app-tokens 自动建户 | `{ userId, userName }` |
| `subsys.user.updated` | 昵称/邮箱/手机/头像等资料变更 | `{ userId, userName, fields: [...] }` |
| `subsys.user.status_changed` | 禁用/启用 | `{ userId, userName, disabled: true 或 false }` |
| `subsys.user.org_roles_changed` | 用户组织角色整体覆盖（含注册默认角色） | `{ userId, roleIds: [...] }`；`roleIds` ≤50，超限给 `roleCount + truncated` |
| `subsys.user.oauth_linked` | 第三方身份绑定/自动绑定 | `{ userId, providerId }` |
| `subsys.user.oauth_unlinked` | 第三方身份解绑 | `{ userId, providerId }` |

> `user.deleted` 暂不定义（当前只有禁用，无删除用户能力）；将来加软删除时再追加，编号不复用。

### 5.3 组织（all）

| 事件 | 触发 | data |
|---|---|---|
| `subsys.org.role_created` | 新建组织角色 | `{ roleId, parentId }` |
| `subsys.org.role_updated` | 名称/上级变更（含子树 path 迁移） | `{ roleId, parentId, moved: true 或 false }` |
| `subsys.org.role_deleted` | 删除组织角色（角色下用户解绑） | `{ roleId }` |

> 组织树迁移会改变 `up/down` 授权的有效人群；收到 `role_updated(moved=true)` 的子系统应重取授权角色/用户并重算，而不是只改本地名称。

### 5.4 团队（all）

| 事件 | 触发 | data |
|---|---|---|
| `subsys.team.created` | 新建团队（含系统接口建队） | `{ teamId, ownerUserId? }` |
| `subsys.team.updated` | 名称/简介/头像变更 | `{ teamId, fields: [...] }` |
| `subsys.team.status_changed` | 平台管理员禁用/启用团队 | `{ teamId, disabled: true 或 false }` |
| `subsys.team.member_added` | 添加/批量添加成员 | `{ teamId, userIds: [...], role: "owner"/"admin"/"member" }`；≤50，超限给 `count + truncated` |
| `subsys.team.member_updated` | 成员角色调整 | `{ teamId, userId, role }` |
| `subsys.team.member_removed` | 移除成员 | `{ teamId, userId }` |
| `subsys.team.owner_changed` | Owner 转让 | `{ teamId, oldOwnerUserId, newOwnerUserId }` |

> 团队不可解散；成员批量添加 ≤50 用 `userIds` 合并为一条，>50 只发 `teamId + count + truncated`，由接收方回查成员列表；**不会把成员列表塞进事件**。团队级应用接入（`moai-ac-`）**不在此事件面内**。

### 5.5 事件 → 回查接口

| 事件 | 接收方回查 |
|---|---|
| `user.created` / `user.updated` / `user.status_changed` | `GET /api/subsys/system/users/{userId}` |
| `user.org_roles_changed` | 带 `roleIds` 可直接覆盖本地；`truncated` 或缺失时 `GET /api/subsys/system/org/users/{userId}/roles` |
| `user.oauth_linked` / `user.oauth_unlinked` | 接收方本地绑定表按 `userId + providerId` 处理；用户资料以用户接口为准 |
| `org.role_created` / `org.role_updated` / `org.role_deleted` | `GET /api/subsys/system/org/roles`；需要角色成员时 `GET /api/subsys/system/org/roles/{roleId}/users` |
| `team.created` / `team.updated` / `team.status_changed` | `GET /api/subsys/system/teams/list` |
| `team.member_*` / `team.owner_changed` | 带 `userIds/role` 可增量更新；`truncated` 或缺失时 `GET /api/subsys/system/teams/{teamId}/users` |
| `access_app.updated` / `status_changed` / `secret_reset` | `GET /api/subsys/info` |
| `access_app.grants_changed` | 带 `teamScope/teamIds/orgRoleIds` 可直接更新；`truncated` 或缺失时 `GET /api/subsys/grants` |
| `access_app.deleted` | 无需回查：停止业务并清理本地凭证 |
| `subsys.resync_required` | 按 §6 全量对账 |

### 5.6 明确不推送

密码/2FA/管理员标记、登录日志、AI 应用/插件/知识库/知识图谱、系统设置、token 本身。事件面只覆盖「系统级接入依赖的身份、组织、团队与自身接入配置」。

## 6. 投递语义与一致性

- **实时推送 best-effort**：MQ 消费失败重试耗尽后 Ack 放弃，不投死信；不阻塞业务请求。
- **最近事件窗口**：Redis Stream `subsys:events`，`MAXLEN ~ 10000` 长度滚动；只用于短时断线补发，不是审计日志（Redis 侧可按部署补 TTL）。
- **最终一致性**：任何无法补齐的场景（首次连接、游标过期、Redis 丢失、客户端实现异常）都走**全量对账**：
  1. `GET /api/subsys/info` + `GET /api/subsys/grants`（新增，取自身授权快照）；
  2. `GET /api/subsys/system/users/list`（分页）；
  3. `GET /api/subsys/system/org/roles`；
  4. `GET /api/subsys/system/teams/list` + `GET /api/subsys/system/teams/{teamId}/users`（新增，现有协作面缺团队成员查询）。
- **顺序**：不保证跨实例严格有序。事件只是「去取最新状态」的提示，客户端按业务状态幂等处理；不要用事件顺序推导状态机。
- **重复/乱序**：MQ 重投、补发与实时推送叠加都可能重复或乱序。客户端应用**已见 id 集合/LRU**去重，不能简单用「id 大于 lastId 才处理」丢弃迟到的低 id 事件；`lastEventId` 只用于补发游标，取已处理的最大 id 即可。
- **背压**：客户端应把事件放入本地队列并合并同实体短时间内的重复刷新（如 200ms debounce），避免事件风暴打爆 REST。

## 7. 发送端设计

### 7.1 契约与实现

```csharp
public interface IAccessEventPublisher
{
    Task PublishAsync(
        string eventName,
        AccessEventAudience audience,
        Guid? targetAppId = null,
        IReadOnlyDictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default);
}
```

- 契约在 `MoAI.Infra.Shared/Events`，实现在 `MoAI.Subsystem.Core`；业务模块只引用共享契约，不依赖子系统模块。
- **不抛异常**：业务已提交后再发事件，发布失败只记日志（实时提示丢失由对账兜底），不能反向让业务请求失败。
- **事务边界**：`SystemUpsertUserCommandHandler` 等显式事务的 Handler 必须在 `CommitAsync` 之后发布；禁止在事务内发事件，避免回滚后仍推送。非事务 Handler 在 `SaveChangesAsync` 之后发布。
- `source` 由 Publisher 从 `IHttpContextAccessor` 读取 `SubsysCallerContext` 自动填充；无调用方上下文时为空。请求头 `X-MoAI-Event-Echo: false` 时置 `suppressEcho=true`（默认回推）。
- 发布顺序：先 `XADD` 拿 `id`，再发 MQ `subsys.event`；Redis 不可用时生成 Guid v7 继续发 MQ（补发能力降级）。
- Stream 记录字段：`event/audience/target/source/suppress/time/data`；实时推送与重连补发使用同一套 audience + suppress 过滤，避免被抑制的回声在重连时又补回来。
- **载荷守卫**：关联 ID 数组构造侧先判长度，>50 直接降级为 `count + truncated`；Publisher 序列化 `data` 后 >2 KB 记 warning，>8 KB **拒绝发送该事件**并记 error（实时信号丢失由对账兜底），不允许大对象进入 Stream/MQ。

### 7.2 消费与路由

`AccessEventPushConsumer`（Qos=1）：
1. 按 `audience` 解析目标：`all` → 查询启用中的系统级接入 id 列表；`target` → 仅 `TargetAppId`。`suppressEcho=true` 时从目标集合剔除 `source`。
2. 启用列表实时查库（接入数量很小）；**不做进程内缓存**，保证新建/启用的接入立即收到后续事件。
3. `IHubContext<SubsysEventHub>.Clients.Group($"subsys:{id:N}").SendAsync("onEvent", item)`。
4. `status_changed=disabled` / `deleted` 走 target 组直发，跳过启用过滤，且不受 `suppressEcho` 影响（必须是最后一次投递）。
5. 失败重试耗尽后 Ack 放弃并记日志。

### 7.3 触发点（需要改动的 Handler）

> 下表事件名为简写，实际发送时统一加 `subsys.` 前缀（如 `user.created` → `subsys.user.created`）。

| 模块 | 触发点 |
|---|---|
| auth | `RegisterUserCommandHandler` → `user.created` |
| account | `SystemUpsertUserCommandHandler` → `user.created` / `user.updated` / `user.status_changed` / `user.oauth_linked`（按实际分支） |
| account | `UpdateUserInfoCommandHandler`、`UpdateUserAvatarCommandHandler` → `user.updated` |
| account | `UpdateUserIsDisableCommandHandler`、`SystemSetUserDisableCommandHandler` → `user.status_changed` |
| account | `OAuthBindExistAccountCommandHandler`、`OAuthBindAccountByCodeCommandHandler`、`UnbindUserOAuthCommandHandler` → `user.oauth_linked` / `oauth_unlinked` |
| app | app-tokens 自动建户/绑定 → `user.created` + `user.oauth_linked` |
| org | `Create/Update/DeleteOrgRoleCommandHandler` → `org.role_*` |
| org | `UpdateOrgUserRolesCommandHandler` → `user.org_roles_changed` |
| org | `OrgGrantService.ReplaceGrantsAsync` 且资源类型为 `Subsystem` → `access_app.grants_changed`（统一入口，覆盖 `/org/grant/subsystem/*` 与子系统管理面） |
| team | 建队/改资料/禁用/成员增删改/转让 Owner 各 Handler → `team.*` |
| subsystem | `SubsysAppCommandsHandler` Update/Reset/Delete → `access_app.updated` / `status_changed` / `secret_reset` / `deleted`（Create 不发自身事件） |
| subsystem | `SetSubsystemOrgGrantsCommandHandler` + 团队授权更新 → `access_app.grants_changed` |

> 组织授权从 `IOrgGrantService.ReplaceGrantsAsync` 单点发事件，避免子系统管理面与通用 `/org/grant` 管理面两处漏发。该接口有 long/Guid/string 多个重载，事件只在**最终写库路径**发一次，防止重载相互委托导致重复。

## 8. 安全与运维

- **面隔离**：仅 `level=system` 的 `subsystem`/`app` 接入可建连；团队级 `moai-ac-`、内部用户 JWT、用户委托 token、`moai-sa-` 直连一律不可作为 Hub 凭据。
- **实时吊销**：REST 每请求回库校验；Hub 建连回库校验；推送消费端按启用状态过滤。停用/删除后不再收到资源事件（仅最后一条自身状态/删除事件）。
- **密钥重置**：事件提示客户端重换 token；已签发的 access token 在自然过期前仍可用（当前无 token 版本号）。建议后续给 `access_app` 加 `secret_version` 并写入 token claim，实现即时吊销。
- **数据最小化**：严格执行 §4.4 载荷红线——只推主体 ID 与变更元数据，不推邮箱/手机/列表/正文；`userName` 作为外部幂等键保留。日志只记事件名/id/appId，不记 data。
- **事件风暴**：系统批量 upsert 默认仍回推（保证同接入多实例一致）；调用方确认不需要回声时显式带 `X-MoAI-Event-Echo: false`。消费端可后续加按 app 的合并/限速。
- **连接数**：每接入每实例一条长连接，规模在数十到数百量级；Redis backplane 足够。更大规模再评估 Azure SignalR/独立网关。

## 9. 演进约束

- 事件名 **append-only**：新增只加不改；废弃先文档标注并保留至少一个大版本，编号不复用。
- 信封只做加法；客户端必须忽略未知字段与未知事件。
- 后续可选：按接入订阅过滤、Webhook 兜底、持久化事件审计、连接注册表与强制断连、事件指标（连接数/发布数/补发数/对账次数）。
- 配套客户端：`clients/MoAI.Subsystem.Client` 已提供 `ISubsysEventClient`（SignalR 连接、自动刷新 token、`ISubsysEventCursorStore` 游标、重连补发/对账）与 `SubsysResourceClient`（grants/events/团队成员），接入方无需自写协议。

## 10. 落地内容（已完成）

1. `MoAI.Infra.Shared/Events` 契约 + Publisher（Redis Stream + MQ）+ Consumer + `SubsysEventHub` + Hub 认证方案 + `/api/subsys/events`。
2. 接入自身事件 + 用户事件（先把身份/禁用/绑定这条最关键的链路打通）。
3. 组织/团队事件 + `grants`、团队成员对账接口。
4. `clients/MoAI.Subsystem.Client` 的 SignalR 事件客户端与 `lastEventId` 持久化示例。
5. E2E 与 BDD/TDD 回填。

## 11. 验收（落地时）

建议新增 `local-dev/subsys-event-e2e.mjs`（Node `@microsoft/signalr`），场景从 `@SUB-S76` 起：

- 建连鉴权：机器 token 可连；用户 JWT / `moai-ac-` / 停用接入 / 已删除接入被拒。
- 用户事件：系统 upsert 新建、资料更新、禁用、组织角色覆盖、OAuth 绑定/解绑。
- 组织/团队事件：角色增改删、用户角色覆盖、建队、成员增删改、Owner 转让、团队禁用。
- 接入自身：改资料、授权变更、密钥重置、停用、删除；`target` 隔离（A 的事件不到 B）。
- 补发：断开后产生事件，带 `lastEventId` 重连收到补发；游标过期收到 `resync_required`。
- 对账：`GET /api/subsys/grants`、`GET /api/subsys/system/teams/{id}/users` 回显正确。
- 载荷规则：断言事件不含实体快照/完整列表，关联 ID 数组 ≤50；超 50 退化 `count + truncated`，超 8 KB 拒绝发送并留日志。
- 多实例：两个 MoAI 实例 + backplane，连接在 A、事件在 B 发布仍可达（手工/环境验证）。

## 12. 待定

1. Redis Stream 已作为 V1 补发介质；是否落库 `access_event` 做持久审计待后续评估。
2. 系统级 `app` 是否与 `subsystem` 收完全相同的事件面（本文推荐相同；二者资源读权限本就一致）。
3. 密钥重置是否要立即吊销已签发 token（需 `secret_version`）。
4. 是否开放按接入订阅过滤（减少无关事件量）。
5. 事件风暴的消费端合并/限速策略。
