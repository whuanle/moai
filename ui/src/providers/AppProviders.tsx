import { useEffect, type ReactNode } from 'react'
import { App as AntdApp, ConfigProvider } from 'antd'
import i18n from '@/i18n'
import { useAppStore } from '@/store/app'
import { getAntdLocale, getThemeConfig } from '@/design-system/theme'

interface AppProvidersProps {
  children: ReactNode
}

export function AppProviders({ children }: AppProvidersProps) {
  const themeKey = useAppStore((state) => state.themeKey)
  const locale = useAppStore((state) => state.locale)

  useEffect(() => {
    i18n.changeLanguage(locale)
    document.documentElement.lang = locale
  }, [locale])

  return (
    <ConfigProvider locale={getAntdLocale(locale)} theme={getThemeConfig(themeKey)}>
      <AntdApp>{children}</AntdApp>
    </ConfigProvider>
  )
}
