import {
  ApiOutlined,
  ApartmentOutlined,
  AppstoreAddOutlined,
  AppstoreOutlined,
  AuditOutlined,
  CloudServerOutlined,
  DashboardOutlined,
  LogoutOutlined,
  MoonOutlined,
  SettingOutlined,
  ShopOutlined,
  SunOutlined,
  TagsOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  TranslationOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Avatar, Dropdown, Layout, Menu, Select, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router'
import { AppLogo } from '@/layouts/components/AppLogo'
import { useSystemName } from '@/layouts/useSystemLogo'
import {
  useAppStore,
  type Locale,
  type ThemeMode,
} from '@/store/app'

const { Sider } = Layout

interface NavItem {
  key: string
  icon: React.ReactNode
  labelKey: string
  path: string
}

const mainNav: NavItem[] = [
  { key: 'dashboard', icon: <DashboardOutlined />, labelKey: 'nav.overview', path: '/dashboard' },
  { key: 'apps', icon: <AppstoreOutlined />, labelKey: 'nav.apps', path: '/apps' },
  { key: 'promptMarket', icon: <ShopOutlined />, labelKey: 'nav.promptMarket', path: '/prompt-market' },
  { key: 'skillMarket', icon: <ThunderboltOutlined />, labelKey: 'nav.skillMarket', path: '/skill-market' },
  { key: 'team', icon: <TeamOutlined />, labelKey: 'nav.team', path: '/team' },
]

const adminNav: NavItem[] = [
  { key: 'plugin', icon: <AppstoreAddOutlined />, labelKey: 'nav.plugin', path: '/plugin' },
  { key: 'classify', icon: <TagsOutlined />, labelKey: 'nav.classify', path: '/classify' },
  { key: 'users', icon: <UserOutlined />, labelKey: 'nav.users', path: '/users' },
  { key: 'adminTeams', icon: <ApartmentOutlined />, labelKey: 'nav.adminTeams', path: '/admin/teams' },
  { key: 'publications', icon: <AuditOutlined />, labelKey: 'nav.publications', path: '/publications' },
  { key: 'models', icon: <CloudServerOutlined />, labelKey: 'nav.channel', path: '/models' },
  { key: 'oauthconnect', icon: <ApiOutlined />, labelKey: 'nav.oauthconnect', path: '/oauthconnect' },
  { key: 'settings', icon: <SettingOutlined />, labelKey: 'nav.settings', path: '/settings' },
]

const pathToKey: Record<string, string> = {
  '/dashboard': 'dashboard',
  '/apps': 'apps',
  '/prompts': 'promptMarket',
  '/prompt-market': 'promptMarket',
  '/skills': 'skillMarket',
  '/skill-market': 'skillMarket',
  '/team': 'team',
  '/plugin': 'plugin',
  '/classify': 'classify',
  '/users': 'users',
  '/admin/teams': 'adminTeams',
  '/publications': 'publications',
  '/models': 'models',
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
  const systemName = useSystemName()
  const themeKey = useAppStore((state) => state.themeKey)
  const setThemeKey = useAppStore((state) => state.setThemeKey)
  const locale = useAppStore((state) => state.locale)
  const setLocale = useAppStore((state) => state.setLocale)
  const userInfo = useAppStore((state) => state.userInfo)
  const clearUserInfo = useAppStore((state) => state.clearUserInfo)
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const isRoot = useAppStore((state) => state.userInfo?.isRoot === true)
  // 一级菜单不再展示知识库/知识图谱，统一从团队详情分区进入
  // 提示词编辑器等 /prompts 子路径归入「提示词市场」高亮，技能中心（市场/我的）归入「技能市场」高亮
  const selectedKey =
    pathToKey[location.pathname]
    ?? (location.pathname.startsWith('/prompts')
      ? 'promptMarket'
      : location.pathname.startsWith('/skill')
        ? 'skillMarket'
        : 'dashboard')
  const isDark = themeKey === 'dark'
  const dividerColor = isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(16, 24, 40, 0.08)'

  const displayName = userInfo?.nickName ?? userInfo?.userName

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: t('common.settings'),
      onClick: () => navigate('/account'),
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

  const visibleAdminNav = adminNav.filter((item) => item.key !== 'settings' || isRoot)

  const menuItems: Required<MenuProps>['items'] = [
    ...buildMenuItems(mainNav, t),
    ...(isAdmin ? [{ type: 'divider' as const }, ...buildMenuItems(visibleAdminNav, t)] : []),
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
        <AppLogo size={28} />
        <Typography.Text strong ellipsis style={{ fontSize: 16, flex: 1 }}>
          {systemName}
        </Typography.Text>
      </div>

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
        <Dropdown menu={{ items: userMenuItems }} placement="topLeft" trigger={['click']}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '4px 4px 2px',
              cursor: 'pointer',
            }}
          >
            <Avatar size={32} icon={<UserOutlined />} src={userInfo?.avatar ?? undefined}>
              {displayName?.charAt(0) ?? 'U'}
            </Avatar>
            <div style={{ minWidth: 0, flex: 1 }}>
              <Typography.Text strong ellipsis style={{ display: 'block', fontSize: 14 }}>
                {displayName}
              </Typography.Text>
              <Typography.Text
                type="secondary"
                ellipsis
                style={{ display: 'block', fontSize: 12 }}
              >
                {userInfo?.email ?? userInfo?.userName}
              </Typography.Text>
            </div>
          </div>
        </Dropdown>
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
