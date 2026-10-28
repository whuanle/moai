# 主题系统

对应代码：`ui/src/design-system/theme/`。

## 结构
- `tokens.ts`：令牌基线（品牌语义色/圆角/字号等跨主题不变量）。
- `presets.ts`：`ThemeKey`（8 套预设 key）、`ThemePalette`（每套主题的核心色板）、`themePalettes`。
- `config.ts`：`ThemePreset`、`themePresets`、`themePresetList`、`getThemeConfig`；按调色板 + 亮/暗构建器生成 antd `ThemeConfig`，语义色（success/warning/error/info）全主题共用。

## 内置预设（亮/暗各 4 套）
- 亮色：`light` 经典白（默认）、`paper` 米黄纸、`sakura` 樱花粉、`green` 护眼绿。
- 暗色：`dark` 经典黑（默认）、`midnight` 午夜蓝、`forest` 墨玉绿、`violet` 暗夜紫。

## 切换
- store 的 `themeKey`（`ThemeKey` 预设 key，`localStorage['moai-web-theme']` 单独持久化）驱动；`AppProviders` 传 `getThemeConfig(themeKey)` 给 antd `ConfigProvider`。
- `AppProviders` 同时把预设落到 `<html>`：`data-theme`（预设 key）、`data-theme-mode`（亮/暗）、`--moai-bg-layout`（页面底色），index.css 的 body 背景与暗色滚动条据此渲染。
- 亮/暗判断一律用 `themePresets[themeKey].mode`，禁止 `themeKey === 'dark'` 字面比较（现在有 4 套暗色 key）。
- antd 只接受亮/暗二值的场景（如 `Sider theme`、Monaco `vs-dark`）传 `mode`，不传预设 key。
- 选择器入口在 Sider 底部：亮/暗两组各 4 套，选项带底色+强调色双色圆点（`AppSider.tsx`）。

## 新增主题预设
1. 在 `presets.ts` 补一条 `ThemePalette`（背景/文字/边框/菜单/表格头成套取色，对比度对齐既有预设）。
2. `ThemeKey` 联合类型与 `config.ts` 的 `themeKeyOrder`/`themeNameKeys`/`themeNames` 各加一项。
3. i18n：`zh-CN`/`en-US` 的 `common.json` 补 `common.themePresets.<key>`（同步改）。
4. 在 `theming.md` 补说明；单测 `theme/__tests__/config.test.ts` 的数量断言同步调整。

## 注意事项
- 暗色模式请勿硬编码颜色，一律经 token 或 `useNeutralColors` 等 theme 感知方式取色，保证对比度。
- 旧用户存量的 `moai-web-theme=light/dark` 仍是合法预设 key，无需迁移；非法值回退到跟随系统。
