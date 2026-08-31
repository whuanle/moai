# 主题系统

对应代码：`ui/src/design-system/theme/`。

## 结构
- `tokens.ts`：令牌基线。
- `config.ts`：`ThemeKey`、`ThemePreset`、`themePresets`、`getThemeConfig`。

## 切换
- store 的 `themeKey`（`'light' | 'dark'`）驱动；`AppProviders` 传 `getThemeConfig(themeKey)` 给 antd `ConfigProvider`。

## 新增主题预设
1. 在 `config.ts` 的 `themePresets` 中新增一条记录。
2. `ThemeKey` 联合类型加入新 key。
3. 在 `theming.md` 补说明与 token 覆盖要点。

## 注意事项
- 暗色模式请勿硬编码颜色，一律经 token 或 theme 感知取色，保证对比度。
