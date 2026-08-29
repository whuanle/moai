# 前端开发规范

本文描述 `ui/` 前端项目的架构与开发约定。前端项目根目录为 `F:\workspace\moai\ui`（与旧版 `ui/moai` 无关，旧版仅在迁移时作参考，不作为新规范依据）。

## 技术栈

| 分类 | 选型 |
|------|------|
| 构建 | Vite 6 |
| 框架 | React 19 + TypeScript |
| UI | ant-design (antd 5) + @ant-design/icons |
| 路由 | react-router 7 |
| 状态 | zustand 5 (persist 中间件) |
| 国际化 | i18next + react-i18next + i18next-browser-languagedetector |
| API 客户端 | Kiota (TypeScript) |

> Kiota 依赖统一锁定在 `1.0.0-preview.93`，与生成的代码保持一致。**不要用 caret（`^`）升级**，否则会出现 `getCollectionOfPrimitiveValues` 等签名不匹配导致的类型错误。

## 目录结构

```
ui/
├── package.json
├── vite.config.ts
├── tsconfig.json / tsconfig.app.json / tsconfig.node.json
├── eslint.config.js
├── scripts/
│   └── sync-api.mjs          # 清理并重新生成 Kiota 客户端
├── .env.example / .env.development
└── src/
    ├── main.tsx              # 入口，组合 AppProviders
    ├── App.tsx               # 挂载 RouterProvider
    ├── vite-env.d.ts
    ├── config/
    │   └── env.ts            # 环境配置（ServerUrl）
    ├── api/
    │   ├── kiota.ts          # 客户端工厂（鉴权、baseUrl、401 中间件）
    │   ├── auth.ts           # 登录 / 注册 / 续期 / token 检查
    │   └── client/           # Kiota 生成代码（勿手改）
    ├── i18n/
    │   ├── index.ts          # i18next 初始化
    │   └── locales/{zh-CN,en-US}/common.json
    ├── theme/
    │   ├── config.ts         # light / dark 的 antd ThemeConfig
    │   └── locale.ts         # app locale -> antd locale
    ├── store/
    │   └── app.ts            # zustand store（theme/locale/serverInfo/userInfo）
    ├── auth/
    │   └── RequireAuth.tsx   # 路由守卫 + 定时 token 续期
    ├── providers/
    │   └── AppProviders.tsx  # ConfigProvider + antd App 编排
    ├── layouts/
    │   ├── AppLayout.tsx     # 登录后布局（Header + Outlet）
    │   └── components/AppHeader.tsx
    ├── pages/
    │   ├── Dashboard.tsx
    │   └── auth/{Login,Register}.tsx
    └── router/
        └── index.tsx
```

- `src/api/client/` 为 Kiota 自动生成，**禁止手工修改**。
- 路径别名 `@` -> `src/*`。
- `eslint.config.js` 忽略 `src/api/client`、`moai/**`、`node_modules`。

## API 客户端（Kiota）规则

### 同步命令

```jsonc
// package.json
"syncapi": "node scripts/sync-api.mjs"
```

`scripts/sync-api.mjs` 会**先删除 `src/api/client/`（含旧文件与锁文件）**，再调用 kiota 重新生成，避免 OpenAPI 变更后残留过期文件。默认文档源：

```
http://127.0.0.1:5000/openapi/v1.json
```

可用 CLI 参数覆盖文档源（便于离线使用缓存 `src/MoAI/MoAI.json`）：

```bash
npm run syncapi -- "F:\workspace\moai\src\MoAI\MoAI.json"
```

### 统一客户端工厂

`src/api/kiota.ts` 提供两个工厂：

- `getApiClient()`：带鉴权（从 store 读取 `accessToken`，Bearer + `AllowedHostsValidator`），适用于登录后的业务请求。
- `getAnonymousClient()`：匿名，适用于登录/注册/刷新 token/获取 serverinfo 等公开接口。

`baseUrl` 取自 `Env.serverUrl`。请求中间件过滤 401：清空登录态并跳转 `/login`。

> Kiota 生成代码的 `createMoAIClient` 会读取 adapter 的解析/序列化注册表；因此匿名客户端沿用 `new FetchRequestAdapter(new AnonymousAuthenticationProvider())` 默认注册表即可。

## 环境与相对路径

`src/config/env.ts`：

```ts
function getServerUrl(): string {
  const envUrl = import.meta.env.VITE_ServerUrl
  if (envUrl) return String(envUrl)
  return window.location.origin
}
```

- 未配置 `VITE_ServerUrl` 时使用页面地址（同源），从而支持相对路径请求。
- 环境变量在 `.env.development` / `.env.example` 中声明。

## 状态管理规则

`src/store/app.ts` 使用 zustand + persist：

- `themeMode: 'light' | 'dark'`
- `locale: 'zh-CN' | 'en-US'`
- `serverInfo`：`{ serviceUrl, publicStoreUrl, rsaPublic }`（持久化）
- `userInfo`：`{ accessToken, expiresIn, refreshToken, tokenType, userId, userName }`（持久化）
- 提供 `setThemeMode/toggleTheme/setLocale/setServerInfo/clearServerInfo/setUserInfo/clearUserInfo` 动作。

> theme 与 locale 的选择器通过 `localStorage` 的 `moai-web-theme` / `moai-web-locale` 单独持久化；token 与 serverinfo 通过 persist 存储。

## 路由规则

`src/router/index.tsx`：

- 公开页：`/login`、`/register`（不含 Header 的独立页面）。
- 受保护：根 `/` 由 `RequireAuth` 包裹 `AppLayout` 渲染。
  - 入口 `/` -> 重定向 `/dashboard`。
  - `/dashboard`：登录后首页。
  - `*` 子路由兜底重定向到 `/dashboard`，为后续 `/xxx` 专用页面预留。

新增受保护页面：在 `AppLayout` 的 `children` 里追加；新增公开页面：在顶层追加。

## 认证与 token 续期规则

登录/注册密码需使用 RSA 公钥加密后传输：

1. `getServerInfo()` 拉取 `serverinfo.rsaPublic`。
2. `rsaEncrypt(publicKey, password)` 加密（`src/utils/rsa.ts`，基于 jsencrypt）。
3. POST `account.login` / `account.register`。

### token 检查与刷新

`src/api/auth.ts`：

- `checkToken()`：无 accessToken 返回 false；JWT 未过期返回 true；过期则用 `refreshToken` 换取新 token 并更新 store，失败返回 false。
- `refreshAccessToken(refreshToken)`：调用 `account.refresh_token`。
- JWT 过期判断在 `src/utils/jwt.ts`，含 60 秒提前量，避免刷新不及时。

### 周期性续期

`src/auth/RequireAuth.tsx`：

- 页面打开时执行一次 `checkToken()`：过期即刷新，刷新失败则清空登录态并跳转 `/login`。
- 每 60 秒执行一次 `checkToken()`，保持 token 新鲜；失败同样跳转 `/login`。
- 检查期间显示加载态。

## 主题与上下文提供器

`src/providers/AppProviders.tsx` 统一编排：

- `ConfigProvider`：`locale={getAntdLocale(locale)}`、`theme={getThemeConfig(themeMode)}`。
- antd `App`：包裹 children，提供 `message/notification/modal` 的上下文用法（页面内使用 `App.useApp()`，而非静态 `message`）。

`src/theme/config.ts` 提供 `getThemeConfig(mode)`（light 用 `defaultAlgorithm`、dark 用 `darkAlgorithm`，主色 `#4A9EFF`）；`src/theme/locale.ts` 将 app locale 映射到 antd locale。

> 主题切换的入口统一放在 `AppHeader`（`Switch`），语言切换在 `Select`；两者变更都写回 store。

## 国际化规则

`src/i18n/index.ts` 使用 i18next：

- 资源：`locales/zh-CN/common.json`、`locales/en-US/common.json`，统一 namespace `translation`。
- 语言自动检测（`localStorage` 键 `moai-web-locale`，其次 `navigator`），fallback `zh-CN`。
- 语言变化时由 `AppProviders` 调用 `i18n.changeLanguage(locale)` 并同步 `<html lang>` 与 antd locale。

> 文案一律走 `useTranslation()` 的 `t()`，禁止硬编码。新增语言在 `locales/` 增加对应目录并注册到 `i18n/index.ts`，同时补充 `theme/locale.ts` 的 antd 映射。
