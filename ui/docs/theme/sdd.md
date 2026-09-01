# 前端主题系统（theme）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[frontend-conventions.md](../frontend-conventions.md)、[design-system 分册](../design-system/README.md) ｜ 证据：[theme 单测](../../src/design-system/theme/__tests__/)
> 规范：[DOC-STANDARD](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-TH-Sxx），本文不重复。

## 目标

设计令牌单一来源 + antd 主题预设（light/dark）+ antd locale 映射，经 `@/design-system` 统一导出；ConfigProvider 是主题与 antd 文案的全局唯一注入点。侧边栏切换入口的布局归属见 [../layout-routing/sdd.md](../layout-routing/sdd.md)。

## 结构（`ui/src/design-system/theme/`）

| 文件 | 职责（契约） |
|---|---|
| tokens.ts | 设计令牌唯一来源（值见下节）；**无 zIndex/shadow 导出** |
| config.ts | `ThemeKey='light'｜'dark'`（TS 收口）；`defaultThemeKey='light'`；`themePresets` 两套 antd ThemeConfig（light=defaultAlgorithm / dark=darkAlgorithm；token 级 + components 级 Layout/Menu/Button/Input/Select/Card/Table/Modal/Tag/Statistic 覆盖，暗色如 Menu itemSelectedColor #7BA2FF、Table headerBg #222D44）；`getThemeConfig(key)` 唯一取用入口 |
| locale.ts | `getAntdLocale(locale)`：'zh-CN'→antd zh_CN、'en-US'→en_US（Locale 类型来自 store/app） |
| index.ts | barrel，并入 `@/design-system` |

## 设计令牌（tokens.ts，防漂移以代码为准）

| 组 | 值 |
|---|---|
| colorPrimary | **#2970FF** |
| brandColors | primaryHover #1E5BFF / primaryActive #1E53E0 / success #17B26A / warning #F79009 / error #F04438 / info #0BA5EC |
| neutralColors | textPrimary #101828 / textSecondary #475467 / textTertiary #667085 / border #E5E7EB / background #F9FAFB / backgroundElevated #FFFFFF |
| radius | sm 6 / default 8 / lg 12 / pill 999 |
| spacing | xxs 4 / xs 8 / sm 12 / md 16 / lg 24 / xl 32 / xxl 48（8px 栅格） |
| fontFamily | Inter 系（含 sans-serif 兜底）；fontSize xs12→xxl24；controlHeight 36 |

## 装配链路（AppProviders）

`main.tsx`：StrictMode → AppProviders → App(RouterProvider)。AppProviders 内：

1. `ConfigProvider locale={getAntdLocale(locale)} theme={getThemeConfig(themeKey)}`——主题与 antd 文案唯一注入点（[@FE-TH-S9](./bdd.md#fe-th-s9)）；
2. antd `<App>` 包裹 children 并内挂 `<FeedbackBridge />`，使 message/notification/modal 走上下文用法；
3. `useEffect([locale])`：`i18n.changeLanguage(locale)` + 同步 `document.documentElement.lang`（[@FE-TH-S6](./bdd.md#fe-th-s6)）。

## 持久化与切换入口

- `store/app.ts` 初始化顺序：localStorage `moai-web-theme`（合法值）→ `matchMedia('(prefers-color-scheme: dark)')` → `'light'`（[@FE-TH-S3](./bdd.md#fe-th-s3)/[@FE-TH-S4](./bdd.md#fe-th-s4)）。
- `setThemeKey`/`toggleTheme` 均手工写 localStorage 再 set；**themeKey 不在 zustand persist 的 partialize 内**（persist 仅 serverInfo+userInfo，键 `moai-web-store`），主题持久化完全靠独立键。
- 切换 UI 为 AppSider 底部 Select（Sun/Moon 两项）；`toggleTheme` 动作保留但当前 UI 未使用。

## 关键决策

1. 主色已从 #4A9EFF 迁移为 #2970FF；两组单测（config/tokens）同时锁值防文档漂移（[@FE-TH-S1](./bdd.md#fe-th-s1)/[@FE-TH-S7](./bdd.md#fe-th-s7)）。
2. 未实现 css-vars 方案：`src/index.css` 无 CSS 变量（`grep 'var(--'` 计 0）；非 antd 场景（图表等）需自行引 tokens 对象。
3. 加主题 = 扩 `ThemeKey` + `themePresets`（步骤见 [SOP 第 2 节](./sop.md)）；暗色组件级覆盖必须同补，避免对比度回归。

## 已知问题

- `ui/docs/design-system/tokens.md`（作者文档，只读）仍记旧值 #4A9EFF / success #00B578 / radius 4 等，与 tokens.ts 不同步——以代码为准，待文档 owner 更新。
- [frontend-conventions.md](../frontend-conventions.md) 写「AppHeader 的 Switch 切主题」与主色 #4A9EFF，实际代码为 AppSider 底部 Select 与 #2970FF（as-built 差异，行为以代码为准；AppHeader.tsx 并不存在，见 [../layout-routing/sdd.md](../layout-routing/sdd.md) 已知问题）。
