import {
  BellOutlined,
  LinkOutlined,
  LockOutlined,
  SafetyCertificateOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Layout, Menu } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate, useParams } from 'react-router'
import { AccountOauth } from '@/pages/account/AccountOauth'
import { AccountPassword } from '@/pages/account/AccountPassword'
import { AccountProfile } from '@/pages/account/AccountProfile'
import { AccountTwoFactor } from '@/pages/account/AccountTwoFactor'
import { Messages } from '@/pages/account/Messages'

const { Sider, Content } = Layout

const SECTION_KEYS = ['messages', 'profile', 'password', 'twofa', 'oauth'] as const
type SectionKey = (typeof SECTION_KEYS)[number]

const DEFAULT_SECTION: SectionKey = 'messages'

const SECTION_COMPONENTS: Record<SectionKey, () => React.JSX.Element> = {
  messages: Messages,
  profile: AccountProfile,
  password: AccountPassword,
  twofa: AccountTwoFactor,
  oauth: AccountOauth,
}

/** 个人中心：二级菜单（消息中心 / 基本信息 / 修改密码 / 两步验证 / 第三方账号），沿用 team/:id/:section? 的 URL 分区导航模式 */
export function AccountPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ section?: string }>()

  const rawSection = params.section ?? DEFAULT_SECTION
  const section: SectionKey = SECTION_KEYS.includes(rawSection as SectionKey)
    ? (rawSection as SectionKey)
    : DEFAULT_SECTION

  if (!params.section || params.section === 'settings') {
    // 旧「账号设置」聚合页拆分为独立分区，老链接重定向到基本信息
    return <Navigate to={`/account/${params.section === 'settings' ? 'profile' : section}`} replace />
  }

  const menuItems: Required<MenuProps>['items'] = [
    { key: 'messages', icon: <BellOutlined />, label: t('messages.title') },
    { key: 'profile', icon: <UserOutlined />, label: t('account.menuProfile') },
    { key: 'password', icon: <LockOutlined />, label: t('account.menuPassword') },
    { key: 'twofa', icon: <SafetyCertificateOutlined />, label: t('account.menuTwofa') },
    { key: 'oauth', icon: <LinkOutlined />, label: t('account.menuOauth') },
  ]

  const SectionContent = SECTION_COMPONENTS[section]

  return (
    <Layout style={{ background: 'transparent', gap: 16 }}>
      <Sider width={200} style={{ background: 'transparent' }}>
        <Menu
          mode="inline"
          items={menuItems}
          selectedKeys={[section]}
          onClick={({ key }) => navigate(`/account/${key}`)}
        />
      </Sider>
      <Content>
        <SectionContent />
      </Content>
    </Layout>
  )
}
