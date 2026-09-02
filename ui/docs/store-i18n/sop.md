# 前端状态管理与 i18n（Store & i18n）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 新增持久化 store 字段

1. `ui/src/store/app.ts`：`AppState` 补字段声明与（如需）动作 + create 回调里补初始值；
2. 持久化二选一：进 persist 快照 → 加入 `partialize` 白名单（**漏加则刷新即丢，最常见坑**）；需初始化探测的小状态 → 仿 `setThemeKey` 在 action 内写独立键并在 `getInitialXxx()` 读取；
3. 不要绕过 action 直写 `setState`（独立键不跟随，[@FE-SI-S2](./bdd.md#fe-si-s2)/[@FE-SI-S6](./bdd.md#fe-si-s6)）；
4. 消费：组件内选择器订阅 / 组件外快照读写（[@FE-SI-S17](./bdd.md#fe-si-s17)/[@FE-SI-S18](./bdd.md#fe-si-s18)）；
5. 回归：`npm run typecheck && npm run lint && npm run test`。

旧快照兼容：新增字段由 create 初始值合并兜底，无需迁移；删除/改名会在旧快照残留死数据，必要时清 localStorage。

## 2. 新增/维护 i18n 词条

1. **同时**编辑 zh-CN 与 en-US 的 `common.json`（两包键数须一致）；
2. 键名 `<模块前缀>.<名称>`，新页面用新顶层前缀，嵌套 ≤ 2 层（参照 `settings.oauthAutoRegister.name`）；
3. 变量文案用 `{{name}}` 插值；组件内 `useTranslation()`，**禁止硬编码文案**；
4. 回归：typecheck（resolveJsonModule 已开；键拼错属运行时问题）+ 切 en-US 检查布局不溢出。

## 3. 新增一种语言（5 处，缺一不可）

1. `locales/<lang>/common.json` 以 zh-CN 为底全量翻译（键结构一致）；
2. `i18n/index.ts` resources 注册；
3. `store/app.ts` `Locale` 联合类型 + `getInitialLocale` 合法值放行；
4. `design-system/theme/locale.ts` antd locale 映射；
5. `AppSider.tsx` `localeOptions` 增加选项。

## 4. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 新加字段刷新后丢失 | 未加入 partialize 白名单 | 补进 partialize |
| 主题/语言偶发不跟随 | 绕过 action 用 setState 直写 | 改走 `setThemeKey`/`setLocale` |
| 英文界面出现中文 | en-US 缺键走回退（[@FE-SI-S7](./bdd.md#fe-si-s7)） | 补齐 en-US 词条 |
| antd 组件文案没切换 | `getAntdLocale` 缺映射 | 见第 3 节第 4 步 |
| 图标/头像 404 | serverInfo 缓存的 serviceUrl 过期（[@FE-SI-S12](./bdd.md#fe-si-s12)） | `refreshServerInfo()` 或清 store 快照 |
| 测试里 store 是旧值 | persist 水合与用例间状态残留 | 用例内显式 `useAppStore.setState({...})` 重置 |

## 5. 验收流程（发布前）

1. `cd ui && npm run lint && npm run test && npm run typecheck` 全绿；
2. 走查 [@FE-SI-S1](./bdd.md#fe-si-s1)~[@FE-SI-S3](./bdd.md#fe-si-s3)（清 localStorage 首访暗色探测、切换、刷新保持）与 [@FE-SI-S5](./bdd.md#fe-si-s5)~[@FE-SI-S8](./bdd.md#fe-si-s8)（默认中文、切英文、回退、刷新保持）；
3. 核对 localStorage 三键：`moai-web-theme`、`moai-web-locale`、`moai-web-store`（快照 JSON 只含 serverInfo/userInfo）。

## 6. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 轮次 17 交付（as-built 回溯整理）**：`npm run test` 13 文件 42 用例全绿（含 `design-system/theme/__tests__/{config,tokens}.test.ts` 6 用例与 `pages/users/__tests__/Users.test.tsx` 3 用例对 store 注入约定的覆盖）；`npm run typecheck`（tsc -b --noEmit）无输出退出码 0；手工走查主题/语言切换、刷新保持、antd 文案跟随均通过。

## 7. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 功能交付（轮次 17，as-built） |
| 2026-09-02 | 按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-SI）、四件互链、职责瘦身 |
