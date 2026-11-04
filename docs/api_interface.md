# 前后端对接

后端用 OpenAPI 暴露接口文档，前端用 `npm run syncapi` 生成 TypeScript 客户端。不用手写接口代码。

> 关联：[CQRS 规范](./cqrs-conventions.md) ｜ [前端规范](../ui/docs/frontend-conventions.md) ｜ [文档地图](./README.md)

## 流程

```
写完后端 CQRS 三层（Shared → Core → Api）
        ↓
启动后端（src/MoAI 下 dotnet run）
        ↓
前端 npm run syncapi
        ↓
ui/src/api/client/ 重新生成
        ↓
写页面，用 @/api/*.ts 封装调用
```

## 1. 后端：接口即文档

后端不需要单独维护接口文档。Controller 写完后，NSwag 自动扫描生成 OpenAPI 文档（`src/MoAI/Modules/OpenApiModule.cs`）。

- 文档地址：`http://127.0.0.1:5000/openapi/v1.json`（内部接口，前端 Kiota 默认源）
- 外部接口单独成文档：`http://127.0.0.1:5000/openapi/teamres.json`（`/api/teamres` 全部外部接口，前端 Kiota 不消费）
- 子系统协作接口单独成文档：`http://127.0.0.1:5000/openapi/subsys.json`（`/api/subsys` 协作面，全环境暴露，子系统 .NET 客户端库的生成源）
- **三份文档均自包含**（`OpenApiModule.PruneUnreferencedSchemas`）：路径只含本面接口，`components.schemas` 按本文档操作可达性裁剪——v1 510、external 107、subsys 39，互不掺杂；按面做 client 生成互不干扰
- 可视化页面（Scalar）：`http://127.0.0.1:5000/scalar`，页面左上角下拉切换 `AI API`（v1 内部）、`AI External API`（外部）与 `AI Subsystem API`（子系统协作）三份文档；外部文档也可直达 `/scalar/external`、子系统文档直达 `/scalar/subsys`
- **暴露策略按环境区分**（`Program.cs`）：v1 文档与 Scalar 仅 Development 暴露；`/openapi/teamres.json` 全环境暴露——部署（非 Development）后仅保留此文档供第三方接入方获取，其余 `/openapi/**`、`/scalar/**` 一律 404（不落入 SPA 回退）
- 文档的 `servers` 会写入 `MoAI:Server` 配置与实际监听地址，前端同步后 baseUrl 自动一致

启动后端：

```bash
cd src/MoAI
dotnet run
```

端口取自配置的 `MoAI:Port`，默认 **5000**（同时监听 `Port + 1`）。用 `MAI_FILE` 指向自定义配置文件可覆盖端口等配置，此时以该配置文件为准。

```bash
MAI_FILE=/path/to/system.local.json ASPNETCORE_ENVIRONMENT=Development dotnet run
```

## 2. 前端：同步生成客户端

```bash
cd ui
npm run syncapi
```

- 默认拉取 `http://127.0.0.1:5000/openapi/v1.json`，端口不是 5000 时显式传参：

  ```bash
  npm run syncapi http://127.0.0.1:5210/openapi/v1.json
  ```

- 后端没起时，可用仓库内缓存的 OpenAPI 文件离线生成：

  ```bash
  npm run syncapi "F:\workspace\moai\src\MoAI\MoAI.json"
  ```

- 脚本会**先删除 `src/api/client/` 再重新生成**，避免接口变更后残留过期文件。

## 3. 生成物的使用规则

| 路径 | 说明 |
|---|---|
| `ui/src/api/client/` | Kiota 生成代码，**禁止手工修改**，每次 syncapi 会被覆盖 |
| `ui/src/api/*.ts` | 手写封装（如 `auth.ts`、`wiki.ts`），基于生成代码做业务级封装，页面只调这一层 |
| `ui/src/api/kiota.ts` | 客户端工厂：`getApiClient()` 带鉴权，`getAnonymousClient()` 匿名 |

## 4. 后端改动后要做什么

1. 后端改完 Controller / Command / Response → 重启后端（`dotnet run`）。
2. 前端 `npm run syncapi` 重新生成客户端。
3. 若手写封装 `ui/src/api/*.ts` 依赖的类型变了，同步改封装。
4. 提交前跑一遍：`npm run typecheck && npm run lint && npm run test`。

## 5. 常见问题

| 现象 | 原因 | 处理 |
|---|---|---|
| `openapi/v1.json` 404 | 非 Development 环境（v1 与 Scalar 仅开发暴露，部署后只有 `/openapi/teamres.json`） | 加 `ASPNETCORE_ENVIRONMENT=Development` |
| syncapi 连接被拒 | 后端没起，或端口与 `MoAI:Port` 不一致 | 确认后端已启动，并显式传正确端口 |
| 生成后类型报错 | Kiota 版本不匹配 | 锁定 `1.0.0-preview.93`，不要用 `^` 升级 |

## 6. 子系统协作面：.NET 客户端库

子系统接入方（`/api/subsys/**`，文档 `/openapi/subsys.json` 全环境暴露）不手写 HTTP，用仓库内 Kiota 生成的 .NET 客户端库：

```bash
node clients/sync-api.mjs   # 后端运行中执行；只重写 clients/MoAI.Subsystem.Client/Generated/
```

- 生成物 `Generated/` 禁止手改（与前端 `ui/src/api/client/` 同规则）；手写包装（DI `AddMoaiSubsysClient` / 配置 `MoaiSubsysClientOptions` / token 管理 / 错误 `SubsysException`）在项目根，重生成永不覆盖。
- 接入方用法、token 生命周期、错误语义、SSO 用户委托 token 见 [clients/MoAI.Subsystem.Client/README.md](../clients/MoAI.Subsystem.Client/README.md)。
- 单测：`dotnet test tests/MoAI.Subsystem.Client.Tests`（内存桩，无需真实后端）。
| 接口改了但前端没变 | 旧 `client/` 未清理 | 脚本已自动删除重建；仍异常则手动删 `src/api/client/` 后重跑 |
