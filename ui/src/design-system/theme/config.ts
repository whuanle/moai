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
        colorTextBase: '#101828',
        colorText: '#101828',
        colorTextSecondary: '#475467',
        colorTextTertiary: '#667085',
        colorTextQuaternary: '#98A2B3',
        colorBgBase: '#F9FAFB',
        colorBgLayout: '#F9FAFB',
        colorBgContainer: '#FFFFFF',
        colorBorder: '#E5E7EB',
        colorBorderSecondary: '#EAECF0',
        borderRadius: radius.default,
        borderRadiusLG: radius.lg,
        borderRadiusSM: radius.sm,
        fontFamily,
        fontSize: 14,
        controlHeight: 36,
        boxShadow: '0 1px 2px rgba(16, 24, 40, 0.05)',
      },
      components: {
        Layout: {
          headerBg: '#FFFFFF',
          headerHeight: 56,
          headerPadding: '0 24px',
          siderBg: '#FFFFFF',
          bodyBg: '#F9FAFB',
        },
        Menu: {
          itemBg: 'transparent',
          subMenuItemBg: 'transparent',
          itemColor: '#667085',
          itemHoverColor: '#344054',
          itemSelectedColor: '#2970FF',
          itemSelectedBg: '#EFF4FF',
          itemHoverBg: '#F2F4F7',
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
          colorBgContainer: '#FFFFFF',
        },
        Table: {
          headerBg: '#F9FAFB',
          headerColor: '#475467',
          borderColor: '#EAECF0',
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
        colorTextBase: '#F2F4F7',
        colorText: '#F2F4F7',
        colorTextSecondary: '#C0C5D0',
        colorTextTertiary: '#8A93A5',
        colorTextQuaternary: '#556070',
        colorBgBase: '#101828',
        colorBgLayout: '#101828',
        colorBgContainer: '#1C2536',
        colorBgElevated: '#1C2536',
        colorBorder: '#344054',
        colorBorderSecondary: '#2A3446',
        borderRadius: radius.default,
        borderRadiusLG: radius.lg,
        borderRadiusSM: radius.sm,
        fontFamily,
        fontSize: 14,
        controlHeight: 36,
        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.4)',
      },
      components: {
        Layout: {
          headerBg: '#1C2536',
          headerHeight: 56,
          headerPadding: '0 24px',
          siderBg: '#1C2536',
          bodyBg: '#101828',
        },
        Menu: {
          itemBg: 'transparent',
          subMenuItemBg: 'transparent',
          itemColor: '#8A93A5',
          itemHoverColor: '#C0C5D0',
          itemSelectedColor: '#7BA2FF',
          itemSelectedBg: '#24304A',
          itemHoverBg: '#222D44',
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
          colorBgContainer: '#1C2536',
        },
        Table: {
          headerBg: '#222D44',
          headerColor: '#C0C5D0',
          borderColor: '#2A3446',
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
      },
    },
  },
}

export function getThemeConfig(themeKey: ThemeKey): ThemeConfig {
  return themePresets[themeKey].config
}
