# 前端 Kiota API 层（api-layer）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../../docs/infra/sdd.md](../../../docs/infra/sdd.md) ｜ 证据：typecheck/vitest 命令（见 [TDD](./tdd.md)）
> 规范：[../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md)、[../frontend-conventions.md](../frontend-conventions.md)（仅引用）。认证行为详见 [../auth-flow/sdd.md](../auth-flow/sdd.md)。行为场景见 BDD（@FE-API-Sxx），本文不重复。

## 分层

```
ui/src/api/
├── client/          # kiota generate 产物（禁手改；kiota-lock.json 记录描述文件 hash+版本）
├── kiota.ts         # FetchRequestAdapter 工厂 + FilterRequestHandler 中间件
├── auth.ts          # 认证域封装（login/register/oauth*/checkToken/getServerInfo...）
├── account.ts       # 账号自助封装（改资料/改密/头像/绑定）
├── settings.ts      # 设置域（含 SettingKeys 常量映射）
├── oauthconnect.ts  # 渠道 CRUD 封装
└── usermanage.ts    # 用户管理封装（服务端分页 + RSA 改密）
```

`ui/scripts/sync-api.mjs` 负责再生成；错误提示 UI 在 `ui/src/design-system/components/Feedback/error.ts`。

## 工厂与中间件（kiota.ts）

- `getApiClient()`：Bearer token 取自 zustand `userInfo.accessToken`，无 token 退化为匿名 Provider；**每次调用新建 client/adapter**（无全局单例），token 刷新后下一次取客户端即拿到新值（[@FE-API-S6](./bdd.md#fe-api-s6)）；`AllowedHostsValidator` 无参构造=放行所有 host。
- `getAnonymousClient()`：登录/注册/serverinfo/refresh_token 等匿名端点专用（[@FE-API-S7](./bdd.md#fe-api-s7)）；两工厂共用显式注册 `application/json` 的序列化注册表。
- `FilterRequestHandler` 经 `unshift` 排在默认中间件**最前**：
  - 401 且 URL 不含字符串 `login` → 清登录态 + 整页跳 `/login`（[@FE-API-S1](./bdd.md#fe-api-s1)）；
  - 其余 `!ok` → `parseApiErrorResponse` 归一化后 `feedback.handleError`（业务 4xx→message，500/网络→notification）并抛出（[@FE-API-S3](./bdd.md#fe-api-s3)）；
  - 请求异常（无响应）→ handleError 后重抛；**非网络异常额外清登录态**（防御性，[@FE-API-S5](./bdd.md#fe-api-s5)）。
- baseUrl=`Env.serverUrl`（`VITE_ServerUrl` 优先，否则 `window.location.origin` 同源）。生成物内置兜底 `http://127.0.0.1:5210` 实践中永不生效（kiota.ts 总是显式赋值）。

## rsaEncrypt 用法（ui/src/utils/rsa.ts）

- JSEncrypt PKCS1，密文 Base64，失败抛 `Encryption failed`；末尾 `btoa(atob(x))` 为 Base64 归一化。
- 公钥来源：`getServerInfo()`（store 持久缓存优先，无则 `GET /api/common/serverinfo` 并写回）。
- **唯一正确位置是 api/ 封装层**：auth.ts/account.ts/usermanage.ts 均在封装函数内加密，页面只传明文（[@FE-API-S11](./bdd.md#fe-api-s11)）。

## 错误归一化（Feedback/error.ts）

- `getHttpStatus`：兼容 Response / Kiota APIError（responseStatusCode/responseStatus/status 候选）；`isNetworkError`：无状态码 + TypeError/常见 fetch 文案。
- `parseApiErrorResponse(response)` → `NormalizedApiError {status, responseStatusCode, detail, errors, message}`：detail（BusinessException）与 `errors[]{name,errors[]}`（字段级校验）双通道；`resolveErrorMessage` 优先 detail、次取第一条字段错误。**响应体被消费后无法再被 Kiota 解析，调用方必须直接抛出**。

## 客户端再生成（syncapi）

```bash
cd ui && npm run syncapi [openapi-url]   # 默认 http://127.0.0.1:5000/openapi/v1.json（遗留值，见已知问题 1）
```

- `scripts/sync-api.mjs`：`rmSync src/api/client` → `kiota generate --additional-data false -l typescript -d <url> -c MoAIClient -n ApiSdk -o ./src/api/client`。
- 生成物：`moAIClient.ts` + `api/{account,auth,common,oauthconnect,settings,storage,usermanage}/` + `models/` + `.kiota.log`（int64→String 等 warning 属正常）。
- **版本硬约束**：kiota CLI 必须 **1.27.0**（package.json 六个 `@microsoft/kiota-*` 运行时依赖精确锁 `1.0.0-preview.93`，禁用 `^`；1.34.x 产物 typecheck 必挂，[@FE-API-S9](./bdd.md#fe-api-s9)）。改后端接口后：重启后端 → syncapi（显式传实际端口）→ typecheck。

## 手写封装层分工

| 文件 | 客户端 | 职责 |
|---|---|---|
| auth.ts | 匿名为主 + 鉴权（userinfo） | serverinfo 缓存/登录/注册/refresh_token/OAuth 登录注册/refreshUserProfile/checkToken |
| account.ts | 鉴权 | bound_accounts/update_userinfo/reset_password（新旧密码均 RSA）/avatar/unbind/oauth_bind |
| settings.ts | 鉴权 | settings get/put + SettingKeys 常量 |
| oauthconnect.ts | 鉴权 | 渠道 CRUD；update 回填 oAuthConnectionId（路径参数） |
| usermanage.ts | 鉴权 | 用户治理五接口；`UserListItem.id` 为 `string \| number`——Kiota 将 int64 映射为 string，封装层吸收 |

约定：页面禁止直接 import `@/api/client/**` 生成物，一律经封装层（[@FE-API-S11](./bdd.md#fe-api-s11)）。

## 已知问题（5000 遗留值，如实记录）

仓库多处仍指向 5000 端口，而本地后端实际运行在 **5210**（kiota-lock descriptionLocation、后端各轮验收记录均为 5210）：

1. `scripts/sync-api.mjs` 默认文档源 `http://127.0.0.1:5000/openapi/v1.json`——后端不在 5000 时必须显式传参（[@FE-API-S10](./bdd.md#fe-api-s10)）。
2. `vite.config.ts` dev 代理 `/openapi` → 127.0.0.1:5000：**运行时代码从不请求 /openapi 相对路径**（Kiota baseUrl 直接用绝对地址），该代理未被使用，仅剩误导性。
3. `ui/.env.example` 与 `.env.development` 均为 5000——后端在 5210 时需 `.env.local` 覆盖或删变量回退同源。
4. 后端 `configs/system.json`、`launchSettings.json`（http profile）确实配置 5000，两端不一致属环境问题非代码缺陷。

其余：

5. 401 判定用 `url.includes('login')` 粗匹配——路径含 "login" 的其它接口会被误豁免（[@FE-API-S12](./bdd.md#fe-api-s12)）；`/auth/refresh_token` 不在豁免之列（详见 [../auth-flow/sdd.md](../auth-flow/sdd.md)）。
6. 每次请求新建 client/adapter 有轻微开销，换来 token 即时生效（as-built 取舍）。
