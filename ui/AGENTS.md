# MoAI 前端开发指南（ui/AGENTS.md）

> 前端入口文档。只讲硬约束与目录约定，细节真源：[frontend-conventions.md](./docs/frontend-conventions.md)、[design-system/](./docs/design-system/README.md)。
> 后端与全栈约定见根目录 [AGENTS.md](../AGENTS.md)。

## 技术栈

Vite 6 · React 19 · TypeScript · antd 5 · react-router 7 · zustand 5(persist) · i18next · Kiota 生成客户端

## 目录结构

```
src/
├── api/
│   ├── client/        Kiota 生成代码，禁止手改
│   ├── kiota.ts       工厂：getApiClient() 带鉴权 / getAnonymousClient() 匿名
│   └── *.ts           手写业务封装（wiki.ts、team.ts…），页面只调这一层
├── design-system/
│   ├── components/    Page PageToolbar QueryBar DataTable FormPage DetailPage Card Chat Feedback
│   ├── templates/     List / Form / Detail / Dashboard / Chat
│   └── theme/         tokens、light/dark 配置、antd locale 映射
├── pages/             dashboard account users team wiki settings oauthconnect models plugin classify
├── layouts/ providers/ router/ auth/ store/ i18n/ utils/ test/
└── docs/              前端规范文档
```

统一从 `@/design-system` 导入；路径别名 `@` → `src/*`。

## 硬约束（违反即返工）

- **禁止直接 import antd 的 `Table`/`Form`** 等被封装组件，一律用 `@/design-system` 对应组件
- **颜色/间距必须取 token**，禁止魔法值与 `#hex` 硬编码
- **危险操作（删除/批量删除）必须 `Popconfirm`**，禁止裸执行
- **文案全走 `useTranslation()`**，zh-CN 与 en-US **同步改**，禁止硬编码
- **`src/api/client/` 禁手改**；改后端后 `npm run syncapi` 重新生成（脚本先删后建）
- **Kiota 锁 `1.0.0-preview.93`**，勿用 `^` 升级
- **提示用 `App.useApp()`** 的 message/notification，不用静态 `message`
- **新页面必须配测试**，写在同目录 `__tests__/`

## 页面编写四条

1. **不重复渲染大标题**：顶部导航已标识页面，`<Page>` 默认不传 `title`/`subtitle`
2. **内容撑满宽度**：`Page` 不加 `maxWidth`、不 `margin: 0 auto` 居中
3. **操作栏左对齐**：`DataTable` 头部操作区 flex 左对齐 + `gap`，不要 `space-between`
4. **Modal 一律 `maskClosable={false}`**，防误点遮罩丢失输入

时间显示统一 `formatDateTime()`（`YYYY-MM-DD HH:mm`），禁用 `toLocaleString()`。

## API 与后端对接

```bash
cd src/MoAI && dotnet run          # 后端默认 :5000（MoAI:Port）
npm run syncapi                    # 默认拉 http://127.0.0.1:5000/openapi/v1.json
npm run syncapi http://127.0.0.1:5210/openapi/v1.json   # 端口被 MAI_FILE 覆盖时
npm run syncapi "F:\workspace\moai\src\MoAI\MoAI.json"   # 后端未起，离线生成
```

- 接口文档由后端 NSwag 自动生成，**勿手写接口文档**
- `Env.serverUrl` 取 `VITE_ServerUrl`，未配置则同源，支持相对路径请求
- 登录/注册密码：`getServerInfo().rsaPublic` → `rsaEncrypt` → 提交，禁止明文
- 前后对接务必使用 kiota，禁止自行拼接 http 请求

## 状态 / 路由 / 国际化

- **store** `src/store/app.ts`（zustand persist）：`themeKey`、`locale`、`serverInfo`、`userInfo`
- **路由** `src/router/index.tsx`：公开页 `/login` `/register` `/oauth_login`；其余挂 `AppLayout` 的 children，由 `RequireAuth` 守卫，`*` 兜底重定向 `/dashboard`。新增受保护页在 children 追加
- **token 续期** `auth/RequireAuth.tsx`：进入时 + 每 60s `checkToken()`，失败清空登录态跳 `/login`
- **i18n**：资源在 `i18n/locales/{zh-CN,en-US}/common.json`，namespace `translation`

## 命令

```bash
npm run dev          # 4000
npm run typecheck && npm run lint && npm run test    # 提交前全绿
npm run syncapi      # 同步后端接口
```

## 文档索引

- [frontend-conventions.md](./docs/frontend-conventions.md) — 架构、目录、API 客户端、主题、页面规范
- [design-system/README.md](./docs/design-system/README.md) → `tokens.md` `components.md` `pages.md` `theming.md` `feedback.md`
- [docs/api-layer/](./docs/api-layer/) · [auth-flow/](./docs/auth-flow/) · [theme/](./docs/theme/) · [components-base/](./docs/components-base/) · [components-form/](./docs/components-form/) · [layout-routing/](./docs/layout-routing/) · [store-i18n/](./docs/store-i18n/) · [dashboard-testing/](./docs/dashboard-testing/)
