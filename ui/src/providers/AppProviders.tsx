import { useEffect, type ReactNode } from 'react'
import { App as AntdApp, ConfigProvider } from 'antd'
import i18n from '@/i18n'
import { refreshServerInfo } from '@/api/auth'
import { useAppStore } from '@/store/app'
import { FeedbackBridge } from '@/design-system'
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

  // 每次应用加载刷新一次服务器信息：保证 RSA 公钥与网站 Logo（root 可在系统设置替换）不滞留在本地持久化快照
  useEffect(() => {
    void refreshServerInfo()
  }, [])

  return (
    <ConfigProvider locale={getAntdLocale(locale)} theme={getThemeConfig(themeKey)}>
      <AntdApp>
        <FeedbackBridge />
        {children}
      </AntdApp>
    </ConfigProvider>
  )
}
