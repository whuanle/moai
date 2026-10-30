import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router'
import { Tabs } from 'antd'
import { Page } from '@/design-system'
import { useAppStore } from '@/store/app'
import { SubsystemsPanel } from '@/pages/admin/AdminSubsystems'
import { SystemAccessAppsPanel } from '@/pages/admin/AdminSystemAccessApps'

/**
 * 应用接入中心（仅 root）：Tab 承载「子系统管理」（业务子系统，委托 SSO/团队授权/在线状态）
 * 与「应用接入」（系统级接入 key，ERP 等内部系统经 /api/subsys/system/* 维护系统资源）。
 */
export function AdminIntegrations() {
  const { t } = useTranslation()
  const isRoot = useAppStore((state) => state.userInfo?.isRoot === true)
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get('tab') === 'apps' ? 'apps' : 'subsystems'

  if (!isRoot) {
    return <Navigate to="/apps" replace />
  }

  return (
    <Page>
      <Tabs
        activeKey={tab}
        onChange={(key) => setSearchParams(key === 'subsystems' ? {} : { tab: key }, { replace: true })}
        items={[
          {
            key: 'subsystems',
            label: t('adminIntegrations.tabSubsystems'),
            children: <SubsystemsPanel />,
          },
          {
            key: 'apps',
            label: t('adminIntegrations.tabAccessApps'),
            children: <SystemAccessAppsPanel />,
          },
        ]}
      />
    </Page>
  )
}
