# 内部应用第三方身份自动初始化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 让第三方系统第一次为用户申请内部应用访问用户 token 时，MoAI 在同一条链路内幂等创建用户并绑定 OAuth 身份，后续继续复用该绑定完成登录、会话归属和 token 刷新。

**Architecture:** 保留 `oauth_connection`/`user_oauth_connection` 作为第三方登录身份的唯一映射，不向第三方系统暴露或接受可伪造的 MoAI `userId`。`POST /api/subsys/system/app-tokens` 先校验目标内部应用和 OAuth 渠道，再按 `(providerId, sub/unionId)` 查找用户；未命中时调用账户领域已有的系统建户命令，以稳定生成的机器用户名创建用户并在事务内写入绑定，最后签发现有 `typ=normal`、单应用绑定的访问用户 token。并发请求通过数据库唯一约束 + 重新查询收敛到同一个身份，接口响应增加是否新建用户的标志。

**Tech Stack:** .NET 10、MediatR、EF Core/PostgreSQL、现有 OAuth 绑定表、现有外部 JWT/AG-UI 访问点链路、Node E2E、Kiota 生成客户端。

---

## 文件边界

- Modify `src/app/MoAI.App.Shared/Commands/SystemIssueAppAccessTokenCommand.cs`：增加首次自动建户时可选的展示昵称，明确不接受 `userId`/可控用户名。
- Modify `src/app/MoAI.App.Shared/Queries/Responses/SystemIssueAppAccessTokenCommandResponse.cs`：增加 `userCreated` 回执。
- Modify `src/app/MoAI.App.Core/Handlers/SystemIssueAppAccessTokenCommandHandler.cs`：实现目标校验、身份查找、自动建户、并发收敛和 token 签发。
- Modify `src/account/MoAI.Account.Core/Handlers/SystemUpsertUserCommandHandler.cs`：将用户与 OAuth 绑定写入放入同一数据库事务，保留现有系统用户 upsert 的冲突语义。
- Modify `local-dev/internal-app-access-e2e.mjs`：把未绑定身份场景改为一次调用自动初始化，并验证幂等复用、回执和后续会话链路。
- Modify `docs/app/{bdd,sdd,sop,tdd}.md`、`docs/subsystem/{bdd,sdd,sop,tdd}.md`、`docs/gateway/external-api-scope-system.md`：同步新的单调用接入流程和验收映射。
- Modify `clients/MoAI.Subsystem.Client/Generated/`：只通过 `npm run syncapi`/客户端同步脚本更新生成模型，禁止手工改 Kiota 生成物。
- Modify `AGENTS.md` 和 `docs/rounds-log.md`：在现有 D57 描述上补充“未绑定时 app-tokens 自动初始化”，记录验证证据。

### Task 1: 固化接口契约和建户安全边界

**Files:**
- Modify: `src/app/MoAI.App.Shared/Commands/SystemIssueAppAccessTokenCommand.cs`
- Modify: `src/app/MoAI.App.Shared/Queries/Responses/SystemIssueAppAccessTokenCommandResponse.cs`

- [x] **Step 1: 更新 command 文档和字段**

将 command 的语义从“未绑定返回 404”改为“未绑定时自动初始化”。新增：

```csharp
/// <summary>
/// 首次自动建户时使用的昵称，可选；已存在用户不会被本接口修改昵称.
/// </summary>
public string? NickName { get; init; }
```

在 `Validate` 中增加 `NickName` 最大长度 50 校验。不要增加 `UserId` 或可由请求直接指定的 `UserName` 字段；MoAI 用户身份必须由 OAuth 绑定决定，避免接入方借 token 颁发接口把第三方身份挂到任意已有账号。

- [x] **Step 2: 增加创建结果回执**

在 response 增加：

```csharp
/// <summary>
/// 本次是否因第三方身份未绑定而自动创建了 MoAI 用户.
/// </summary>
public bool UserCreated { get; init; }
```

已绑定用户返回 `false`，首次自动初始化返回 `true`；两种情况都返回同一结构的访问 token 和真实 `userId`。

- [x] **Step 3: 编译共享层**

运行：

```powershell
dotnet build src/app/MoAI.App.Shared/MoAI.App.Shared.csproj --no-restore
```

预期：0 error。若工作区现有 WIP 造成无关错误，只记录错误文件，不回滚用户改动。

### Task 2: 让账户创建与 OAuth 绑定原子化

**Files:**
- Modify: `src/account/MoAI.Account.Core/Handlers/SystemUpsertUserCommandHandler.cs`

- [x] **Step 1: 为 Handle 建立数据库事务边界**

把现有 `Handle` 的“查找 → Update/Create → 返回”包在 `Database.BeginTransactionAsync` 中；成功得到结果后 `CommitAsync`，任意异常 `RollbackAsync` 后继续抛出。现有 `SaveChangesAsync` 可以继续保留在 `UpdateAsync`/`CreateAsync` 内，它们会参加当前事务；注册增强 hook 也参加同一事务。

伪代码必须收敛为以下控制流，不要在异常后提交半成品：

```csharp
await using var transaction = await _databaseContext.Database.BeginTransactionAsync(cancellationToken);
try
{
    var user = await ...;
    await EnsureOauthBindingAvailableAsync(user?.Id, request, cancellationToken);
    var result = user == null
        ? await CreateAsync(request, cancellationToken)
        : await UpdateAsync(user, request, cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return result;
}
catch
{
    await transaction.RollbackAsync(CancellationToken.None);
    throw;
}
```

无 OAuth 字段的既有系统用户 upsert 也必须继续保持原有结果；事务只是加强一致性，不改变返回契约。

- [x] **Step 2: 保留冲突前置校验和软删除约束**

继续在写用户前调用 `EnsureOauthBindingAvailableAsync`，确保“同渠道 sub 已绑定他人”和“同用户同渠道换 sub”都在用户写入前返回 409。禁止手工写审计字段或绕过 EF 软删除过滤器。

- [x] **Step 3: 运行账户相关测试**

运行：

```powershell
dotnet test tests/MoAI.App.Tests/MoAI.App.Tests.csproj --no-restore
```

预期：现有测试全部通过；若该项目未覆盖账户 handler，则在 E2E 任务中验证事务行为。

### Task 3: 在 app-tokens 中实现一次调用自动初始化

**Files:**
- Modify: `src/app/MoAI.App.Core/Handlers/SystemIssueAppAccessTokenCommandHandler.cs`

- [x] **Step 1: 注入账户 command 调度能力**

给 handler 注入 `IMediator`，复用 `SystemUpsertUserCommandHandler` 的既有建户、占位邮箱/手机号、随机密码、注册 hook 和 OAuth 绑定规则，不在 App 模块复制密码哈希或用户初始化逻辑。

- [x] **Step 2: 把目标应用校验提前到可能建户之前**

先查询目标应用和访问点，并调用 `ExternalAppAccessValidator.EnsureInternalUsable`。因此外部应用、未发布应用、停用应用或未开启访问点时，不会因为一次错误请求留下新 MoAI 用户。

- [x] **Step 3: 按 OAuth 身份查找并处理异常匹配**

保留现有匹配语义：同渠道下 `sub` 或提供的 `unionId` 任一命中都算命中；0 个命中进入自动初始化；1 个命中读取用户并检查禁用；多个用户返回 409。未知渠道仍返回 404。

- [x] **Step 4: 为首次建户生成不可控的稳定用户名**

使用 `providerId + "\\0" + oauthSub` 的 SHA-256，转 Base64Url 后截取固定长度，生成类似 `oauth_<hash>` 的用户名，保证符合系统用户名字符规则且不把第三方原始标识写入用户名。请求中的 `NickName` 只传给新建命令，不能修改已有用户。

如果极低概率出现同名但未绑定的既有账号，追加密码学随机短后缀后再创建，不能把 OAuth 身份直接绑定到该既有账号。生成名需保持在数据库 `user.user_name` 的 50 字符限制内。

- [x] **Step 5: 调用系统建户命令并收敛并发竞争**

未命中时发送：

```csharp
new SystemUpsertUserCommand
{
    UserName = generatedUserName,
    NickName = request.NickName,
    OauthProviderId = request.OauthProviderId,
    OauthSub = request.OauthSub,
    OauthUnionId = request.OauthUnionId,
}
```

若两个请求同时初始化同一身份，数据库唯一索引可能使其中一个请求以 `DbUpdateException` 结束；捕获该异常后清空当前 `ChangeTracker`，重新查询 `(providerId, sub/unionId)`：已出现唯一绑定时视为 `userCreated=false` 并继续签发；仍未出现则重新抛出，避免吞掉邮箱、用户名等其它真实约束错误。最终只允许一个用户与该 OAuth 身份关联。

- [x] **Step 6: 返回创建标志并签发现有 token**

自动初始化成功或并发收敛后重新查询唯一用户，检查 `IsDisable`，调用现有 `GenerateAccessUserTokens(user, app)`。响应补充 `UserCreated`，不返回自动生成密码；用户之后通过 OAuth 登录进入 MoAI，访问点会话使用真实 `userId` 落 `UserType.Normal`。

- [x] **Step 7: 编译后端模块**

运行：

```powershell
dotnet build src/MoAI/MoAI.csproj --no-restore
```

预期：0 error。

### Task 4: 更新 E2E 验收并覆盖最终业务闭环

**Files:**
- Modify: `local-dev/internal-app-access-e2e.mjs`

- [x] **Step 1: 替换未绑定 404 断言**

将原 IA-S7a 改为：对全新 `oauthSub` 只调用一次 `app-tokens`，断言 200、`userCreated=true`、返回 `userId/userName/accessToken/refreshToken`，并用系统用户查询接口确认用户已经存在。

- [x] **Step 2: 验证自动初始化幂等**

使用同一 `(providerId, sub, unionId)` 再调用一次，断言 200、`userCreated=false` 且 `userId` 不变；不再要求第三方先调用 `/api/subsys/system/users`。

- [x] **Step 3: 验证失败不建户**

对未发布应用、关闭访问点、外部应用分别使用全新 OAuth sub，断言原有 403/400，并通过用户列表或后续身份查询确认没有因失败请求创建用户。

- [x] **Step 4: 保留既有绑定、冲突、禁用、刷新、会话合并断言**

现有 IA-S4/IA-S5 用户同步链路继续保留，用来证明旧接入方兼容；现有 IA-S10~IA-S17 继续证明自动创建或既有用户都能进入 AG-UI、合并站内会话、单应用隔离、刷新和禁用吊销。

- [x] **Step 5: 执行 E2E**

后端运行后执行：

```powershell
node local-dev/internal-app-access-e2e.mjs http://127.0.0.1:5000
```

预期：所有断言 PASS，且不再出现“未绑定 404、提示先建用户”的断言。

### Task 5: 同步生成客户端和四件套文档

**Files:**
- Modify: `clients/MoAI.Subsystem.Client/Generated/`
- Modify: `docs/app/bdd.md`, `docs/app/sdd.md`, `docs/app/sop.md`, `docs/app/tdd.md`
- Modify: `docs/subsystem/bdd.md`, `docs/subsystem/sdd.md`, `docs/subsystem/sop.md`, `docs/subsystem/tdd.md`
- Modify: `docs/gateway/external-api-scope-system.md`
- Modify: `AGENTS.md`, `docs/rounds-log.md`

- [x] **Step 1: 重新生成 Kiota 客户端**

后端运行并提供最新 OpenAPI 后执行仓库既定同步命令：

```powershell
cd ui
npm run syncapi
cd ..
node clients/sync-api.mjs
```

检查生成的 `SystemIssueAppAccessTokenCommand` 含 `nickName`，response 含 `userCreated`。不要直接编辑 `clients/.../Generated`。

- [x] **Step 2: 更新 BDD 场景**

在现有 IA-S7/SAA-S64 语义中改写：未绑定身份不再返回 404；增加新的永久场景编号（当前最高 IA 编号后续编号）覆盖“第一次 app-tokens 自动初始化 + 第二次幂等复用”。保留未知渠道 404、多用户 409、禁用 403、目标应用门禁和旧 `/system/users` 兼容场景。

- [x] **Step 3: 更新 SDD/SOP/API 总账**

把接入步骤统一成：创建 OAuth 连接器 → 创建系统级应用接入 → 第三方系统拿到自身用户 sub/unionId → 一次调用 `app-tokens`（可带昵称）→ 未命中自动创建并绑定 → 返回访问用户 token → 访问点会话落真实 MoAI 用户。SOP 删除“先调用 `/system/users` 才能颁发”的强制步骤，并补充自动账号无密码回传、OAuth 登录复用、失败不建户和并发幂等说明。`docs/gateway/external-api-scope-system.md` 同步 token 颁发语义。

- [x] **Step 4: 更新 TDD 和轮次台账**

将 `internal-app-access-e2e.mjs` 的结果映射到新增场景，记录后端 build、E2E、客户端生成和必要回归结果；在 `AGENTS.md` 的现状摘要中把“未绑定 404、先建用户”改为“app-tokens 幂等自动初始化”。

- [x] **Step 5: 执行最终验证**

```powershell
dotnet build src/MoAI/MoAI.csproj
dotnet test tests/MoAI.Subsystem.Client.Tests
node local-dev/internal-app-access-e2e.mjs http://127.0.0.1:5000
```

预期：后端 0 error、客户端测试全绿、内部应用访问点 E2E 全绿；不执行 `git reset` 或覆盖工作区现有 D57 改动。

## 设计边界

1. 第三方系统永远提交 OAuth `providerId + sub/unionId`，不提交 MoAI `userId`；MoAI `userId` 只作为响应和 token 主体返回。
2. `userCreated=true` 仅表示本次请求新建了 MoAI 用户；绑定复用、token 刷新和 OAuth 登录不重复建户。
3. 自动创建的账号随机密码不回传；可见连接器支持后续 OAuth 登录，隐藏连接器仍可用于系统身份映射，但不会出现在登录页和个人中心绑定列表。
4. 目标应用校验在自动建户前执行，避免错误的应用请求产生孤儿用户；用户/绑定落库由事务和唯一索引保护。
5. 现有显式 `/api/subsys/system/users` 继续保留，用于第三方主动同步用户名、邮箱、手机号、禁用状态等资料；它不再是首次访问用户 token 的必经步骤。
