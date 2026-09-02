---
name: moai-cqrs-backend
description: Backend CQRS three-layer code standards for MoAI (.NET 9, Maomi modules, MediatR, EF Core). Use when adding/modifying Commands, Queries, Handlers, Controllers, validators, or business rules in src/*. 仅限 MoAI 项目。Use only for the MoAI project.
---

# MoAI 后端 CQRS 三层规范（L2）

## PROJECT SCOPE

只服务 MoAI 后端 `src/`。**REQUIRED REFERENCE：改代码前必读 [docs/cqrs-conventions.md](../../../docs/cqrs-conventions.md)（唯一真源，含完整代码模板与命名规范）。** 本 skill 只写执行要点与实踩坑。

## WHEN

- 新增/修改 Command、Query、Handler、Controller、校验
- 涉及 `src/<module>/MoAI.<Module>.{Shared,Core,Api}` 任一层的改动

## WHAT

按三层依赖（`Api → Core → Shared`）产出合规范的 CQRS 代码，构建 0 错。

## HOW

### 0. 先找范本

`rg` 找同域代码读真实实现。最佳完整范本：**user-management**（`src/account/` 的 UpdateUserIsAdminCommand 全链路）。

### 1. 目录与命名（细则见真源 §目录结构/命名规范）

| 项 | 规范 |
|---|---|
| Command | `{动作}{实体}Command.cs`，命名空间 `MoAI.{Domain}.Commands` |
| Query | `Query{实体}{描述}Command.cs`，命名空间 `MoAI.{Domain}.Queries` |
| Handler | `{Command/Query名}Handler.cs`；Command 在 `Handlers/`，Query 在 `Queries/` |
| Response | `{Query名}Response.cs` / `{Query名}ResponseItem.cs`，在 `Queries/Responses/` |
| 复杂模块 | 可按子领域建子目录（参照 `MoAI.Plugin.Shared/Classify/...`） |

### 2. Shared 层要点

- Command/Query 都继承 `IModelValidator<T>`（FluentValidation 静态 `Validate`），提前拦截无效请求
- ⚠️ **路由参数时序坑**：`{id}` 由 Controller 回填，自动验证发生在回填之前——Validate 里只能校验请求体字段，校验路由回填字段 = 接口恒 400（oauthconnect PUT 实踩）
- 需要用户上下文的命令继承 `IUserIdContext`（`ContextUserId`/`ContextUserType`）；用不到就别继承
- 分页继承 `PagedParamter`（上限 1000）；写命令响应统一 `EmptyCommandResponse`
- 公开成员全部中文 XML 注释（StyleCop 强制）

### 3. Core 层要点

- Handler 构造注入 `DatabaseContext` + 领域服务；**禁止注入 IUserContextProvider/UserContext**（用户信息只能经 Command 的 IUserIdContext 传入）
- 实体审计属性（CreateUserId/CreateTime/UpdateUserId/UpdateTime/IsDeleted）框架自动注入，**不要手动赋值**；查询记得 `IsDeleted == 0`
- 目标保护依赖 DB 事实：root 判定 = `setting` 表 `key="root"` 的 value
- 业务异常：`throw new BusinessException("中文消息.") { StatusCode = 400/403/404/409 }`，禁止裸 500
- ⚠️ **写用户相关数据后必须 `RemoveUserStateAsync`** 失效 Redis 用户态（禁用/降权即时生效依赖此）

### 4. Api 层要点

- Controller 只做门禁 + 转发，无业务逻辑
- 角色门禁在 Controller（`EnsureAdminAsync`/`EnsureRootAsync` 私有方法，查 `GetUserStateAsync`，非 403 即抛）；目标保护在 Handler
- 路由参数显式回填 + `_userContextProvider.SetUserContext(cmd)` 后再 `_mediator.Send`
- 用户上下文只经 `IUserContextProvider.GetUserContext()`，不直接注入 UserContext

### 5. 模块注册

三层各有 `{Domain}SharedModule`/`{Domain}CoreModule`/`{Domain}ApiModule`（`IModule`，Core 用 `[InjectModule<...>]` 声明依赖，模板见真源 §模块注册）。

## REFERENCE

正例：`UpdateUserIsAdminCommand` 全链路（Shared 定义 → Handler 目标保护+缓存失效 → Controller EnsureRoot+回填）。

## LIMITS

- 不含前端规范（`L2-code-standards/moai-frontend-ui`）；审查清单（`L3-fix-standards/moai-cqrs-review`）
- 不做迁移/DDL、部署
