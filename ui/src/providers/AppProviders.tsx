import { useEffect, type ReactNode } from 'react'
import { App as AntdApp, ConfigProvider } from 'antd'
import i18n from '@/i18n'
import { useAppStore } from '@/store/app'
import { getAntdLocale } from '@/theme/locale'
import { getThemeConfig } from '@/theme/config'

interface AppProvidersProps {
  children: ReactNode
}

export function AppProviders({ children }: AppProvidersProps) {
  const themeMode = useAppStore((state) => state.themeMode)
  const locale = useAppStore((state) => state.locale)

  useEffect(() => {
    i18n.changeLanguage(locale)
    document.documentElement.lang = locale
  }, [locale])

  return (
    <ConfigProvider locale={getAntdLocale(locale)} theme={getThemeConfig(themeMode)}>
      <AntdApp>{children}</AntdApp>
    </ConfigProvider>
  )
}
