import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router'
import { Tabs } from 'antd'
import { Page } from '@/design-system'
import { useAppStore } from '@/store/app'
import { TeamsPanel } from '@/pages/admin/TeamsPanel'
import { OrgRolesPanel } from '@/pages/admin/OrgRolesPanel'
import { UsersPanel } from '@/pages/admin/UsersPanel'

const TAB_KEYS = ['teams', 'roles', 'users']

/** 组织用户页（仅管理员）：Tab 切换团队管理/组织管理/用户管理 */
export function AdminTeams() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const [searchParams] = useSearchParams()

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  // 旧 /users 书签经 ?tab=users 平滑落到用户 Tab
  const requestedTab = searchParams.get('tab') ?? 'teams'
  const defaultActiveKey = TAB_KEYS.includes(requestedTab) ? requestedTab : 'teams'

  return (
    <Page>
      <Tabs
        defaultActiveKey={defaultActiveKey}
        items={[
          { key: 'teams', label: t('adminTeams.tabTeams'), children: <TeamsPanel /> },
          { key: 'roles', label: t('adminTeams.tabRoles'), children: <OrgRolesPanel /> },
          { key: 'users', label: t('adminTeams.tabUsers'), children: <UsersPanel /> },
        ]}
      />
    </Page>
  )
}
