import { Navigate } from 'react-router'
import { Empty, Tabs } from 'antd'
import { useTranslation } from 'react-i18next'
import { Page } from '@/design-system'
import { useAppStore } from '@/store/app'

export function Plugins() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  const items = [
    { key: 'custom', label: t('plugins.tabCustom'), children: <Empty description={t('plugins.empty')} /> },
    { key: 'dynamic', label: t('plugins.tabDynamic'), children: <Empty description={t('plugins.empty')} /> },
    { key: 'static', label: t('plugins.tabStatic'), children: <Empty description={t('plugins.empty')} /> },
  ]

  return (
    <Page>
      <Tabs items={items} />
    </Page>
  )
}
