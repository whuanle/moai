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
    "Secret": "******"
  }
}
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
    public async Task<SystemUpsertUserCommandResponse?> UpsertUserAsync()
        => await client.Api.Subsys.System.Users.PostAsync(new SystemUpsertUserCommand { /* ... */ });
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

## 本地测试

```bash
dotnet test tests/MoAI.Subsystem.Client.Tests
```

8 个用例覆盖：token 换取/主动刷新/轮换失败回退、appid 直连、401 重试、403 错误语义、heartbeat 请求形状、DI 装配，全部走内存桩 handler，无需真实后端。
