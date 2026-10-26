import { theme, type ThemeConfig } from 'antd'
import { brandColors, radius, fontFamily } from './tokens'
import { themePalettes, type ThemeKey, type ThemeMode, type ThemePalette } from './presets'

export type { ThemeKey, ThemeMode }

export interface ThemePreset {
  key: ThemeKey
  /** 展示名 i18n key（common.themePresets.<key>，zh-CN 与 en-US 同步维护） */
  nameKey: string
  /** 中文兜底名（非 React 场景与测试用，页面展示一律走 nameKey） */
  name: string
  mode: ThemeMode
  palette: ThemePalette
  config: ThemeConfig
}

export const defaultThemeKey: ThemeKey = 'light'

/** 亮/暗两个家族各自的默认预设（toggleTheme 在两家族默认间切换） */
export const defaultThemeKeyByMode: Record<ThemeMode, ThemeKey> = {
  light: 'light',
  dark: 'dark',
}

const themeNameKeys: Record<ThemeKey, string> = {
  light: 'common.themePresets.light',
  paper: 'common.themePresets.paper',
  sakura: 'common.themePresets.sakura',
  green: 'common.themePresets.green',
  dark: 'common.themePresets.dark',
  midnight: 'common.themePresets.midnight',
  forest: 'common.themePresets.forest',
  violet: 'common.themePresets.violet',
}

const themeNames: Record<ThemeKey, string> = {
  light: '经典白',
  paper: '米黄纸',
  sakura: '樱花粉',
  green: '护眼绿',
  dark: '经典黑',
  midnight: '午夜蓝',
  forest: '墨玉绿',
  violet: '暗夜紫',
}

/** 亮/暗共用的结构令牌（圆角/高度/字号等），颜色差异全部来自调色板 */
function buildBaseTokens(palette: ThemePalette): ThemeConfig['token'] {
  return {
    colorPrimary: palette.accent,
    colorInfo: brandColors.info,
    colorSuccess: brandColors.success,
    colorWarning: brandColors.warning,
    colorError: brandColors.error,
    colorTextBase: palette.textPrimary,
    colorText: palette.textPrimary,
    colorTextSecondary: palette.textSecondary,
    colorTextTertiary: palette.textTertiary,
    colorTextQuaternary: palette.textQuaternary,
    colorBgBase: palette.bgLayout,
    colorBgLayout: palette.bgLayout,
    colorBgContainer: palette.bgContainer,
    colorBgElevated: palette.bgElevated,
    colorBorder: palette.border,
    colorBorderSecondary: palette.borderSecondary,
    borderRadius: radius.default,
    borderRadiusLG: radius.lg,
    borderRadiusSM: radius.sm,
    fontFamily,
    fontSize: 14,
    controlHeight: 36,
  }
}

function buildSharedComponents(palette: ThemePalette): ThemeConfig['components'] {
  return {
    Layout: {
      headerBg: palette.bgContainer,
      headerHeight: 56,
      headerPadding: '0 24px',
      siderBg: palette.bgContainer,
      bodyBg: palette.bgLayout,
    },
    Menu: {
      itemBg: 'transparent',
      subMenuItemBg: 'transparent',
      itemColor: palette.menu.item,
      itemHoverColor: palette.menu.hover,
      itemSelectedColor: palette.menu.selected,
      itemSelectedBg: palette.menu.selectedBg,
      itemHoverBg: palette.menu.hoverBg,
      itemBorderRadius: 8,
      itemHeight: 40,
      itemMarginInline: 12,
      itemMarginBlock: 4,
    },
    Button: {
      borderRadius: 8,
      controlHeight: 36,
      primaryShadow: 'none',
    },
    Input: {
      borderRadius: 8,
      controlHeight: 36,
    },
    Select: {
      borderRadius: 8,
      controlHeight: 36,
    },
    Card: {
      borderRadiusLG: radius.lg,
      colorBgContainer: palette.bgContainer,
    },
    Table: {
      headerBg: palette.table.headerBg,
      headerColor: palette.table.headerColor,
      borderColor: palette.table.borderColor,
      headerBorderRadius: 6,
    },
    Modal: {
      borderRadiusLG: 12,
    },
    Tag: {
      borderRadiusSM: 6,
    },
    Statistic: {
      contentFontSize: 24,
    },
  }
}

function buildLightConfig(palette: ThemePalette): ThemeConfig {
  return {
    algorithm: theme.defaultAlgorithm,
    token: {
      ...buildBaseTokens(palette),
      boxShadow: '0 1px 2px rgba(16, 24, 40, 0.05)',
    },
    components: buildSharedComponents(palette),
  }
}

function buildDarkConfig(palette: ThemePalette): ThemeConfig {
  return {
    algorithm: theme.darkAlgorithm,
    token: {
      ...buildBaseTokens(palette),
      boxShadow: '0 1px 2px rgba(0, 0, 0, 0.4)',
    },
    components: buildSharedComponents(palette),
  }
}

const themeConfigBuilders: Record<ThemeMode, (palette: ThemePalette) => ThemeConfig> = {
  light: buildLightConfig,
  dark: buildDarkConfig,
}

const themeKeyOrder: ThemeKey[] = ['light', 'paper', 'sakura', 'green', 'dark', 'midnight', 'forest', 'violet']

/** 全部预设（亮色 4 套在前、暗色 4 套在后），供选择器分组展示 */
export const themePresetList: ThemePreset[] = themeKeyOrder.map((key) => ({
  key,
  nameKey: themeNameKeys[key],
  name: themeNames[key],
  mode: themePalettes[key].mode,
  palette: themePalettes[key],
  config: themeConfigBuilders[themePalettes[key].mode](themePalettes[key]),
}))

export const themePresets: Record<ThemeKey, ThemePreset> = Object.fromEntries(
  themePresetList.map((preset) => [preset.key, preset]),
) as Record<ThemeKey, ThemePreset>

export function isThemeKey(value: unknown): value is ThemeKey {
  return typeof value === 'string' && Object.prototype.hasOwnProperty.call(themePalettes, value)
}

export function getThemePreset(themeKey: ThemeKey): ThemePreset {
  return themePresets[themeKey]
}

export function getThemeConfig(themeKey: ThemeKey): ThemeConfig {
  return themePresets[themeKey].config
}
