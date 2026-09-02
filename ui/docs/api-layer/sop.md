# 前端 Kiota API 层（api-layer）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（验证映射与回归命令） ｜ [SOP](./sop.md)

## 1. 后端接口变更后的标准动作

1. 后端编译通过并运行（本地 5210）。
2. `cd ui && PATH="$HOME/.dotnet/tools:$PATH" npm run syncapi http://127.0.0.1:5210/openapi/v1.json`（**必须显式传地址**，默认 5000 为遗留值，[@FE-API-S10](./bdd.md#fe-api-s10)）。
3. `npm run typecheck` 必须绿；若 models/index.ts 报 TS2554 → kiota CLI 版本不对（[@FE-API-S9](./bdd.md#fe-api-s9)，见排障表）。
4. 手写封装 `api/<domain>.ts` 增改方法；页面调用点跟进。

## 2. 排障

| 现象 | 原因 | 处理 | 场景 |
|---|---|---|---|
| typecheck TS2554（models/index.ts） | kiota CLI ≠ 1.27.0 | `dotnet tool uninstall -g Microsoft.OpenApi.Kiota && HTTPS_PROXY=http://127.0.0.1:7897 dotnet tool install -g Microsoft.OpenApi.Kiota --version 1.27.0` 后重新 syncapi | [@FE-API-S9](./bdd.md#fe-api-s9) |
| syncapi 报 kiota: command not found | PATH 缺 ~/.dotnet/tools | 前缀 `PATH="$HOME/.dotnet/tools:$PATH"` | — |
| syncapi 卡住/生成失败 | openapi 地址不通（后端没起/端口错/默认 5000） | 先 `curl <url>` 确认 200；直接传完整 URL | [@FE-API-S10](./bdd.md#fe-api-s10) |
| 页面所有请求 401 循环跳登录 | token 失效且 refresh 失败 | 属预期（[@FE-API-S1](./bdd.md#fe-api-s1)）；排查后端用户态 |
| 4xx 无提示/提示不友好 | 调用方吞掉了归一化错误 | 归一化错误必须直接抛出（响应体已被消费） | [@FE-API-S3](./bdd.md#fe-api-s3) |

## 3. 封装一个新接口（手写层最小步骤）

1. syncapi 后在 `src/api/client/api/<domain>/` 确认请求构建器路径（如 `usermanage.user.byId(id).isadmin.put`）。
2. `ui/src/api/<domain>.ts` 新增函数：取客户端 → 调用 → **返回值做 `?? ''`/`?? []`/`?? 0` 兜底与类型收窄**（Kiota 字段多为 `| null`），复杂响应定义局部 interface（参考 `UserListItem`）。
3. 涉及密码字段：函数内 `rsaEncrypt((await getServerInfo()).rsaPublic, password)`。
4. 页面只 import `@/api/<domain>`；错误提示交给全局中间件（页面仅 console.error + 成功分支 feedback.success）。

## 4. 验收流程（api 层改动后）

1. `cd ui && npm run typecheck && npm run lint` 全绿（生成物被 eslint 忽略，手写层零告警）。
2. `npm run test` 全量（基线 42/42）；新增封装影响页面时补/改 `__tests__` 并 mock `@/api/<domain>`（参考 Users.test.tsx，同时 mock `@/api/auth` 的 getServerInfo）。
3. 后端运行时手工冒烟：dev server 登录后触发新接口，观察 Network 出参与错误提示路由（4xx→message、500/断网→notification，[@FE-API-S3](./bdd.md#fe-api-s3)/[@FE-API-S4](./bdd.md#fe-api-s4)）。
4. 401 行为回归：篡改 accessToken 触发请求 → 整页跳 /login（[@FE-API-S1](./bdd.md#fe-api-s1)）；登录接口 401 → 仅提示不清态（[@FE-API-S2](./bdd.md#fe-api-s2)）。

## 5. 约定提醒

- `src/api/client/` 是生成物，任何手改都会被下次 syncapi 抹掉。
- 新封装一律从 `@/api/kiota` 取 client；禁止页面直接 import client 生成物。
- 密码类字段只在封装层 rsaEncrypt，页面只传明文（[@FE-API-S11](./bdd.md#fe-api-s11)）。

## 6. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01（轮 12，as-built）**：`npm run typecheck`（tsc -b --noEmit）退出码 0；`npm run lint` 0 告警；全量 Vitest 13 文件 42 用例全过（Feedback 14 + bridge 1 + Users 3 定向 18/18）；kiota-lock `kiotaVersion=1.27.0`、`descriptionLocation=http://127.0.0.1:5210/openapi/v1.json`、usermanage 目录（users/ 与 isadmin/isdisable/password）齐全；package-lock 六个 `@microsoft/kiota-*` 均精确锁 `1.0.0-preview.93`；kiota.ts 中间件三分支走查与 as-built 一致。syncapi 未重跑（保护生成物），以快照证据替代（见 [TDD](./tdd.md)）。同日复核补强：5000 遗留值（syncapi 默认/vite /openapi 代理未运行时使用/.env）与 includes('login') 粗匹配如实记录。

## 7. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 12，as-built）；同日补强：5000 遗留值记录、封装步骤与验收流程、models 体量修正 |
| 2026-09-02 | 按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（@FE-API-S1~S12）、四件互链、职责瘦身 |
