# 应用工作台 Phase 1 实现计划（外壳 + 配置分区 + 调试会话）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把应用管理页升级为「应用工作台」，配置分区左配置右调试对话，调试会话仅存 Redis、不落库、未发布也能调试。

**Architecture:** 后端复用现有 AG-UI 对话链路（`/api/agent/{appId}/chat`），新增 Redis 调试会话注册表 `IDebugSessionRegistry`；`AppAgentDispatcher` 在 DB 查不到会话行时回落注册表解析，`AppChatFlushService` 因无 session 行天然不落库，`AppAgentFactory` 以 `isDebug` 跳过用量计数。前端新增工作台外壳（顶部 Tab）与调试对话面板。

**Tech Stack:** .NET 10 / MediatR / Maomi DI / StackExchange.Redis.Extensions / React 19 / antd 5 / Kiota / vitest。

> 注（2026-09-14 实施后调整）：用户要求工作台分区导航改为**左侧菜单**（`Layout` + `Sider` + `Menu`，同 `WikiDetail`），取代本计划原文的「顶部 Tab」；i18n key 由 `appWorkspace.tab*` 改为 `appWorkspace.menu*`。下文相关代码片段为当时的计划稿，实际实现以源码与 `docs/app/sdd.md` §5.4 / 设计文档 D27 为准。

**范围：** 本计划仅覆盖设计的 **Phase 1**（工作台外壳 + 配置分区 + 调试会话）。Phase 2 日志、Phase 3 监控、Phase 4 访问点各自另立计划（见设计文档分期）。

**关联设计：** `docs/superpowers/specs/2026-09-14-app-workspace-design.md`

---

## 文件结构

**后端（新增）**
- `src/ai/MoAI.AI.Shared/Models/DebugSessionRegistryEntry.cs` — 调试会话注册项模型
- `src/ai/MoAI.AI.Shared/Services/IDebugSessionRegistry.cs` — 注册表接口
- `src/ai/MoAI.AI.Core/Services/RedisDebugSessionRegistry.cs` — Redis 实现 + 键格式
- `src/app/MoAI.App.Shared/Commands/CreateDebugSessionCommand.cs` — 创建调试会话命令
- `src/app/MoAI.App.Core/Handlers/CreateDebugSessionCommandHandler.cs` — 命令处理
- `tests/MoAI.AI.Core.Tests/DebugSessionRegistryTests.cs` — 键格式单测

**后端（修改）**
- `src/app/MoAI.App.Core/MoAI.App.Core.csproj` — 增加对 `MoAI.AI.Shared` 的引用
- `src/ai/MoAI.AI.Core/Services/AppAgentDispatcher.cs` — 注册表回落解析
- `src/ai/MoAI.AI.Core/Services/AppAgentFactory.cs` — `isDebug` 跳过计数
- `src/app/MoAI.App.Api/Controllers/AppController.cs` — 调试会话端点

**前端（新增）**
- `ui/src/pages/teams/apps/chat/ChatMessageList.tsx` — 消息渲染展示组件
- `ui/src/pages/teams/apps/chat/AppDebugChat.tsx` — 调试对话面板
- `ui/src/pages/teams/apps/AppConfigSection.tsx` — 配置分区（左配置 + 右调试）
- `ui/src/pages/teams/apps/AppWorkspace.tsx` — 工作台外壳（顶部 Tab）
- `ui/src/pages/teams/apps/__tests__/AppWorkspace.test.tsx`
- `ui/src/pages/teams/apps/__tests__/AppDebugChat.test.tsx`

**前端（修改）**
- `ui/src/api/app.ts` — `createDebugSession`
- `ui/src/pages/teams/apps/AppChat.tsx` — 改用 `ChatMessageList`
- `ui/src/router/index.tsx` — 工作台路由
- `ui/src/i18n/locales/{zh-CN,en-US}/common.json` — 文案

**前端（删除）**
- `ui/src/pages/teams/apps/AppManage.tsx`（迁移完成后删除）
- `ui/src/pages/teams/apps/__tests__/AppManage.test.tsx`（迁移为 `AppConfigSection` 用例）

**文档**
- `docs/app/bdd.md`、`docs/app/tdd.md`、`docs/app/sdd.md`、`docs/app/sop.md`、`docs/rounds-log.md`
- `local-dev/app-e2e.mjs`

---

## Task 1: 调试会话注册表（接口 + Redis 实现）

**Files:**
- Create: `src/ai/MoAI.AI.Shared/Models/DebugSessionRegistryEntry.cs`
- Create: `src/ai/MoAI.AI.Shared/Services/IDebugSessionRegistry.cs`
- Create: `src/ai/MoAI.AI.Core/Services/RedisDebugSessionRegistry.cs`
- Test: `tests/MoAI.AI.Core.Tests/DebugSessionRegistryTests.cs`

- [ ] **Step 1: 写失败测试（键格式）**

创建 `tests/MoAI.AI.Core.Tests/DebugSessionRegistryTests.cs`：

```csharp
using System;
using MoAI.AI.Services;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 调试会话注册表的键格式.
/// </summary>
public class DebugSessionRegistryTests
{
    [Fact]
    public void SessionKey_UsesNamespacedNFormat()
    {
        var id = Guid.Parse("0198f2c1-1111-7000-8000-000000000000");

        Assert.Equal("appagent:debug:0198f2c1111170008000000000000000", RedisDebugSessionRegistry.SessionKey(id));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/MoAI.AI.Core.Tests/MoAI.AI.Core.Tests.csproj --filter DebugSessionRegistryTests`
Expected: 编译失败（`RedisDebugSessionRegistry` 不存在）。

- [ ] **Step 3: 创建注册项模型**

创建 `src/ai/MoAI.AI.Shared/Models/DebugSessionRegistryEntry.cs`：

```csharp
namespace MoAI.AI.Models;

/// <summary>
/// 调试会话注册项：记录临时调试会话归属的应用与用户；仅存 Redis，不落库.
/// </summary>
public class DebugSessionRegistryEntry
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 发起调试的用户 id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
```

- [ ] **Step 4: 创建注册表接口**

创建 `src/ai/MoAI.AI.Shared/Services/IDebugSessionRegistry.cs`：

```csharp
using MoAI.AI.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 调试会话注册表（Redis）：仅存调试会话的应用/用户归属，供对话运行时在无正式会话行时回落解析；
/// 不产生任何业务表记录，读取时滑动续期.
/// </summary>
public interface IDebugSessionRegistry
{
    /// <summary>
    /// 创建调试会话注册项.
    /// </summary>
    /// <param name="sessionId">调试会话 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">发起调试的用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task CreateAsync(Guid sessionId, Guid appId, int teamId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取调试会话注册项，并滑动续期 TTL.
    /// </summary>
    /// <param name="sessionId">调试会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>注册项；不存在返回 null.</returns>
    Task<DebugSessionRegistryEntry?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: 创建 Redis 实现**

创建 `src/ai/MoAI.AI.Core/Services/RedisDebugSessionRegistry.cs`：

```csharp
using Maomi;
using MoAI.AI.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// <see cref="IDebugSessionRegistry"/> 的 Redis 实现.
/// </summary>
[InjectOnSingleton]
public sealed class RedisDebugSessionRegistry : IDebugSessionRegistry
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisDebugSessionRegistry"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public RedisDebugSessionRegistry(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <summary>
    /// 调试会话注册表 Redis 键.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <returns>键名.</returns>
    public static string SessionKey(Guid sessionId) => $"appagent:debug:{sessionId:N}";

    /// <inheritdoc/>
    public async Task CreateAsync(Guid sessionId, Guid appId, int teamId, long userId, CancellationToken cancellationToken = default)
    {
        var entry = new DebugSessionRegistryEntry
        {
            AppId = appId,
            TeamId = teamId,
            UserId = userId,
            CreateTime = DateTimeOffset.Now,
        };

        await _redisDatabase.Database.StringSetAsync(SessionKey(sessionId), entry.ToRedisValue(), Ttl);
    }

    /// <inheritdoc/>
    public async Task<DebugSessionRegistryEntry?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var key = SessionKey(sessionId);
        var entry = await _redisDatabase.GetAsync<DebugSessionRegistryEntry>(key);
        if (entry != null)
        {
            await _redisDatabase.Database.KeyExpireAsync(key, Ttl);
        }

        return entry;
    }
}
```

> `ToRedisValue<T>` 扩展在 `MoAI` 命名空间（全局 using 可见）；`IRedisDatabase.GetAsync<T>` 与 `AppChatHotStore` 用法一致。

- [ ] **Step 6: 运行测试确认通过**

Run: `dotnet test tests/MoAI.AI.Core.Tests/MoAI.AI.Core.Tests.csproj --filter DebugSessionRegistryTests`
Expected: PASS 1/1。

- [ ] **Step 7: 提交**

```bash
git add src/ai/MoAI.AI.Shared/Models/DebugSessionRegistryEntry.cs src/ai/MoAI.AI.Shared/Services/IDebugSessionRegistry.cs src/ai/MoAI.AI.Core/Services/RedisDebugSessionRegistry.cs tests/MoAI.AI.Core.Tests/DebugSessionRegistryTests.cs
git commit -m "feat(ai): 调试会话 Redis 注册表"
```

---

## Task 2: 创建调试会话命令与处理

**Files:**
- Modify: `src/app/MoAI.App.Core/MoAI.App.Core.csproj`
- Create: `src/app/MoAI.App.Shared/Commands/CreateDebugSessionCommand.cs`
- Create: `src/app/MoAI.App.Core/Handlers/CreateDebugSessionCommandHandler.cs`

- [ ] **Step 1: 给 App.Core 增加 AI.Shared 引用**

修改 `src/app/MoAI.App.Core/MoAI.App.Core.csproj`，在 `ItemGroup` 的 ProjectReference 中追加一行：

```xml
		<ProjectReference Include="..\..\ai\MoAI.AI.Shared\MoAI.AI.Shared.csproj" />
```

- [ ] **Step 2: 创建命令**

创建 `src/app/MoAI.App.Shared/Commands/CreateDebugSessionCommand.cs`：

```csharp
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 创建应用调试会话：仅存 Redis 热态、不落库、不计用量，未发布应用也可调试；需要团队 Admin 及以上角色.
/// </summary>
public class CreateDebugSessionCommand : IRequest<SimpleGuid>, IUserIdContext, IModelValidator<CreateDebugSessionCommand>
{
    /// <summary>
    /// 应用 id（来自路由）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateDebugSessionCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不正确.");
    }
}
```

- [ ] **Step 3: 创建处理**

创建 `src/app/MoAI.App.Core/Handlers/CreateDebugSessionCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Services;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateDebugSessionCommand"/>
/// </summary>
public class CreateDebugSessionCommandHandler : IRequestHandler<CreateDebugSessionCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IDebugSessionRegistry _debugSessionRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDebugSessionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="debugSessionRegistry">调试会话注册表.</param>
    public CreateDebugSessionCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IDebugSessionRegistry debugSessionRegistry)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _debugSessionRegistry = debugSessionRegistry;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateDebugSessionCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("应用已被禁用.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持调试.") { StatusCode = 400 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("需要团队管理员才能调试应用.") { StatusCode = 403 };
        }

        var sessionId = Guid.CreateVersion7();
        await _debugSessionRegistry.CreateAsync(sessionId, app.Id, app.TeamId, request.ContextUserId, cancellationToken);

        return new SimpleGuid { Value = sessionId };
    }
}
```

- [ ] **Step 4: 编译后端**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error。

- [ ] **Step 5: 提交**

```bash
git add src/app/MoAI.App.Core/MoAI.App.Core.csproj src/app/MoAI.App.Shared/Commands/CreateDebugSessionCommand.cs src/app/MoAI.App.Core/Handlers/CreateDebugSessionCommandHandler.cs
git commit -m "feat(app): 创建调试会话命令"
```

---

## Task 3: 对话运行时支持调试会话

**Files:**
- Modify: `src/ai/MoAI.AI.Core/Services/AppAgentFactory.cs:70`
- Modify: `src/ai/MoAI.AI.Core/Services/AppAgentDispatcher.cs:46-148`

- [ ] **Step 1: 给工厂增加 isDebug**

修改 `src/ai/MoAI.AI.Core/Services/AppAgentFactory.cs`：

把方法签名（约第 70 行）改为：

```csharp
    public async Task<AIAgent> CreateAsync(Guid appId, int teamId, long userId, Guid sessionId, bool isDebug, CancellationToken cancellationToken)
```

在 XML 注释中 `sessionId` 后补：

```csharp
    /// <param name="isDebug">是否调试会话：true 时不包裹用量计数器（不计数）.</param>
```

把构造 `IChatClient` 的一行（约第 91 行）：

```csharp
        IChatClient chatClient = new UsageCapturingChatClient(inner, _usageCounter, _hotStore, pair.Value.Model.Id, teamId, userId, appId, sessionId);
```

改为：

```csharp
        // 调试会话不计入用量，避免污染应用的监控统计
        IChatClient chatClient = isDebug
            ? inner
            : new UsageCapturingChatClient(inner, _usageCounter, _hotStore, pair.Value.Model.Id, teamId, userId, appId, sessionId);
```

- [ ] **Step 2: 让派发器回落到注册表**

修改 `src/ai/MoAI.AI.Core/Services/AppAgentDispatcher.cs`：

把方法声明（约第 117 行）从 `private static async Task<AIAgent> ResolveInnerAsync` 改为实例方法 `private async Task<AIAgent> ResolveInnerAsync`（去掉 `static`）。

把从 `var row = ...` 到方法结尾（约第 135-148 行）替换为：

```csharp
        var row = await databaseContext.AppAgentSessions
            .Where(x => x.Id == sessionId)
            .Select(x => new { x.AppId, x.TeamId, x.CreateUserId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var factory = serviceProvider.GetRequiredService<AppAgentFactory>();

        if (row != null)
        {
            // 正式会话：会话不属于当前用户时按不存在处理，避免越权续聊
            if (row.CreateUserId != userId)
            {
                throw new BusinessException("会话不存在.") { StatusCode = 404 };
            }

            return await factory.CreateAsync(row.AppId, row.TeamId, userId, sessionId, false, cancellationToken).ConfigureAwait(false);
        }

        // 无正式会话行：回落调试会话注册表（Redis）；命中且本人时按调试装配，不落库、不计数
        var registry = serviceProvider.GetRequiredService<IDebugSessionRegistry>();
        var debug = await registry.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (debug == null || debug.UserId != userId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        return await factory.CreateAsync(debug.AppId, debug.TeamId, userId, sessionId, true, cancellationToken).ConfigureAwait(false);
```

> `IDebugSessionRegistry` 与 `AppAgentDispatcher` 同命名空间 `MoAI.AI.Services`，无需新增 using。删除原先在 `if (row == null || row.CreateUserId != userId)` 之后残留的 `var factory = ...; return ...` 两行。

- [ ] **Step 3: 确认无其它 CreateAsync 调用点**

Run: `Get-ChildItem -Recurse -Filter *.cs F:\workspace\moai\src | Select-String -Pattern "\.CreateAsync\(" | Select-String -Pattern "AppAgentFactory|factory\."`
Expected: 仅 `AppAgentDispatcher.cs`（新签名已同步）。若还有其它调用点，按新签名补 `false` 参数。

- [ ] **Step 4: 编译后端**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error。

- [ ] **Step 5: 提交**

```bash
git add src/ai/MoAI.AI.Core/Services/AppAgentFactory.cs src/ai/MoAI.AI.Core/Services/AppAgentDispatcher.cs
git commit -m "feat(ai): 对话运行时支持 Redis 调试会话"
```

---

## Task 4: 调试会话端点与前端封装

**Files:**
- Modify: `src/app/MoAI.App.Api/Controllers/AppController.cs`
- Modify: `ui/src/api/app.ts`
- Regenerate: `ui/src/api/client/**`（Kiota，禁手改）

- [ ] **Step 1: 增加 Controller 端点**

在 `src/app/MoAI.App.Api/Controllers/AppController.cs` 的 `QueryApp` 方法之前插入：

```csharp
    /// <summary>
    /// 创建应用调试会话：仅存 Redis 热态、不落库、不计用量，未发布应用也可调试；需要团队 Admin 及以上角色.
    /// </summary>
    /// <param name="id">应用 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回调试会话 <see cref="SimpleGuid"/>.</returns>
    [HttpPost("{id:guid}/debug/session")]
    public async Task<SimpleGuid> CreateDebugSession([FromRoute] Guid id, CancellationToken ct)
    {
        var cmd = new CreateDebugSessionCommand { AppId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }
```

- [ ] **Step 2: 编译并启动后端**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error。
然后启动：`cd src/MoAI && dotnet run`（保持运行，默认 :5000）。

- [ ] **Step 3: 重新生成 Kiota 客户端**

Run: `npm run syncapi`（workdir `ui`）
Expected: 成功，`ui/src/api/client/` 生成物更新，包含 debug session 的请求构建器。

- [ ] **Step 4: 确认生成的调用路径**

Run: `Get-ChildItem -Recurse ui\src\api\client\api\app | Select-String -Pattern "debug"`
Expected: 出现 `debug` 目录或 `debugSession` 构建器。记下实际路径（形如 `client.api.app.byId(appId).debug.session.post()`）。

- [ ] **Step 5: 前端封装**

在 `ui/src/api/app.ts` 的 `createAppSession` 之后追加（若 Step 4 的路径不同，按实际生成的构建器调整）：

```ts
/** 创建调试会话：未发布应用也可调试；会话仅存 Redis、不落库、不计用量，刷新即弃用 */
export async function createDebugSession(appId: string): Promise<string> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).debug.session.post()
  return String(res?.value ?? '')
}
```

- [ ] **Step 6: 类型检查**

Run: `npm run typecheck`（workdir `ui`）
Expected: 0 error。

- [ ] **Step 7: 提交**

```bash
git add src/app/MoAI.App.Api/Controllers/AppController.cs ui/src/api/client ui/src/api/app.ts ui/src/api/client/kiota-lock.json
git commit -m "feat(app): 调试会话端点与前端封装"
```

---

## Task 5: 前端消息渲染组件与调试对话面板

**Files:**
- Create: `ui/src/pages/teams/apps/chat/ChatMessageList.tsx`
- Create: `ui/src/pages/teams/apps/chat/AppDebugChat.tsx`
- Modify: `ui/src/pages/teams/apps/AppChat.tsx`

- [ ] **Step 1: 抽出消息渲染组件**

创建 `ui/src/pages/teams/apps/chat/ChatMessageList.tsx`：

```tsx
import { CopyOutlined, RobotFilled, ThunderboltFilled } from '@ant-design/icons'
import type { CSSProperties } from 'react'
import { Button, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

export interface DisplayMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  toolCalls?: string[]
}

export interface ChatMessageListProps {
  messages: DisplayMessage[]
  sending: boolean
  appAvatar?: string
  userName: string
  userAvatar?: string
  onCopy: (text: string) => void
  emptyState?: React.ReactNode
  style?: CSSProperties
}

/**
 * 对话消息流（用户/助手气泡、工具调用标签、Markdown 流式渲染）。
 * 供正式对话页 AppChat 与调试面板 AppDebugChat 复用；样式依赖全局 app-chat.css。
 */
export function ChatMessageList({
  messages,
  sending,
  appAvatar,
  userName,
  userAvatar,
  onCopy,
  emptyState,
  style,
}: ChatMessageListProps) {
  const { t } = useTranslation()

  if (messages.length === 0) {
    return <>{emptyState ?? null}</>
  }

  return (
    <div className="moai-chat__stream" style={style}>
      {messages.map((m, index) => {
        const isLast = index === messages.length - 1
        const streaming = sending && isLast && m.role === 'assistant'
        return (
          <div key={m.id} className={`moai-chat__row moai-chat__row--${m.role}`}>
            {m.role === 'assistant' ? (
              <div className="moai-chat__avatar moai-chat__avatar--ai">
                {appAvatar ? <img src={appAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} /> : <RobotFilled />}
              </div>
            ) : (
              <div className="moai-chat__avatar moai-chat__avatar--user">
                {userAvatar ? <img src={userAvatar} alt="" style={{ width: 34, height: 34, objectFit: 'cover' }} /> : userName.slice(0, 1).toUpperCase()}
              </div>
            )}

            <div className="moai-chat__msg">
              {m.role === 'assistant' ? (
                <>
                  {(m.toolCalls?.length ?? 0) > 0 && (
                    <div className="moai-chat__toolbar">
                      {m.toolCalls!.map((name, i) => (
                        <span key={`${name}-${i}`} className="moai-chat__tool-chip">
                          <ThunderboltFilled />
                          {name}
                        </span>
                      ))}
                    </div>
                  )}
                  <div className="moai-chat__assistant">
                    {m.content ? (
                      <div className="moai-chat__markdown">
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>{m.content}</ReactMarkdown>
                      </div>
                    ) : streaming ? (
                      <span className="moai-chat__typing">
                        <span />
                        <span />
                        <span />
                      </span>
                    ) : null}
                    {streaming && m.content && <span className="moai-chat__caret" />}
                  </div>
                  {m.content && !streaming && (
                    <div className="moai-chat__actions">
                      <Tooltip title={t('appChat.copy')}>
                        <Button type="text" size="small" icon={<CopyOutlined />} onClick={() => onCopy(m.content)} />
                      </Tooltip>
                    </div>
                  )}
                </>
              ) : (
                <div className="moai-chat__bubble">{m.content}</div>
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}
```

- [ ] **Step 2: 重构 AppChat 使用该组件**

修改 `ui/src/pages/teams/apps/AppChat.tsx`：

1. 删除本文件内的 `DisplayMessage` 接口定义，改为导入：

```tsx
import { ChatMessageList, type DisplayMessage } from './chat/ChatMessageList'
```

2. 删除已不再使用的 import：`CopyOutlined`、`RobotFilled`、`ThunderboltFilled`（若其它处不再使用）、`ReactMarkdown`、`remarkGfm`。保留 `BulbOutlined`、`PlusOutlined`（侧栏/建议仍用）。

3. 把 `<div className="moai-chat__scroll" ref={scrollRef}>` 内从 `{loading ? null : messages.length === 0 ? (` 到对应的消息 `map` 结束（约第 357-449 行）整段替换为：

```tsx
          {loading ? null : (
            <ChatMessageList
              messages={messages}
              sending={sending}
              appAvatar={appAvatar}
              userName={userName}
              userAvatar={userAvatar}
              onCopy={(text) => void copyMessage(text)}
              emptyState={
                <div className="moai-chat__stream">
                  <div className="moai-chat__hero">
                    <div className="moai-chat__hero-badge">
                      <ThunderboltFilled />
                    </div>
                    <h2 className="moai-chat__hero-title">{t('appChat.welcomeTitle')}</h2>
                    <p className="moai-chat__hero-subtitle">{t('appChat.welcomeSubtitle', { name: appName || t('appChat.title') })}</p>
                    <div className="moai-chat__suggestions">
                      {suggestions.map((text) => (
                        <button key={text} type="button" className="moai-chat__suggestion" onClick={() => applySuggestion(text)}>
                          <BulbOutlined className="moai-chat__suggestion-icon" />
                          {text}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              }
            />
          )}
```

> 若 `ThunderboltFilled` 仅用于 hero 与助手头像，保留其 import（helper 中 hero 仍在 AppChat 内）；`RobotFilled` 仅消息渲染用，可删除。

4. `DisplayMessage` 的用法（`useState<DisplayMessage[]>`）保持，类型改为从新组件导入。

- [ ] **Step 3: 运行现有 AppChat 测试**

Run: `npm run test -- AppChat`（workdir `ui`）
Expected: 现有 `AppChat.test.tsx` 全绿（重构不改变行为）。

- [ ] **Step 4: 创建调试对话面板**

创建 `ui/src/pages/teams/apps/chat/AppDebugChat.tsx`：

```tsx
import { useCallback, useEffect, useRef, useState } from 'react'
import { ClearOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Button, Input, Popconfirm, Tooltip, Typography } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import type { HttpAgent } from '@ag-ui/client'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { createDebugSession } from '@/api/app'
import { abortAppChat, createAppChatAgent, runAppChat } from '@/api/agentChat'
import { ChatMessageList, type DisplayMessage } from './ChatMessageList'

const { Text } = Typography

export interface AppDebugChatProps {
  appId: string
  appName: string
  appAvatar?: string
}

/**
 * 调试对话面板：进入即创建 Redis 调试会话（不落库、不计用量），
 * 页面刷新即放弃会话 id 并重新创建；后端注册表过期时自动重建一次并重试。
 */
export function AppDebugChat({ appId, appName, appAvatar }: AppDebugChatProps) {
  const { t } = useTranslation()
  const agentRef = useRef<HttpAgent | null>(null)
  const sessionIdRef = useRef('')
  const inputRef = useRef<TextAreaRef | null>(null)

  const [messages, setMessages] = useState<DisplayMessage[]>([])
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)

  const ensureSession = useCallback(async (): Promise<string> => {
    if (sessionIdRef.current) return sessionIdRef.current
    const id = await createDebugSession(appId)
    sessionIdRef.current = id
    return id
  }, [appId])

  useEffect(() => {
    sessionIdRef.current = ''
    setMessages([])
    void ensureSession().catch(() => undefined)
    return () => {
      if (agentRef.current) abortAppChat(agentRef.current)
    }
  }, [appId, ensureSession])

  const runOnce = useCallback(
    async (text: string, assistantId: string, sessionId: string) => {
      agentRef.current = createAppChatAgent(appId, sessionId)
      await runAppChat(agentRef.current, text, {
        onDelta: (buffer) => setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: buffer } : m))),
        onToolCall: (name) =>
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, toolCalls: [...(m.toolCalls ?? []), name] } : m))),
        onError: (message) =>
          setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: message || t('appDebug.runError') } : m))),
      })
    },
    [appId, t],
  )

  const send = useCallback(async () => {
    const text = input.trim()
    if (!text || sending) return

    const assistantId = crypto.randomUUID()
    setMessages((prev) => [...prev, { id: crypto.randomUUID(), role: 'user', content: text }, { id: assistantId, role: 'assistant', content: '' }])
    setInput('')
    setSending(true)

    try {
      const sessionId = await ensureSession()
      try {
        await runOnce(text, assistantId, sessionId)
      } catch {
        // 注册表过期：重建一次调试会话后重试
        sessionIdRef.current = ''
        const retryId = await ensureSession()
        await runOnce(text, assistantId, retryId)
      }
    } catch {
      setMessages((prev) => prev.map((m) => (m.id === assistantId ? { ...m, content: t('appDebug.runError') } : m)))
    } finally {
      setSending(false)
    }
  }, [ensureSession, input, runOnce, sending, t])

  const stop = useCallback(() => {
    if (agentRef.current) abortAppChat(agentRef.current)
    setSending(false)
  }, [])

  const clear = useCallback(async () => {
    if (agentRef.current) abortAppChat(agentRef.current)
    sessionIdRef.current = ''
    setMessages([])
    setSending(false)
    await ensureSession().catch(() => undefined)
  }, [ensureSession])

  const copy = useCallback(
    async (text: string) => {
      try {
        await navigator.clipboard.writeText(text)
        feedback.success(t('appChat.copied'))
      } catch {
        // 忽略剪贴板不可用
      }
    },
    [t],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 420 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm, marginBottom: spacing.sm }}>
        <div>
          <Text strong>{t('appDebug.title')}</Text>
          <div>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {t('appDebug.hint')}
            </Text>
          </div>
        </div>
        <Tooltip title={t('appDebug.clear')}>
          <Popconfirm
            title={t('appDebug.clearConfirm')}
            onConfirm={() => void clear()}
            okText={t('appManage.confirm')}
            cancelText={t('appManage.cancel')}
          >
            <Button type="text" size="small" icon={<ClearOutlined />} />
          </Popconfirm>
        </Tooltip>
      </div>

      <div style={{ flex: 1, overflow: 'auto', border: '1px solid var(--mc-border)', borderRadius: 8, padding: spacing.sm }}>
        <ChatMessageList
          messages={messages}
          sending={sending}
          appAvatar={appAvatar}
          userName={appName}
          onCopy={(text) => void copy(text)}
          emptyState={
            <div style={{ padding: spacing.lg, textAlign: 'center' }}>
              <Text type="secondary">{t('appDebug.empty')}</Text>
            </div>
          }
        />
      </div>

      <div style={{ display: 'flex', gap: spacing.sm, marginTop: spacing.sm }}>
        <Input.TextArea
          ref={inputRef}
          autoSize={{ minRows: 1, maxRows: 6 }}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onPressEnter={(e) => {
            if (!e.shiftKey) {
              e.preventDefault()
              void send()
            }
          }}
          placeholder={t('appDebug.placeholder')}
        />
        {sending ? (
          <Button icon={<StopOutlined />} onClick={stop}>
            {t('appDebug.stop')}
          </Button>
        ) : (
          <Button type="primary" icon={<SendOutlined />} disabled={!input.trim()} onClick={() => void send()}>
            {t('appDebug.send')}
          </Button>
        )}
      </div>
    </div>
  )
}
```

- [ ] **Step 5: 类型检查**

Run: `npm run typecheck`（workdir `ui`）
Expected: 0 error。

- [ ] **Step 6: 提交**

```bash
git add ui/src/pages/teams/apps/chat/ChatMessageList.tsx ui/src/pages/teams/apps/chat/AppDebugChat.tsx ui/src/pages/teams/apps/AppChat.tsx
git commit -m "feat(ui): 抽出对话消息组件并新增调试对话面板"
```

---

## Task 6: 工作台外壳与配置分区

**Files:**
- Create: `ui/src/pages/teams/apps/AppConfigSection.tsx`
- Create: `ui/src/pages/teams/apps/AppWorkspace.tsx`
- Modify: `ui/src/router/index.tsx`
- Delete: `ui/src/pages/teams/apps/AppManage.tsx`、`ui/src/pages/teams/apps/__tests__/AppManage.test.tsx`

- [ ] **Step 1: 迁移配置区**

把 `ui/src/pages/teams/apps/AppManage.tsx` 的**逻辑与两栏内容**迁到新文件 `ui/src/pages/teams/apps/AppConfigSection.tsx`，做三处调整：

1. 组件签名：

```tsx
export interface AppConfigSectionProps {
  teamId: number
  appId: string
  detail: AppDetail | null
  loading: boolean
  canManage: boolean
  onReload: () => Promise<void> | void
}

export function AppConfigSection({ teamId, appId, detail, loading, canManage, onReload }: AppConfigSectionProps) {
```

2. 把原 `AppManage` 自身的 `getAppDetail`/`load` 逻辑删除，改为用 `props.detail` 驱动；`handleSaveInfo`、`handlePublish`、`handleUnpublish`、`loadOptions` 保留；`handleSaveInfo` 成功后调用 `onReload()`。头像上传后调用 `onReload()`。`load` 中加载 agent config 的逻辑保留（仅依赖 `appId`）。

3. 返回 JSX 改为「左配置 + 右调试」：

```tsx
  const isAgent = detail?.appType !== 'workflow'

  return (
    <Row gutter={[spacing.md, spacing.md]} align="top">
      <Col xs={24} lg={15} xxl={16}>
        <DSCard title={t('appManage.sectionInfo')}>
          {/* 原左栏「应用信息」表单原样 */}
        </DSCard>
        <DSCard title={t('appManage.agentConfigTitle')} style={{ marginTop: spacing.md }}>
          {/* 原右栏「Agent 配置」表单原样；流程应用保留原提示 */}
        </DSCard>
      </Col>
      <Col xs={24} lg={9} xxl={8}>
        <DSCard title={t('appDebug.title')}>
          {isAgent && canManage ? (
            <AppDebugChat appId={appId} appName={detail?.name ?? ''} appAvatar={resolveStorageUrl(detail?.avatarPath ?? null) || undefined} />
          ) : (
            <Alert type="info" showIcon message={t('appDebug.adminOnly')} />
          )}
        </DSCard>
      </Col>
    </Row>
  )
```

需要的新 import：`import { AppDebugChat } from './chat/AppDebugChat'`、`Row`/`Col`/`Alert`（`Row`/`Col` 已有，补 `Alert`）。

- [ ] **Step 2: 创建工作台外壳**

创建 `ui/src/pages/teams/apps/AppWorkspace.tsx`：

```tsx
import { useCallback, useEffect, useState } from 'react'
import { Button, Popconfirm, Result, Space, Spin, Tabs, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppDetail, publishApp, unpublishApp, type AppKind } from '@/api/app'
import { AppConfigSection } from './AppConfigSection'

const { Text } = Typography

const ROLE_MEMBER = 0
const SECTIONS = ['config', 'logs', 'monitor', 'access'] as const
type SectionKey = (typeof SECTIONS)[number]

interface AppDetail {
  appId?: string | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  appType?: AppKind | null
  avatarPath?: string | null
  isExternal?: boolean | null
  isAuth?: boolean | null
  isPublic?: boolean | null
  publishStatus?: number | null
  myRole?: number | null
}

/**
 * 应用工作台：顶部 Tab 切换 配置 / 日志 / 监控（外部应用含 访问点）。
 * 配置分区为「左配置、右调试对话」；日志/监控/访问点在后续期次实现，本期为占位。
 */
export function AppWorkspace() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; appId: string; section?: string }>()
  const teamId = Number(params.teamId)
  const appId = params.appId ?? ''
  const rawSection = params.section ?? 'config'
  const section: SectionKey = (SECTIONS as readonly string[]).includes(rawSection) ? (rawSection as SectionKey) : 'config'

  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<AppDetail | null>(null)
  const [publishing, setPublishing] = useState(false)

  const canManage = (detail?.myRole ?? -1) > ROLE_MEMBER
  const isAgent = detail?.appType !== 'workflow'
  const isExternal = detail?.isExternal === true
  const isPublished = detail?.publishStatus === 1

  const load = useCallback(async () => {
    if (!appId) return
    setLoading(true)
    try {
      setDetail((await getAppDetail(appId)) as unknown as AppDetail)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [appId])

  useEffect(() => {
    void load()
  }, [load])

  const togglePublish = async () => {
    if (!appId) return
    setPublishing(true)
    try {
      if (isPublished) {
        await unpublishApp(appId)
        feedback.success(t('appManage.unpublishSuccess'))
      } else {
        await publishApp(appId)
        feedback.success(t('appManage.publishSuccess'))
      }
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

  const items = [
    { key: 'config', label: t('appWorkspace.tabConfig'), children: <AppConfigSection teamId={teamId} appId={appId} detail={detail} loading={loading} canManage={canManage} onReload={load} /> },
  ]

  if (canManage) {
    items.push(
      { key: 'logs', label: t('appWorkspace.tabLogs'), children: <Placeholder text={t('appWorkspace.logsComingSoon')} /> },
      { key: 'monitor', label: t('appWorkspace.tabMonitor'), children: <Placeholder text={t('appWorkspace.monitorComingSoon')} /> },
    )
  }

  if (canManage && isExternal) {
    items.push({ key: 'access', label: t('appWorkspace.tabAccess'), children: <Placeholder text={t('appWorkspace.accessComingSoon')} /> })
  }

  const validSection = items.some((i) => i.key === section) ? section : 'config'

  return (
    <Page
      breadcrumb={[
        { title: <Link to="/team">{t('team.title')}</Link> },
        { title: <Link to={`/team/${teamId}/apps`}>{t('team.apps')}</Link> },
        { title: detail?.name ?? '' },
      ]}
      extra={
        <Space>
          {isAgent && (
            isPublished ? <Tag color="green">{t('appManage.published')}</Tag> : <Tag>{t('appManage.unpublished')}</Tag>
          )}
          {isAgent && canManage && (
            <Popconfirm
              title={isPublished ? t('appManage.unpublishConfirm') : t('appManage.publishConfirm')}
              onConfirm={() => void togglePublish()}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
            >
              <Button type="primary" loading={publishing}>
                {isPublished ? t('appManage.unpublish') : t('appManage.publish')}
              </Button>
            </Popconfirm>
          )}
          {isAgent && isPublished && (
            <Button onClick={() => navigate(`/team/${teamId}/app/${appId}/chat`)}>{t('appManage.enterChat')}</Button>
          )}
          <Button onClick={() => navigate(`/team/${teamId}/apps`)}>{t('appManage.backToList')}</Button>
        </Space>
      }
    >
      {loading ? (
        <div style={{ padding: spacing.xl, textAlign: 'center' }}>
          <Spin />
        </div>
      ) : detail?.appId ? (
        <Tabs
          activeKey={validSection}
          items={items}
          onChange={(key) => navigate(key === 'config' ? `/team/${teamId}/app/${appId}` : `/team/${teamId}/app/${appId}/${key}`)}
        />
      ) : (
        <Result status="404" title={t('appManage.notFound')} />
      )}
    </Page>
  )
}

function Placeholder({ text }: { text: string }) {
  return (
    <div style={{ padding: spacing.xl, textAlign: 'center' }}>
      <Text type="secondary">{text}</Text>
    </div>
  )
}
```

> `data` 无需额外接口；日志/监控/访问点为 Phase 1 占位（后续期次替换 children）。

- [ ] **Step 3: 更新路由**

修改 `ui/src/router/index.tsx`：

1. 把 `import { AppManage } from '@/pages/teams/apps/AppManage'` 改为 `import { AppWorkspace } from '@/pages/teams/apps/AppWorkspace'`。
2. 把 `{ path: 'team/:teamId/app/:appId', element: <AppManage /> }` 改为：

```tsx
      { path: 'team/:teamId/app/:appId/:section?', element: <AppWorkspace /> },
```

> `/team/:teamId/app/:appId/chat` 的路由顺序在 `/:section?` 之前，React Router 静态段优先，`chat` 不会被 `:section` 吞掉。

- [ ] **Step 4: 删除旧管理页与旧测试**

Run:
```bash
git rm ui/src/pages/teams/apps/AppManage.tsx ui/src/pages/teams/apps/__tests__/AppManage.test.tsx
```

- [ ] **Step 5: 类型检查**

Run: `npm run typecheck`（workdir `ui`）
Expected: 0 error。若 `AppConfigSection` 有未用 import（如 `Page`、`UploadOutlined` 之外），按 lint 修正。

- [ ] **Step 6: 提交**

```bash
git add -A ui/src/pages/teams/apps ui/src/router/index.tsx
git commit -m "feat(ui): 应用工作台外壳与配置分区"
```

---

## Task 7: 文案、前端测试、E2E 与文档

**Files:**
- Modify: `ui/src/i18n/locales/zh-CN/common.json`
- Modify: `ui/src/i18n/locales/en-US/common.json`
- Create: `ui/src/pages/teams/apps/__tests__/AppWorkspace.test.tsx`
- Create: `ui/src/pages/teams/apps/__tests__/AppDebugChat.test.tsx`
- Modify: `local-dev/app-e2e.mjs`
- Modify: `docs/app/{bdd,tdd,sdd,sop}.md`、`docs/rounds-log.md`

- [ ] **Step 1: 补 i18n（zh-CN）**

在 `ui/src/i18n/locales/zh-CN/common.json` 顶层对象中加入 `appWorkspace` 与 `appDebug` 两个命名空间（`appManage` 保留）：

```json
  "appWorkspace": {
    "tabConfig": "配置",
    "tabLogs": "日志",
    "tabMonitor": "监控",
    "tabAccess": "访问点",
    "logsComingSoon": "对话日志看板将在后续版本提供。",
    "monitorComingSoon": "用量监控看板将在后续版本提供。",
    "accessComingSoon": "访问点（后端地址与嵌入组件）将在后续版本提供。"
  },
  "appDebug": {
    "title": "调试",
    "hint": "未发布也能调试 · 对话不保存",
    "clear": "清空对话",
    "clearConfirm": "确定清空当前调试对话？",
    "empty": "发送一条消息开始调试",
    "placeholder": "输入消息调试（Enter 发送 / Shift+Enter 换行）",
    "send": "发送",
    "stop": "停止",
    "runError": "对话运行失败，请稍后重试。",
    "adminOnly": "调试需要团队管理员权限。"
  },
```

- [ ] **Step 2: 补 i18n（en-US）**

在 `ui/src/i18n/locales/en-US/common.json` 对应加入：

```json
  "appWorkspace": {
    "tabConfig": "Configuration",
    "tabLogs": "Logs",
    "tabMonitor": "Monitor",
    "tabAccess": "Access",
    "logsComingSoon": "Conversation logs will be available in a later release.",
    "monitorComingSoon": "Usage monitoring will be available in a later release.",
    "accessComingSoon": "Access point (backend URL and embed widget) will be available in a later release."
  },
  "appDebug": {
    "title": "Debug",
    "hint": "Debug even when unpublished · conversations are not saved",
    "clear": "Clear",
    "clearConfirm": "Clear the current debug conversation?",
    "empty": "Send a message to start debugging",
    "placeholder": "Type a message (Enter to send / Shift+Enter for newline)",
    "send": "Send",
    "stop": "Stop",
    "runError": "Failed to run the conversation. Please try again.",
    "adminOnly": "Debugging requires team administrator permission."
  },
```

同时补 `appManage.notFound`（zh：`"notFound": "应用不存在"`；en：`"notFound": "App not found"`）。

- [ ] **Step 3: 写 AppDebugChat 测试**

创建 `ui/src/pages/teams/apps/__tests__/AppDebugChat.test.tsx`：

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppDebugChat } from '../chat/AppDebugChat'
import { createDebugSession } from '@/api/app'

vi.mock('@/api/app', () => ({
  createDebugSession: vi.fn().mockResolvedValue('0198f2c1-1111-7000-8000-000000000001'),
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

describe('AppDebugChat（调试对话面板）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(createDebugSession).mockResolvedValue('0198f2c1-1111-7000-8000-000000000001')
  })

  it('进入即创建调试会话并提示对话不保存', async () => {
    render(
      <MemoryRouter>
        <AppDebugChat appId="a1" appName="客服助手" />
      </MemoryRouter>,
    )

    expect(screen.getByText(/对话不保存/)).toBeTruthy()
    await waitFor(() => expect(createDebugSession).toHaveBeenCalledWith('a1'))
  })
})
```

- [ ] **Step 4: 写 AppWorkspace 测试**

创建 `ui/src/pages/teams/apps/__tests__/AppWorkspace.test.tsx`：

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AppWorkspace } from '../AppWorkspace'
import { getAppDetail } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppAgentConfig: vi.fn().mockResolvedValue({ appId: 'a1', appType: 'agent', prompt: '', modelId: null, wikiIds: [], plugins: [] }),
  saveAppAgentConfig: vi.fn(),
  updateApp: vi.fn(),
  uploadAppAvatar: vi.fn(),
  publishApp: vi.fn(),
  unpublishApp: vi.fn(),
  createDebugSession: vi.fn().mockResolvedValue('0198f2c1-1111-7000-8000-000000000001'),
}))
vi.mock('@/api/gateway', () => ({ getTeamGatewayModels: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/team-plugin', () => ({ getTeamPlugins: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/wiki', () => ({ getWikis: vi.fn().mockResolvedValue({ items: [] }) }))
vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn(() => ({})),
  runAppChat: vi.fn().mockResolvedValue(undefined),
  abortAppChat: vi.fn(),
}))

function renderPage(path = '/team/3/app/a1') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/team/:teamId/app/:appId/:section?" element={<AppWorkspace />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppWorkspace（应用工作台）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      teamId: '3',
      name: '客服助手',
      appType: 'agent',
      avatarPath: '',
      isExternal: false,
      publishStatus: 0,
      myRole: 2,
    } as never)
  })

  it('内部应用展示配置/日志/监控三个 Tab，无访问点', async () => {
    renderPage()
    expect(await screen.findByRole('tab', { name: '配置' })).toBeTruthy()
    expect(screen.getByRole('tab', { name: '日志' })).toBeTruthy()
    expect(screen.getByRole('tab', { name: '监控' })).toBeTruthy()
    expect(screen.queryByRole('tab', { name: '访问点' })).toBeNull()
  })

  it('外部应用额外展示访问点 Tab', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a2',
      teamId: '3',
      name: '外部客服',
      appType: 'agent',
      avatarPath: '',
      isExternal: true,
      publishStatus: 0,
      myRole: 2,
    } as never)

    renderPage('/team/3/app/a2')

    expect(await screen.findByRole('tab', { name: '访问点' })).toBeTruthy()
  })

  it('Member 只看到配置 Tab，无调试对话', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      teamId: '3',
      name: '客服助手',
      appType: 'agent',
      avatarPath: '',
      isExternal: false,
      publishStatus: 1,
      myRole: 0,
    } as never)

    renderPage()

    expect(await screen.findByRole('tab', { name: '配置' })).toBeTruthy()
    await waitFor(() => expect(screen.queryByRole('tab', { name: '日志' })).toBeNull())
    expect(screen.getByText(/调试需要团队管理员权限/)).toBeTruthy()
  })
})
```

- [ ] **Step 5: 运行前端测试与检查**

Run: `npm run typecheck; npm run lint; npm run test`（workdir `ui`）
Expected: 全部通过。

- [ ] **Step 6: 补 E2E 场景**

在 `local-dev/app-e2e.mjs` 的应用创建区块之后（`AGENT_ID` 定义之后）追加：

```js
  // AP-40 调试会话：未发布也能创建，且不落库、不计用量
  {
    const dbgOutsider = await api('POST', `/api/app/${AGENT_ID}/debug/session`, { token: outsider.token })
    check('AP-40a 非成员创建调试会话 404', dbgOutsider.status === 404, `${dbgOutsider.status}`)

    const dbgMember = await api('POST', `/api/app/${AGENT_ID}/debug/session`, { token: member.token })
    check('AP-40b Member 创建调试会话 403', dbgMember.status === 403, `${dbgMember.status}`)

    const dbgFlow = await api('POST', `/api/app/${WORKFLOW_ID}/debug/session`, { token: owner.token })
    check('AP-40c 流程应用创建调试会话 400', dbgFlow.status === 400, `${dbgFlow.status}`)

    const dbg = await api('POST', `/api/app/${AGENT_ID}/debug/session`, { token: owner.token })
    check('AP-40d Owner 创建调试会话 200 且返回 Guid', dbg.status === 200 && isGuid(dbg.json?.value), `${dbg.status} ${dbg.text.slice(0, 140)}`)

    const before = await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })
    const debugSessionId = String(dbg.json?.value ?? '')
    const after = await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })
    check('AP-40e 调试会话不进入正式会话列表', (after.json?.items ?? []).every((s) => String(s.sessionId) !== debugSessionId),
      `before=${(before.json?.items ?? []).length} after=${(after.json?.items ?? []).length}`)

    const dbgNotFound = await api('POST', '/api/app/01924f5e-0000-7000-8000-00000000ffff/debug/session', { token: owner.token })
    check('AP-40f 不存在应用调试会话 404', dbgNotFound.status === 404, `${dbgNotFound.status}`)
  }
```

> 该用例只验证创建与不落库；真正对话（AG-UI SSE）依赖可用模型与渠道，E2E 不强制覆盖。

- [ ] **Step 7: 运行 E2E（需后端运行中）**

Run: `node local-dev/app-e2e.mjs`（workdir 仓库根，后端 :5000 运行中）
Expected: 全绿，含 AP-40a~f。

- [ ] **Step 8: 更新文档**

- `docs/app/bdd.md`：新增 `## Feature: 应用工作台与调试`，场景 `@AP-S40`（Admin 对未发布 Agent 应用创建调试会话；调试会话不进入正式会话列表）、`@AP-S41`（Member/非成员/流程应用被拒）。标签 `@auto:e2e`。
- `docs/app/tdd.md`：补映射 `@AP-S40 → local-dev/app-e2e.mjs#AP-40`、`@AP-S41 → local-dev/app-e2e.mjs#AP-40`、`AppWorkspace → ui/src/pages/teams/apps/__tests__/AppWorkspace.test.tsx`、`AppDebugChat → ui/src/pages/teams/apps/__tests__/AppDebugChat.test.tsx`；执行并记录 PASS 与日期。
- `docs/app/sdd.md`：新增「5.4 应用工作台」小节与「关键决策」D34/D35/D36（见设计文档 D27–D30）；把状态行日期增补 2026-09-14；「已知问题」补「日志/监控/访问点为占位」。
- `docs/app/sop.md`：排障表补「调试会话不可续接 → Redis 注册表 2h 过期，刷新页面重建」。
- `docs/rounds-log.md`：记账本轮闭环与证据。

- [ ] **Step 9: 最终验证与提交**

Run:
```bash
dotnet build src/MoAI/MoAI.csproj
```
（workdir `ui`）`npm run typecheck; if ($?) { npm run lint }; if ($?) { npm run test }`
Expected: 全绿。

```bash
git add -A
git commit -m "test(app): 工作台与调试会话测试、E2E 与文档"
```

---

## Phase 1 完成标准

- 未发布 Agent 应用可由团队 Admin 在配置分区右侧发起调试对话，消息不写 `app_agent_session`/`app_agent_message`、不计入用量。
- 刷新页面后旧调试会话不可续接（前端丢弃 id）。
- Member 只能看配置（只读），无日志/监控/调试；非成员 404。
- 内部应用 3 个 Tab、外部应用 4 个 Tab；`/team/:teamId/app/:appId/chat` 仍可正常进入正式对话。
- `dotnet build` 0 error；`typecheck`/`lint`/`test` 全绿；`app-e2e.mjs` 全绿。

## 后续期次（各自另立计划）

- **Phase 2 日志**：`QueryAppLogsCommand` + `/app/{id}/logs`、`/app/{id}/logs/{sessionId}/messages` + `AppLogsSection`。
- **Phase 3 监控**：`QueryAppUsageCommand` + `/app/{id}/usage` + `AppMonitorSection`（依赖用量资源 id 字符串化迁移）。
- **Phase 4 访问点**：外部应用 `AppAccessSection` 占位转实（后端地址/前端嵌入地址/JS 悬浮组件）。

## Self-Review

- **Spec 覆盖**：外壳与路由（Task 6）、配置分区左右布局（Task 6）、调试会话 Redis 注册表（Task 1）、创建接口（Task 2/4）、运行时回落与不落库/不计数（Task 3）、前端调试面板与刷新弃用（Task 5）、日志/监控/访问点占位（Task 6）、测试与文档（Task 7）。Phase 2–4 明确另立计划，属设计分期而非缺口。
- **占位扫描**：无 TBD/TODO；Kiota 生成路径给了验证步骤以防路径差异，非占位。
- **类型一致性**：`IDebugSessionRegistry.CreateAsync/GetAsync` 在 Task 1 定义，Task 2/3 使用一致；`AppAgentFactory.CreateAsync(...,bool isDebug,...)` 在 Task 3 定义并同步唯一调用点；前端 `createDebugSession`、`AppDebugChatProps`、`AppConfigSectionProps` 在后续 Task 使用一致。
- **风险**：`ResolveInnerAsync` 由静态改实例需确认无其它静态调用（Task 3 Step 3 已加校验）。
