import {
  ApiOutlined,
  AppstoreAddOutlined,
  AppstoreOutlined,
  BookOutlined,
  DashboardOutlined,
  LogoutOutlined,
  MoonOutlined,
  SettingOutlined,
  SunOutlined,
  TeamOutlined,
  TranslationOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Avatar, Dropdown, Layout, Menu, Select, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router'
import { useAppStore, type Locale, type ThemeMode } from '@/store/app'

const { Sider } = Layout

interface NavItem {
  key: string
  icon: React.ReactNode
  labelKey: string
  path: string
}

const mainNav: NavItem[] = [
  { key: 'dashboard', icon: <DashboardOutlined />, labelKey: 'nav.overview', path: '/dashboard' },
  { key: 'app', icon: <AppstoreOutlined />, labelKey: 'nav.app', path: '/app' },
  { key: 'wiki', icon: <BookOutlined />, labelKey: 'nav.wiki', path: '/wiki' },
  { key: 'team', icon: <TeamOutlined />, labelKey: 'nav.team', path: '/team' },
]

const adminNav: NavItem[] = [
  { key: 'plugin', icon: <AppstoreAddOutlined />, labelKey: 'nav.plugin', path: '/plugin' },
  { key: 'users', icon: <UserOutlined />, labelKey: 'nav.users', path: '/users' },
  { key: 'oauthconnect', icon: <ApiOutlined />, labelKey: 'nav.oauthconnect', path: '/oauthconnect' },
  { key: 'settings', icon: <SettingOutlined />, labelKey: 'nav.settings', path: '/settings' },
]

const pathToKey: Record<string, string> = {
  '/dashboard': 'dashboard',
  '/app': 'app',
  '/wiki': 'wiki',
  '/team': 'team',
  '/plugin': 'plugin',
  '/users': 'users',
  '/oauthconnect': 'oauthconnect',
  '/settings': 'settings',
}

const localeOptions = [
  { label: '简体中文', value: 'zh-CN' },
  { label: 'English', value: 'en-US' },
]

function buildMenuItems(items: NavItem[], t: (key: string) => string): Required<MenuProps>['items'] {
  return items.map((item) => ({ key: item.key, icon: item.icon, label: t(item.labelKey) }))
}

export function AppSider() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const themeKey = useAppStore((state) => state.themeKey)
  const setThemeKey = useAppStore((state) => state.setThemeKey)
  const locale = useAppStore((state) => state.locale)
  const setLocale = useAppStore((state) => state.setLocale)
  const userInfo = useAppStore((state) => state.userInfo)
  const clearUserInfo = useAppStore((state) => state.clearUserInfo)
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const selectedKey = pathToKey[location.pathname] ?? 'dashboard'
  const isDark = themeKey === 'dark'
  const dividerColor = isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(16, 24, 40, 0.08)'

  const displayName = userInfo?.nickName ?? userInfo?.userName

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: t('common.settings'),
      onClick: () => navigate('/settings'),
    },
    { type: 'divider' },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: t('common.logout'),
      onClick: () => {
        clearUserInfo()
        navigate('/login')
      },
    },
  ]

  const themeOptions = [
    { value: 'light', label: (<span><SunOutlined /> {t('common.light')}</span>) },
    { value: 'dark', label: (<span><MoonOutlined /> {t('common.dark')}</span>) },
  ]

  const onClick: MenuProps['onClick'] = ({ key }) => {
    const item = [...mainNav, ...adminNav].find((nav) => nav.key === key)
    if (item) navigate(item.path)
  }

  const menuItems: Required<MenuProps>['items'] = [
    ...buildMenuItems(mainNav, t),
    ...(isAdmin ? [{ type: 'divider' as const }, ...buildMenuItems(adminNav, t)] : []),
  ]

  return (
    <Sider
      width={232}
      theme={themeKey}
      style={{
        borderRight: `1px solid ${dividerColor}`,
        height: '100vh',
        position: 'sticky',
        top: 0,
        overflow: 'hidden',
      }}
    >
      <div
        style={{
          height: '100%',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
      <div style={{ padding: '16px 16px 12px', display: 'flex', alignItems: 'center', gap: 10 }}>
        <img src="/logo.svg" width={28} height={28} alt="logo" />
        <Typography.Text strong style={{ fontSize: 16 }}>
          {t('app.name')}
        </Typography.Text>
      </div>

      <Dropdown menu={{ items: userMenuItems }} placement="bottomLeft" trigger={['click']}>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            padding: '8px 16px 16px',
            cursor: 'pointer',
          }}
        >
          <Avatar size={34} icon={<UserOutlined />} src={userInfo?.avatar ?? undefined}>
            {displayName?.charAt(0) ?? 'U'}
          </Avatar>
          <div style={{ minWidth: 0, flex: 1 }}>
            <Typography.Text strong ellipsis style={{ display: 'block', fontSize: 14 }}>
              {displayName}
            </Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {userInfo?.email ?? userInfo?.userName}
            </Typography.Text>
          </div>
        </div>
      </Dropdown>

      <Menu
        mode="inline"
        items={menuItems}
        selectedKeys={[selectedKey]}
        onClick={onClick}
        style={{ flex: 1, minHeight: 0, overflow: 'auto', borderInlineEnd: 'none' }}
      />

      <div
        style={{
          padding: 12,
          borderTop: `1px solid ${dividerColor}`,
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
        }}
      >
        <Select
          value={themeKey}
          options={themeOptions}
          onChange={(value: ThemeMode) => setThemeKey(value)}
          style={{ width: '100%' }}
          popupMatchSelectWidth={false}
        />
        <Select
          value={locale}
          options={localeOptions}
          onChange={(value: Locale) => setLocale(value)}
          suffixIcon={<TranslationOutlined />}
          style={{ width: '100%' }}
          popupMatchSelectWidth={false}
        />
      </div>
      </div>
    </Sider>
  )
}
