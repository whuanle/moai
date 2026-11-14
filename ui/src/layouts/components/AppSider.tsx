import {
  ApiOutlined,
  ApartmentOutlined,
  BellOutlined,
  DatabaseOutlined,
  DeploymentUnitOutlined,
  AppstoreAddOutlined,
  AppstoreOutlined,
  AuditOutlined,
  CloudServerOutlined,
  DownOutlined,
  FileSearchOutlined,
  LogoutOutlined,
  SettingOutlined,
  ShopOutlined,
  TagsOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  TranslationOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Avatar, Badge, Button, Dropdown, Layout, Select, Tooltip, Typography, theme as antdTheme } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router'
import { AppLogo } from '@/layouts/components/AppLogo'
import { useMenuBadges } from '@/layouts/useMenuBadges'
import { useSystemName } from '@/layouts/useSystemLogo'
import './AppSider.css'
import { themePresetList, themePresets, type ThemeKey } from '@/design-system/theme'
import {
  useAppStore,
  type Locale,
} from '@/store/app'
import { useBadgeStore } from '@/store/badges'
import { useNotificationStore } from '@/store/notification'

const { Sider } = Layout

interface NavItem {
  key: string
  icon: React.ReactNode
  labelKey: string
  path: string
}

const mainNav: NavItem[] = [
  { key: 'apps', icon: <AppstoreOutlined />, labelKey: 'nav.apps', path: '/apps' },
  { key: 'promptMarket', icon: <ShopOutlined />, labelKey: 'nav.promptMarket', path: '/prompt-market' },
  { key: 'skillMarket', icon: <ThunderboltOutlined />, labelKey: 'nav.skillMarket', path: '/skill-market' },
  { key: 'memory', icon: <DatabaseOutlined />, labelKey: 'nav.memory', path: '/memory' },
  { key: 'team', icon: <TeamOutlined />, labelKey: 'nav.team', path: '/team' },
]

const adminNav: NavItem[] = [
  { key: 'plugin', icon: <AppstoreAddOutlined />, labelKey: 'nav.plugin', path: '/plugin' },
  { key: 'classify', icon: <TagsOutlined />, labelKey: 'nav.classify', path: '/classify' },
  { key: 'adminTeams', icon: <ApartmentOutlined />, labelKey: 'nav.adminTeams', path: '/admin/teams' },
  { key: 'adminIntegrations', icon: <DeploymentUnitOutlined />, labelKey: 'nav.adminIntegrations', path: '/admin/integrations' },
  { key: 'logEvents', icon: <FileSearchOutlined />, labelKey: 'nav.logEvents', path: '/admin/loginsafety' },
  { key: 'notificationCenter', icon: <BellOutlined />, labelKey: 'nav.notificationCenter', path: '/admin/notifications' },
  { key: 'publications', icon: <AuditOutlined />, labelKey: 'nav.publications', path: '/publications' },
  { key: 'models', icon: <CloudServerOutlined />, labelKey: 'nav.channel', path: '/models' },
  { key: 'oauthconnect', icon: <ApiOutlined />, labelKey: 'nav.oauthconnect', path: '/oauthconnect' },
  { key: 'settings', icon: <SettingOutlined />, labelKey: 'nav.settings', path: '/settings' },
]

const pathToKey: Record<string, string> = {
  '/apps': 'apps',
  '/prompts': 'promptMarket',
  '/prompt-market': 'promptMarket',
  '/skills': 'skillMarket',
  '/skill-market': 'skillMarket',
  '/memory': 'memory',
  '/team': 'team',
  '/plugin': 'plugin',
  '/classify': 'classify',
  '/admin/teams': 'adminTeams',
  '/admin/integrations': 'adminIntegrations',
  '/admin/subsystems': 'adminIntegrations',
  '/admin/loginsafety': 'logEvents',
  '/admin/notifications': 'notificationCenter',
  '/publications': 'publications',
  '/models': 'models',
  '/oauthconnect': 'oauthconnect',
  '/settings': 'settings',
}

const localeOptions = [
  { label: '简体中文', value: 'zh-CN' },
  { label: 'English', value: 'en-US' },
]

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
  const unreadCount = useNotificationStore((state) => state.unreadCount)
  // 一级菜单不再展示知识库/知识图谱，统一从团队详情分区进入
  // 提示词编辑器等 /prompts 子路径归入「提示词市场」高亮，技能中心（市场/我的）归入「技能市场」高亮
  const selectedKey =
    pathToKey[location.pathname]
    ?? (location.pathname.startsWith('/prompts')
      ? 'promptMarket'
      : location.pathname.startsWith('/skill')
        ? 'skillMarket'
        : '')
  const isDark = themePresets[themeKey].mode === 'dark'
  const dividerColor = isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(16, 24, 40, 0.08)'

  // 菜单角标（待处理数量）：管理员下轮询未读风险事件等数据源
  useMenuBadges()
  const badgeCounts = useBadgeStore((state) => state.counts)

  const displayName = userInfo?.nickName ?? userInfo?.userName

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: t('common.settings'),
      onClick: () => navigate('/account/profile'),
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

  /** 主题选项：亮/暗两组，各 4 套预设，选项带底色+强调色双色圆点 */
  const themeOptions = (['light', 'dark'] as const).map((mode) => ({
    key: mode,
    label: t(mode === 'light' ? 'common.light' : 'common.dark'),
    title: t(mode === 'light' ? 'common.light' : 'common.dark'),
    options: themePresetList
      .filter((preset) => preset.mode === mode)
      .map((preset) => ({
        value: preset.key,
        label: (
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
            <span
              aria-hidden
              style={{
                width: 14,
                height: 14,
                borderRadius: '50%',
                flexShrink: 0,
                background: `linear-gradient(135deg, ${preset.palette.accent} 0 50%, ${preset.palette.bgContainer} 50% 100%)`,
                border: `1px solid ${preset.palette.border}`,
                boxSizing: 'border-box',
              }}
            />
            {t(preset.nameKey)}
          </span>
        ),
      })),
  }))

  // settings 与子系统管理仅 root 可见（root 必然 isAdmin）
  const visibleAdminNav = adminNav.filter((item) => (item.key !== 'settings' && item.key !== 'adminIntegrations') || isRoot)

  // 自定义导航：替代 antd Menu
  // 风格：整栏统一底色 + 大号中性图标 + 浅主色渐变底激活态（明显但不深）、浅色 hover（token 生成，8 套预设自适应）
  // 注意：非选中项不能写内联 background/color，否则内联样式优先级会盖掉 CSS :hover 规则
  const { token } = antdTheme.useToken()

  const renderNavItem = (item: NavItem) => {
    const active = item.key === selectedKey
    const badgeCount = badgeCounts[item.key] ?? 0
    return (
      <button
        key={item.key}
        type="button"
        className={active ? 'app-nav-item app-nav-item-active' : 'app-nav-item'}
        style={
          active
            ? {
                background: `linear-gradient(135deg, ${token.colorPrimaryBgHover} 0%, ${token.colorPrimaryBg} 100%)`,
                color: token.colorPrimaryTextActive,
                fontWeight: 600,
              }
            : undefined
        }
        aria-current={active ? 'page' : undefined}
        onClick={() => navigate(item.path)}
      >
        {item.icon}
        <span>{t(item.labelKey)}</span>
        {badgeCount > 0 && (
          <Badge
            count={badgeCount}
            overflowCount={99}
            size="small"
            style={{ marginLeft: 'auto', boxShadow: 'none', flexShrink: 0 }}
          />
        )}
      </button>
    )
  }

  const renderGroupLabel = (labelKey: string) => (
    <div className="app-nav-group-label" style={{ color: token.colorTextTertiary }}>
      {t(labelKey)}
    </div>
  )

  const renderGroupDivider = () => (
    <div aria-hidden style={{ height: 1, background: token.colorSplit, margin: '12px 8px 4px' }} />
  )

  return (
    <Sider
      width={248}
      theme={themePresets[themeKey].mode}
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
      <div style={{ padding: '14px 12px 8px', display: 'flex', alignItems: 'center', gap: 10 }}>
        <AppLogo size={30} />
        <div style={{ minWidth: 0, flex: 1 }}>
          <Typography.Text strong ellipsis style={{ display: 'block', fontSize: 15, lineHeight: 1.2 }}>
            {systemName}
          </Typography.Text>
          <Typography.Text type="secondary" ellipsis style={{ display: 'block', fontSize: 11 }}>
            {t('app.tagline')}
          </Typography.Text>
        </div>
      </div>

      <nav
        className="app-nav"
        aria-label={systemName}
        style={{
          '--app-nav-color': token.colorTextSecondary,
          '--app-nav-hover-bg': token.colorFillSecondary,
          '--app-nav-hover-color': token.colorText,
          '--app-nav-active-color': token.colorPrimary,
        } as React.CSSProperties}
      >
        {renderGroupLabel('nav.platform')}
        {mainNav.map(renderNavItem)}
        {isAdmin && (
          <>
            {renderGroupDivider()}
            {renderGroupLabel('nav.admin')}
            {visibleAdminNav.map(renderNavItem)}
          </>
        )}
      </nav>

      <div
        style={{
          padding: '10px 12px 12px',
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
              gap: 9,
              padding: '5px 6px',
              borderRadius: 8,
              cursor: 'pointer',
            }}
          >
            <Avatar size={30} icon={<UserOutlined />} src={userInfo?.avatar ?? undefined}>
              {displayName?.charAt(0) ?? 'U'}
            </Avatar>
            <div style={{ minWidth: 0, flex: 1 }}>
              <Typography.Text strong ellipsis style={{ display: 'block', fontSize: 13 }}>
                {displayName}
              </Typography.Text>
              <Typography.Text
                type="secondary"
                ellipsis
                style={{ display: 'block', fontSize: 11 }}
              >
                {userInfo?.email ?? userInfo?.userName}
              </Typography.Text>
            </div>
            {/* 消息铃铛放在用户信息行内（用户名右侧），避免挤压下方主题/语言切换的宽度；阻止冒泡避免误开用户菜单 */}
            <Tooltip title={t('nav.messages')}>
              <Badge count={unreadCount} size="small" offset={[-2, 2]}>
                <Button
                  type="text"
                  size="small"
                  aria-label={t('nav.messages')}
                  icon={<BellOutlined style={{ fontSize: 14 }} />}
                  style={{ flexShrink: 0 }}
                  onClick={(e) => {
                    e.stopPropagation()
                    navigate('/account/messages')
                  }}
                />
              </Badge>
            </Tooltip>
            <DownOutlined aria-hidden style={{ fontSize: 10, color: token.colorTextQuaternary }} />
          </div>
        </Dropdown>
        <div style={{ display: 'flex', gap: 7 }}>
          <Select
            value={themeKey}
            options={themeOptions}
            onChange={(value) => setThemeKey(value as ThemeKey)}
            className="app-side-select"
            style={{ flex: 1, minWidth: 0 }}
            popupMatchSelectWidth={false}
          />
          <Select
            value={locale}
            options={localeOptions}
            onChange={(value: Locale) => setLocale(value)}
            className="app-side-select"
            suffixIcon={<TranslationOutlined />}
            style={{ flex: 1, minWidth: 0 }}
            popupMatchSelectWidth={false}
          />
        </div>
      </div>
      </div>
    </Sider>
  )
}
