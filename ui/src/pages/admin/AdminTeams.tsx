import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { Tabs } from 'antd'
import { Page } from '@/design-system'
import { useAppStore } from '@/store/app'
import { TeamsPanel } from '@/pages/admin/TeamsPanel'
import { OrgRolesPanel } from '@/pages/admin/OrgRolesPanel'

/** 组织角色管理页（仅管理员）：Tab 切换团队管理与角色管理 */
export function AdminTeams() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  return (
    <Page>
      <Tabs
        defaultActiveKey="teams"
        items={[
          { key: 'teams', label: t('adminTeams.tabTeams'), children: <TeamsPanel /> },
          { key: 'roles', label: t('adminTeams.tabRoles'), children: <OrgRolesPanel /> },
        ]}
      />
    </Page>
  )
}
