# 前端主题系统（theme）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 改主色 / 增减令牌

1. 改 `ui/src/design-system/theme/tokens.ts`（单一来源）；config.ts 引用常量自动跟随。
2. 新 token 需影响 antd 组件时，在 `themePresets.light/dark` 的 `token`/`components` 段补覆盖——**暗色必须同补**，避免对比度回归。
3. 业务侧一律 `import { xxx } from '@/design-system'`，禁止硬编码 #hex/魔法数（[@FE-TH-S8](./bdd.md#fe-th-s8)）。
4. 补 [tokens.test.ts](../../src/design-system/theme/__tests__/tokens.test.ts) 断言并跑第 4 节回归。
5. `ui/docs/design-system/tokens.md` 为作者文档（只读），数值同步由文档 owner 处理（已知漂移见 [SDD 已知问题](./sdd.md)）。

## 2. 新增主题预设

扩 `config.ts` 的 `ThemeKey` → `themePresets` 加记录（mode + algorithm + token/components 覆盖，参考 light/dark 写法）→ `AppSider.tsx` 的 `themeOptions` 加选项 → 补 config.test.ts 预设键列表断言。

## 3. 排障

| 现象 | 原因 | 处理 | 场景 |
|---|---|---|---|
| 暗色下自定义样式发白 | 未走令牌/antd 变量（无 css-vars 方案） | 改引 tokens 对象 | [@FE-TH-S8](./bdd.md#fe-th-s8) |
| 刷新后主题丢失 | localStorage `moai-web-theme` 被清（themeKey 不在 persist 键内） | 排查清存储的代码 | [@FE-TH-S5](./bdd.md#fe-th-s5) |
| antd 文案中英混杂 | locale 未进 ConfigProvider 或 i18n 未切换 | 查 AppProviders 装配点 | [@FE-TH-S6](./bdd.md#fe-th-s6) |
| 系统切暗色页面不变 | 手动选过后以本地记录为准 | 清 `moai-web-theme` 回到跟随系统 | [@FE-TH-S3](./bdd.md#fe-th-s3)/[@FE-TH-S4](./bdd.md#fe-th-s4) |

## 4. 验收流程（改主题后）

1. `cd ui && npx vitest run src/design-system/theme` 全过。
2. `npm run typecheck && npm run lint` 全绿。
3. 手工：清 localStorage 打开应用（初始跟随系统，[@FE-TH-S3](./bdd.md#fe-th-s3)）→ Sider 底部切暗色，Layout/Menu/Table/Card/Modal 全套变暗且刷新保持（[@FE-TH-S5](./bdd.md#fe-th-s5)）→ zh↔en 切换文案同步（[@FE-TH-S6](./bdd.md#fe-th-s6)）→ 暗色走查 Dashboard/Users/Settings 对比度。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 交付验收（轮 13，as-built）**：`npx vitest run src/design-system/theme` 6/6（config 3 + tokens 3）；`npm run typecheck`（tsc -b --noEmit 退出码 0）与 `npm run lint`（无输出）均绿；grep 复核主色 #2970FF、`src/index.css` 无 CSS 变量（44 行）。记录主色 #4A9EFF→#2970FF 迁移事实；切换入口实为 AppSider 底部 Select（规范文档「AppHeader Switch」表述过时）；themeKey 持久化机制（独立键 `moai-web-theme`，不经 persist）。
- **2026-09-01 文档标准重构回归**：定向 2 files / 6 tests 复测通过。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 13，as-built）；同日按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-TH-S1~S9，旧 BDD 补全 Gherkin）、四件互链、职责瘦身 |
