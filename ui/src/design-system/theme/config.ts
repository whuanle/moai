import { theme, type ThemeConfig } from 'antd'
import { brandColors, colorPrimary, radius, fontFamily } from './tokens'

export type ThemeKey = 'light' | 'dark'

export interface ThemePreset {
  key: ThemeKey
  name: string
  mode: 'light' | 'dark'
  config: ThemeConfig
}

export const defaultThemeKey: ThemeKey = 'light'

export const themePresets: Record<ThemeKey, ThemePreset> = {
  light: {
    key: 'light',
    name: '亮色',
    mode: 'light',
    config: {
      algorithm: theme.defaultAlgorithm,
      token: {
        colorPrimary,
        colorInfo: brandColors.info,
        colorSuccess: brandColors.success,
        colorWarning: brandColors.warning,
        colorError: brandColors.error,
        borderRadius: radius.default,
        fontFamily,
      },
      components: {
        Layout: {
          headerBg: '#ffffff',
          headerHeight: 60,
        },
      },
    },
  },
  dark: {
    key: 'dark',
    name: '暗色',
    mode: 'dark',
    config: {
      algorithm: theme.darkAlgorithm,
      token: {
        colorPrimary,
        colorInfo: brandColors.info,
        colorSuccess: brandColors.success,
        colorWarning: brandColors.warning,
        colorError: brandColors.error,
        borderRadius: radius.default,
        fontFamily,
      },
      components: {
        Layout: {
          headerBg: '#171C2B',
          headerHeight: 60,
        },
      },
    },
  },
}

export function getThemeConfig(themeKey: ThemeKey): ThemeConfig {
  return themePresets[themeKey].config
}
