import { theme, type ThemeConfig } from 'antd'
import type { ThemeMode } from '@/store/app'

const brandPrimary = '#4A9EFF'

const lightTheme: ThemeConfig = {
  algorithm: theme.defaultAlgorithm,
  token: {
    colorPrimary: brandPrimary,
    colorInfo: brandPrimary,
    borderRadius: 8,
  },
  components: {
    Layout: {
      headerBg: '#ffffff',
      headerHeight: 60,
    },
  },
}

const darkTheme: ThemeConfig = {
  algorithm: theme.darkAlgorithm,
  token: {
    colorPrimary: brandPrimary,
    colorInfo: brandPrimary,
    colorSuccess: '#00E676',
    colorWarning: '#FFB300',
    colorError: '#FF5252',
    borderRadius: 8,
  },
  components: {
    Layout: {
      headerBg: '#171C2B',
      headerHeight: 60,
    },
  },
}

export function getThemeConfig(mode: ThemeMode): ThemeConfig {
  return mode === 'dark' ? darkTheme : lightTheme
}
