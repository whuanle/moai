import {
  AppstoreOutlined,
  BookOutlined,
  FileTextOutlined,
  LogoutOutlined,
  MoonOutlined,
  RobotOutlined,
  SettingOutlined,
  SunOutlined,
  TeamOutlined,
  TranslationOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Avatar, Dropdown, Layout, Menu, Select, Switch } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { useAppStore, type Locale } from '@/store/app'

const { Header } = Layout

const localeOptions = [
  { label: '简体中文', value: 'zh-CN' },
  { label: 'English', value: 'en-US' },
]

export function AppHeader() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const themeKey = useAppStore((state) => state.themeKey)
  const locale = useAppStore((state) => state.locale)
  const userInfo = useAppStore((state) => state.userInfo)
  const toggleTheme = useAppStore((state) => state.toggleTheme)
  const setLocale = useAppStore((state) => state.setLocale)
  const clearUserInfo = useAppStore((state) => state.clearUserInfo)

  const menuItems: MenuProps['items'] = [
    {
      key: 'chat',
      icon: <RobotOutlined />,
      label: t('nav.chat'),
      onClick: () => navigate('/chat'),
    },
    {
      key: 'app',
      icon: <AppstoreOutlined />,
      label: t('nav.app'),
      onClick: () => navigate('/app'),
    },
    {
      key: 'wiki',
      icon: <BookOutlined />,
      label: t('nav.wiki'),
      onClick: () => navigate('/wiki'),
    },
    {
      key: 'team',
      icon: <TeamOutlined />,
      label: t('nav.team'),
      onClick: () => navigate('/team'),
    },
    {
      key: 'prompt',
      icon: <FileTextOutlined />,
      label: t('nav.prompt'),
      onClick: () => navigate('/prompt'),
    },
    {
      key: 'plugin',
      icon: <SettingOutlined />,
      label: t('nav.plugin'),
      onClick: () => navigate('/plugin'),
    },
  ]

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'settings',
      icon: <UserOutlined />,
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

  return (
    <Header
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingInline: 24,
        borderBottom: '1px solid rgba(128,128,128,0.15)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 24, minWidth: 0 }}>
        <div
          onClick={() => navigate('/')}
          style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer' }}
        >
          <img src="/logo.svg" width={30} height={30} alt="logo" />
          <span style={{ fontWeight: 700, fontSize: 16 }}>{t('app.name')}</span>
        </div>
        <Menu mode="horizontal" items={menuItems} selectable={false} style={{ flex: 1, minWidth: 0 }} />
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <Switch
          checked={themeKey === 'dark'}
          checkedChildren={<MoonOutlined />}
          unCheckedChildren={<SunOutlined />}
          onChange={toggleTheme}
          aria-label={themeKey === 'dark' ? t('common.switchToLight') : t('common.switchToDark')}
        />
        <Select
          value={locale}
          options={localeOptions}
          onChange={(value: Locale) => setLocale(value)}
          suffixIcon={<TranslationOutlined />}
          style={{ width: 120 }}
          popupMatchSelectWidth={false}
        />
        <Dropdown menu={{ items: userMenuItems }} placement="bottomRight">
          <Avatar
            size={32}
            icon={<UserOutlined />}
            style={{ cursor: 'pointer' }}
            src={undefined}
          >
            {userInfo?.userName ?? 'U'}
          </Avatar>
        </Dropdown>
      </div>
    </Header>
  )
}
