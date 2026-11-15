# MoAI.Subsystem.Client — 子系统协作面 .NET 客户端库

MoAI 子系统协作面（`/api/subsys/**`，机器 token 鉴权）的 .NET 客户端库。API 代码由 Microsoft Kiota 从 `/openapi/subsys.json` 生成，接入方引用本项目即可对接，接口变更后一键重新生成。

```
MoAI.Subsystem.Client/
├─ Generated/                              ← Kiota 生成物（整体重生成，禁止手改）
│  ├─ SubsysClient.cs  Api/  Models/  kiota-lock.json
├─ MoaiSubsysClientOptions.cs              ← 配置（BaseUrl/AppId/Secret/超时/刷新窗口）
├─ SubsysTokenProvider.cs                  ← 机机 token 管理：换取/主动刷新/轮换缓存/appid 直连
├─ SubsysErrorHandler.cs                   ← 非 2xx → SubsysException（token 管道用）
├─ SubsysAuthRetryHandler.cs               ← 401 作废缓存重试一次 + 错误转换（业务管道用）
├─ SubsysClientServiceCollectionExtensions.cs ← AddMoaiSubsysClient(...) DI 注册
├─ SubsysClientFactory.cs                  ← 静态工厂（用户委托 token 等固定凭据场景）
├─ Events/                                 ← SignalR 事件客户端（/hub/subsys、lastEventId、去重、本地过滤、断线重连）
│  ├─ SubsysEventNames.cs                  ← 事件名常量（与服务端 append-only 目录一致）
│  ├─ SubsysEventItemExtensions.cs         ← data 读取助手（字符串/数字双形态 ID、数组、truncated）
│  └─ ISubsysEventDeduplicator.cs          ← 事件 id 去重窗口（默认 LRU 2048）
├─ SubsysEventClientOptions.cs             ← 事件客户端配置（Hub 路径/去重/退避/本地过滤）
├─ SubsysEventWorker.cs                    ← 事件后台宿主（随应用 Start/Stop）
├─ SubsysHeartbeatOptions.cs               ← 心跳配置（实例标识/间隔/版本）
├─ SubsysHeartbeatWorker.cs                ← 心跳后台宿主（自动上报在线）
├─ Resources/                              ← 授权快照/事件补发与目录/心跳/用户·组织·团队对账
└─ SubsysException.cs                      ← 失败异常（状态码 + error.type/error.code）
```

生成代码与手写包装物理隔离：重生成脚本只清空 `Generated/`，DI、配置、token 管理等固定代码永不覆盖。

## 快速开始

### 1. 注册服务

```csharp
// 方式一：appsettings.json（配置节默认名 MoaiSubsys）
builder.Services.AddMoaiSubsysClient(builder.Configuration);

// 方式二：代码配置
builder.Services.AddMoaiSubsysClient(o =>
{
    o.BaseUrl = "https://moai.example.com";
    o.AppId = "my-subsystem";
    o.Secret = "******";
});
```

`appsettings.json`：

```json
{
  "MoaiSubsys": {
    "BaseUrl": "https://moai.example.com",
    "AppId": "my-subsystem",
    "Secret": "******",
    "Events": {
      "DedupeCapacity": 2048,
      "EventPrefixes": ["subsys.user.", "subsys.team."]
    }
  }
}
```

需要事件长连接随应用自动启停时再加一行：

```csharp
builder.Services.AddMoaiSubsysClient(builder.Configuration)
    .AddMoaiSubsysEventWorker()      // 事件长连接随应用启停
    .AddMoaiSubsysHeartbeatWorker(); // 心跳（多实例在线）随应用启停
```

> **BaseUrl 填主机根地址**，不要带 `/api` 路径——协作面端点以 `/api/subsys/**` 开头，由客户端自动拼接（注册时强校验，带路径会在启动时报错）。

### 2. 注入使用

```csharp
public class MyService(SubsysClient client)
{
    // 自省：身份/状态/在线概况
    public async Task<QuerySubsysAppInfoCommandResponse?> GetInfoAsync()
        => await client.Api.Subsys.Info.GetAsync();

    // 心跳（每实例 ≤20s 一次）
    public async Task HeartbeatAsync(Guid subsystemId, string instanceId)
        => await client.Api.Subsys.Heartbeat.PostAsync(new SubsystemHeartbeatCommand
        {
            SubsystemId = subsystemId,
            InstanceId = instanceId,
        });

    // 系统资源：用户 upsert / 组织角色 / 团队治理（api/subsys/system/**）
    // 可携带 OauthProviderId/OauthSub/OauthUnionId 幂等绑定第三方身份（响应 OauthBound）
    public async Task<SystemUpsertUserCommandResponse?> UpsertUserAsync()
        => await client.Api.Subsys.System.Users.PostAsync(new SystemUpsertUserCommand { /* ... */ });

    // 内部应用访问点：按第三方登录绑定换算站内用户，颁发绑定单应用的访问用户 token
    // （未绑定用户返回 404，需先 UpsertUserAsync 携带 oauth 绑定）
    public async Task<SystemIssueAppAccessTokenCommandResponse?> IssueAccessTokenAsync(Guid appId, Guid providerId, string sub)
        => await client.Api.Subsys.System.AppTokens.PostAsync(new SystemIssueAppAccessTokenCommand
        {
            AppId = appId,
            OauthProviderId = providerId,
            OauthSub = sub,
        });
}
```

### 3. 认证（自动处理）

客户端自动管理机器凭据，接入方无需手工取 token：

- **子系统自填 appid**（配 `Secret`）：首次请求自动 `POST /api/subsys/token` 换取 Bearer（服务端 2 小时有效期），到期前 5 分钟（`RefreshSkewSeconds` 可调）用 `POST /api/subsys/token/refresh` 轮换；轮换被拒（吊销/重置）自动回退 appid+secret 全量换取。
- **应用接入 moai-sa- appid**（`Secret` 留空）：appid 直连，直接以 appid 作 Bearer，不走 token 流程。
- **401 兜底**：业务请求遇 401（token 被吊销/服务端重启）自动作废缓存、重取 token 并重试一次。
- 线程安全：并发请求共享同一 token 缓存，`SemaphoreSlim` 串行化换取。

### 4. 错误处理

非 2xx 响应统一抛 `SubsysException`（替代 Kiota 泛型 ApiError），携带服务端错误语义：

```csharp
try
{
    await client.Api.Subsys.Info.GetAsync();
}
catch (SubsysException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
{
    // ex.ErrorCode == "invalid_token"：凭据失效（appid 被删/密钥重置/token 不合法）
}
catch (SubsysException ex) when (ex.ErrorCode == "subsys_app_disabled")
{
    // 403：接入被停用
}
```

错误体两种形态均可解析：协作面中间件 `{error:{type,code,message}}` 与业务/校验错误 `{message}` / `{errors:[...],name}`；`RawContent` 保留原始响应体。

### 5. 用户委托 token（SSO 场景）

委托 SSO 兑换的用户 token（`POST /api/subsys/sso/exchange`）有效期独立于机器 token。对单个用户会话发起调用时，用静态工厂创建独立客户端（不走 DI、不做自动刷新，刷新用生成代码 `client.Api.Subsys.Sso.Refresh.PostAsync` 自行管理）：

```csharp
var exchanged = await machineClient.Api.Subsys.Sso.Exchange.PostAsync(new SubsystemSsoExchangeCommand
{
    SubsystemId = subsystemId,
    Code = oneTimeCode,
});

var userClient = SubsysClientFactory.CreateWithAccessToken("https://moai.example.com", exchanged!.AccessToken!);
await userClient.Api.Subsys.System.Users.List.GetAsync();
```

## 6. 实时事件订阅（推荐）

`AddMoaiSubsysClient` 同时注册 `ISubsysEventClient`、`ISubsysEventDeduplicator` 与 `SubsysResourceClient`。事件客户端主动外连 MoAI
`/hub/subsys`，按 appId 分组接收，断线自动重连并按 `lastEventId` 补发；游标失效时触发 `ResyncRequired`。

### 6.1 订阅与处理

```csharp
public sealed class MyEventHandler(ISubsysEventClient events, SubsysResourceClient resources)
{
    public async Task StartAsync(CancellationToken ct)
    {
        events.EventReceived += async e =>
        {
            // 事件只带主体/关联 ID 与轻量上下文，详情按需回查
            switch (e.Event)
            {
                case SubsysEventNames.UserStatusChanged:
                    var userId = e.GetDataInt64("userId");
                    var disabled = e.GetDataBool("disabled");
                    break;
                case SubsysEventNames.TeamMemberAdded:
                    var teamId = e.GetDataInt64("teamId");
                    var userIds = e.GetDataStrings("userIds"); // 超 50 个时为空，改看 e.IsTruncated()
                    var members = await resources.GetTeamUsersAsync(teamId!.Value);
                    break;
            }
        };
        events.ResyncRequired += async reason =>
        {
            // 首次连接/游标过期/补发超限：按业务做全量对账
            var info = await resources.GetInfoAsync();
            var grants = await resources.GetGrantsAsync();
            // 用户/组织/团队分页对账见下方补充资源接口
        };
        events.Disconnected += async ex => { /* 观测/告警；自动重连已内置 */ };

        await events.StartAsync(ct);
        await events.WaitForConnectedAsync(TimeSpan.FromSeconds(10), ct);
    }
}
```

- 载荷读取统一走 `SubsysEventItemExtensions`：ID 兼容字符串/数字形态，数组/计数/`truncated` 无需手写 `JsonElement`。
- **去重内建**：按事件 id 的 LRU 窗口（默认 2048，`Events.DedupeCapacity`，0=关闭）。只在事件成功处理后登记，处理失败不推进游标也不登记，下次补发会重试。
- **过滤内建**：`Events.EventPrefixes` / `ExcludedEvents` / `ExcludedEventPrefixes` 本地筛选回调；被过滤事件仍推进游标，避免重连反复补发。
- **进程内游标**默认实现；跨重启续传可替换 `ISubsysEventCursorStore`（Redis/数据库），并覆写 `GetLastEventIdAsync/SaveLastEventIdAsync/ClearLastEventIdAsync` 避免同步阻塞：

```csharp
services.AddSingleton<ISubsysEventCursorStore, MyRedisCursorStore>();
```

### 6.2 事件目录与 REST 补发

服务端事件契约可自描述，客户端无需硬编码：

```csharp
var info = await resources.GetInfoAsync();
Console.WriteLine($"{info.Events.HubPath} / schema v{info.Events.SchemaVersion} / 保留 {info.Events.RetentionDays} 天");

var catalog = await resources.GetEventCatalogAsync();
foreach (var descriptor in catalog.Items)
{
    Console.WriteLine($"{descriptor.Event} [{descriptor.Audience}] {string.Join(",", descriptor.DataFields)}");
}

// 按领域/事件名过滤补发（过滤发生在服务端取 limit 之前）
var page = await resources.QueryEventsAsync(afterId: cursor, limit: 200, eventName: null, eventPrefix: SubsysEventNames.TeamPrefix);
foreach (var e in page.Items) { /* ... */ }
cursor = page.NextCursor ?? cursor;

// 或自动翻页（resyncRequired / 无更多时结束）
await foreach (var e in resources.ReadEventsAsync(afterId: cursor, eventPrefix: SubsysEventNames.UserPrefix))
{
    // ...
}
```

- 响应 `nextCursor`：有更多时与末条 id 一致；达到服务端单次扫描上限但命中不足时也可能回填扫描边界，按它翻页即可。
- `nextCursor` 为空表示没有更多；`resyncRequired=true` 表示游标失效，应走全量对账而不是继续翻页。
- 事件目录只列字段名（值仍需按需回查）；客户端仍应忽略未知事件/未知字段（append-only 演进）。

补充资源接口：

- `GetGrantsAsync()` → `/api/subsys/grants`（团队范围/授权团队/组织授权）
- `GetInfoAsync()` → `/api/subsys/info`（身份/状态/在线实例数/事件通道自描述）
- `GetEventsAsync(afterId, limit)` / `QueryEventsAsync(...)` / `ReadEventsAsync(...)` → `/api/subsys/events`（REST 补发兜底 + 过滤 + 翻页）
- `GetEventCatalogAsync()` → `/api/subsys/events/catalog`（事件目录）
- `HeartbeatAsync(instanceId, version)` → `/api/subsys/heartbeat`（回显在线实例数/窗口/建议间隔）
- `GetUserAsync(userId)` / `ListUsersAsync(pageNo, pageSize, searchText)` → `/api/subsys/system/users/*`
- `ListOrgRolesAsync(search)` / `GetUserOrgRolesAsync(userId)` / `GetOrgRoleUsersAsync(roleId, search)` → `/api/subsys/system/org/*`
- `ListTeamsAsync()` / `GetTeamUsersAsync(teamId)` → `/api/subsys/system/teams/*`
- 全部资源接口非 2xx 统一抛 `SubsysException`（与 Kiota 管道同口径）。

> 子系统多实例建议：每个实例都可连接事件；若副作用必须只执行一次，用事件 `Id` 做共享去重（替换 `ISubsysEventDeduplicator`），或只让 leader 实例连接。
> 事件处理失败不会推进游标，下次重连会重新补发；`ResyncRequired` 后游标与去重窗口都会清空。

### 6.3 心跳（多实例在线）

不注册 `AddMoaiSubsysHeartbeatWorker()` 时也可手工调用：

```csharp
var beat = await resources.HeartbeatAsync(instanceId: "node-a", version: "1.2.3");
// beat.OnlineInstances：本次心跳计入后的在线实例数
// beat.RecommendedIntervalSeconds：建议间隔（默认 20s，服务端窗口 60s）
```

自动宿主会：首次启动立即上报 → 按服务端建议/配置间隔续期 → 401/403/网络异常只记日志并继续下一轮；实例标识未配置时按「机器名-进程 id-随机后缀」自动生成并保持稳定。

## 接口变更后：重新生成

```bash
# 前置：后端运行中（src/MoAI 下 dotnet run），kiota CLI 已安装（与前端 syncapi 同一依赖，1.27.0）
node clients/sync-api.mjs
# 指定地址（端口不同/远程环境）：
node clients/sync-api.mjs http://127.0.0.1:5210/openapi/subsys.json
```

脚本只清空并重写 `Generated/`（`kiota-lock.json` 记录生成参数），随后 `dotnet build` 校准手写包装对生成表面的引用（生成的方法/模型名变更会以编译错误暴露）。

## 配置项

| 键 | 必填 | 默认 | 说明 |
|---|---|---|---|
| `BaseUrl` | ✅ | - | 主机根地址，不含 `/api` |
| `AppId` | ✅ | - | 接入标识（子系统自填 appid 或 moai-sa- 应用接入） |
| `Secret` | moai-sa- 可空 | - | 接入密钥；moai-sa- 前缀 appid 留空走直连 |
| `TimeoutSeconds` | - | 100 | HTTP 请求超时 |
| `RefreshSkewSeconds` | - | 300 | 提前刷新窗口（秒） |
| `Events.HubPath` | - | `/hub/subsys` | 事件 Hub 路径（可由 `info.events.hubPath` 覆盖） |
| `Events.DedupeCapacity` | - | 2048 | 事件 id 去重窗口；0=关闭 |
| `Events.ReconnectInitialDelaySeconds` | - | 2 | 重连初始退避（秒） |
| `Events.ReconnectMaxDelaySeconds` | - | 30 | 重连最大退避（秒） |
| `Events.EventPrefixes` | - | 空（全部） | 本地事件前缀白名单 |
| `Events.ExcludedEvents` | - | 空 | 本地排除的事件名 |
| `Events.ExcludedEventPrefixes` | - | 空 | 本地排除的事件前缀 |
| `Heartbeat.InstanceId` | - | 自动生成 | 实例标识（1~64 字符，进程内稳定） |
| `Heartbeat.IntervalSeconds` | - | 0 | 心跳间隔；0=跟随服务端建议（20s） |
| `Heartbeat.Version` | - | - | 上报版本号 |

## 本地测试

```bash
dotnet test tests/MoAI.Subsystem.Client.Tests
```

34 个用例覆盖：token 换取/主动刷新/轮换失败回退、appid 直连、401 重试、403 错误语义、heartbeat 请求形状、DI 装配、授权快照/事件补发/事件目录/自省/用户/组织/团队补充接口、事件游标异步默认实现、事件载荷读取（双形态 ID/数组/truncated）、事件去重窗口、本地过滤与重连退避、事件与心跳后台宿主启停、资源接口错误语义，全部走内存桩 handler，无需真实后端。
