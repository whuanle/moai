# 前端状态管理与 i18n（Store & i18n）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../frontend-conventions.md](../frontend-conventions.md)、[../theme/sdd.md](../theme/sdd.md)（ThemeKey 与 antd locale 映射来源）、[../api-layer/sdd.md](../api-layer/sdd.md)（serverInfo/userInfo 消费方） ｜ 证据：vitest（`cd ui && npm run test`）+ `npm run typecheck`
> 规范：[../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-SI-Sxx），本文不重复。

## 目标

前端基座：**跨页面共享、可持久化**的客户端状态层（主题/语言/服务器信息/登录用户态）+ **多语言文案机制**（zh-CN / en-US，界面文案零硬编码）。两者经 `AppProviders` 联动：store 的 `locale` 驱动 i18next、`<html lang>` 与 antd locale。

## 组件

```
ui/src/
├── store/app.ts                              # 唯一全局 store useAppStore（zustand 5 + persist）
├── i18n/index.ts                             # i18next 装配（单 namespace translation）
├── i18n/locales/{zh-CN,en-US}/common.json    # 双语资源（各 12 组顶层键，键数一致）
├── providers/AppProviders.tsx                # locale → i18n / antd / <html lang> 联动点
├── main.tsx                                  # import '@/i18n' 副作用装配
└── layouts/components/AppSider.tsx           # 主题/语言切换 Select 入口
```

## 数据（AppState）

| 字段 | 类型 | 初始值 | 持久化 |
|---|---|---|---|
| `themeKey` | `'light' \| 'dark'` | `getInitialTheme()` | 独立键 `moai-web-theme` |
| `locale` | `'zh-CN' \| 'en-US'` | `getInitialLocale()` | 独立键 `moai-web-locale` |
| `serverInfo` | `{ serviceUrl, publicStoreUrl, rsaPublic } \| null` | `null` | persist `moai-web-store` |
| `userInfo` | 令牌 4（accessToken/refreshToken/expiresIn/tokenType）+ 档案 6（userId/userName/email/nickName/phone/avatar）+ 用户态 4（isDisable/isAdmin/isRoot/isDeleted），全可空 | `null` | persist `moai-web-store` |

动作 7 个：`setThemeKey` / `toggleTheme` / `setLocale` / `setServerInfo` / `clearServerInfo` / `setUserInfo` / `clearUserInfo`。

## 关键决策

1. **三路持久化**：`themeKey`/`locale` 走独立 localStorage 键（action 内手动写、初始化函数读取）；`serverInfo`/`userInfo` 走 zustand persist（`partialize` 白名单只序列化这两项）。独立键刻意不进快照，避免 persist 水合覆盖初始化探测（系统深色偏好）。
2. 初始化兜底：`getInitialTheme()` = 独立键合法值 → `matchMedia('(prefers-color-scheme: dark)')` → `light`；`getInitialLocale()` = 独立键合法值 → `zh-CN`（不读 navigator）。
3. **访问约定**：组件外（kiota/auth/storage/测试注入）一律 `useAppStore.getState()/setState()` 快照式读写（[@FE-SI-S17](./bdd.md#fe-si-s17)）；组件内选择器订阅避免无关重渲染（[@FE-SI-S18](./bdd.md#fe-si-s18)）。
4. i18next 单 namespace `translation`：两语言包各一个 `common.json`，顶层键按业务前缀组织（app/common/nav/auth/account/home/dashboard/settings/oauthconnect/feedback/ds/users），插值 `{{name}}`；`fallbackLng: 'zh-CN'`。
5. 启动语言由 `AppProviders` 挂载/更新时的 `i18n.changeLanguage(store.locale)` 决定（init 显式 `lng: 'zh-CN'`）；LanguageDetector 的 order 探测实际不参与决策，仅其 `caches` 生效（`changeLanguage` 后写回 `moai-web-locale`，与 store 手动写同键）。
6. 切换链路（[@FE-SI-S6](./bdd.md#fe-si-s6)）：AppSider Select → `setLocale` → AppProviders `useEffect([locale])` → `i18n.changeLanguage` + `documentElement.lang` + `ConfigProvider locale`（`getAntdLocale` 映射）。

## 已知问题

- 新增语言需改 5 处（Locale 类型 / locales 目录 / i18n 注册 / theme locale 映射 / AppSider 选项），步骤见 [SOP 第 3 节](./sop.md)。
- 绕过 action 直写 `useAppStore.setState({ locale })` 不会同步独立键（i18next caches 会补救 locale 键，theme 键不会）。
- persist 快照中的 `serverInfo` 无失效时间：后端地址/公钥变更后需 `refreshServerInfo()` 或清 localStorage 才会更新（[@FE-SI-S12](./bdd.md#fe-si-s12) 排障见 [SOP](./sop.md)）。
- `UserInfo.expiresIn` 声明为 `string | null`（后端 Unix 毫秒时间戳），语义对齐由 `utils/jwt.ts` 解码承担（见 [../../../docs/auth/sdd.md](../../../docs/auth/sdd.md)）。
