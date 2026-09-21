import { useCallback, useEffect, useState } from 'react'
import { ApiOutlined, AreaChartOutlined, HistoryOutlined, InfoCircleOutlined, ProfileOutlined, SettingOutlined, ShareAltOutlined } from '@ant-design/icons'
import { Button, Layout, Menu, Popconfirm, Result, Space, Spin, Tag, Tooltip, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useShellStore } from '@/store/shell'
import { getAppDetail, publishApp, unpublishApp } from '@/api/app'
import { AppAccessSection } from './AppAccessSection'
import { AppChannelsSection } from './AppChannelsSection'
import { AppConfigSection, type AppDetail } from './AppConfigSection'
import { AppInfoSection } from './AppInfoSection'
import { AppLogsSection } from './AppLogsSection'
import { AppMonitorSection } from './AppMonitorSection'
import { AppWorkflowDebugSection } from './AppWorkflowDebugSection'
import { AppWorkflowRunsSection } from './AppWorkflowRunsSection'
import { WorkflowAppHeader } from './workflow/WorkflowAppHeader'
import { WorkflowDesigner } from './workflow'

const { Sider, Content } = Layout
const { Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

const SECTIONS = ['config', 'info', 'design', 'runs', 'logs', 'monitor', 'access', 'channels', 'debug'] as const
type SectionKey = (typeof SECTIONS)[number]

/** 流程应用「配置」Tab 承载的二级分区（与内部应用的左侧菜单一致） */
const CONFIG_GROUP_SECTIONS = ['info', 'logs', 'monitor', 'channels'] as const

/**
 * 应用工作台外壳：
 * - 内部应用：左侧菜单承载 配置/信息/日志/监控/外部渠道（外部应用另有访问点），「配置」分区为左配置栏 + 右调试对话；
 * - 流程应用：头部 Tab（设计/调试/配置/运行历史），「调试」为会话式对话，「配置」下挂二级菜单（信息/日志/监控/外部渠道，功能与内部应用一致）。
 * 面包屑与发布/取消发布/进入对话等页级操作集中在此，各分区只负责自身内容。
 */
export function AppWorkspace() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; appId: string; section?: string }>()
  const teamId = Number(params.teamId)
  const appId = params.appId ?? ''
  const rawSection = params.section ?? 'config'
  const section: SectionKey = (SECTIONS as readonly string[]).includes(rawSection) ? (rawSection as SectionKey) : 'config'

  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<AppDetail | null>(null)
  const [publishing, setPublishing] = useState(false)
  // 配置状态（0=草稿有未发布变更 1=一致）：由配置分区加载/保存/重新发布时上报，头部据此展示「重新发布」
  const [configStatus, setConfigStatus] = useState<number | null>(null)

  const canManage = (detail?.myRole ?? -1) > ROLE_MEMBER
  const isAgent = detail?.appType !== 'workflow'
  const isExternal = detail?.isExternal === true
  const isPublished = detail?.publishStatus === 1

  const load = useCallback(async (silent = false) => {
    if (!appId) return
    if (!silent) setLoading(true)
    try {
      setDetail((await getAppDetail(appId)) as unknown as AppDetail)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      if (!silent) setLoading(false)
    }
  }, [appId])

  useEffect(() => {
    void load()
  }, [load])

  const togglePublish = async () => {
    if (!appId) return
    setPublishing(true)
    try {
      if (isPublished) {
        await unpublishApp(appId)
        feedback.success(t('appManage.unpublishSuccess'))
        setConfigStatus(null)
      } else {
        await publishApp(appId)
        feedback.success(t('appManage.publishSuccess'))
        // 首次发布即把当前草稿写入快照，配置状态为「一致」
        setConfigStatus(1)
      }
      await load(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

  /** 重新发布：把当前草稿配置推入发布快照，线上对话立即生效；状态回「已发布一致」 */
  const handleRepublish = async () => {
    if (!appId) return
    setPublishing(true)
    try {
      await publishApp(appId)
      feedback.success(t('appManage.republishSuccess'))
      setConfigStatus(1)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

  const menuItems: Required<MenuProps>['items'] = []
  if (isAgent) {
    menuItems.push(
      { key: 'config', icon: <SettingOutlined />, label: t('appWorkspace.menuConfig') },
      { key: 'info', icon: <InfoCircleOutlined />, label: t('appWorkspace.menuInfo') },
    )
    if (canManage) {
      menuItems.push(
        { key: 'logs', icon: <ProfileOutlined />, label: t('appWorkspace.menuLogs') },
        { key: 'monitor', icon: <AreaChartOutlined />, label: t('appWorkspace.menuMonitor') },
      )
      // 外部渠道（飞书接入等）：仅内部应用（外部应用走访问点）
      if (!isExternal) {
        menuItems.push({ key: 'channels', icon: <ShareAltOutlined />, label: t('appWorkspace.menuChannels') })
      }
    }
    // 外部应用：访问点菜单项
    if (canManage && isExternal) {
      menuItems.push({ key: 'access', icon: <ApiOutlined />, label: t('appWorkspace.menuAccess') })
    }
  }

  // 流程应用分区集合：设计 / 调试 / 配置（二级：信息·日志·监控·外部渠道）/ 运行历史；外部应用另有访问点
  const workflowSectionKeys = new Set<string>(['debug', 'runs', 'info'])
  if (canManage) {
    workflowSectionKeys.add('design')
    workflowSectionKeys.add('logs')
    workflowSectionKeys.add('monitor')
  }
  if (!isExternal) {
    workflowSectionKeys.add('channels')
  }
  if (canManage && isExternal) {
    workflowSectionKeys.add('access')
  }

  const validSection = isAgent
    ? menuItems.some((i) => i?.key === section)
      ? section
      : 'config'
    : workflowSectionKeys.has(section)
      ? section
      : canManage
        ? 'design'
        : 'runs'

  // 流程应用「配置」Tab：key 固定 info，激活条件是任一二级分区
  const inConfigGroup = !isAgent && (CONFIG_GROUP_SECTIONS as readonly string[]).includes(validSection)

  // 「配置」分区左侧菜单：信息 / 日志 / 监控 / 外部渠道（功能与内部应用一致，复用既有分区组件）
  const configGroupItems: MenuProps['items'] = [{ key: 'info', icon: <InfoCircleOutlined />, label: t('appWorkspace.menuInfo') }]
  if (canManage) {
    configGroupItems.push(
      { key: 'logs', icon: <ProfileOutlined />, label: t('appWorkspace.menuLogs') },
      { key: 'monitor', icon: <AreaChartOutlined />, label: t('appWorkspace.menuMonitor') },
    )
  }
  if (!isExternal) {
    configGroupItems.push({ key: 'channels', icon: <ShareAltOutlined />, label: t('appWorkspace.menuChannels') })
  }

  const goToSection = (key: string) => navigate(`/team/${teamId}/app/${appId}/${key}`)

  // 流程设计分区默认走 /design 子路由：AppLayout 按 /design 后缀去掉全局内边距（画布真全屏）
  useEffect(() => {
    if (validSection === 'design' && section !== 'design') {
      navigate(`/team/${teamId}/app/${appId}/design`, { replace: true })
    }
  }, [validSection, section, teamId, appId, navigate])

  // 流程应用全屏外壳标记：调试/配置/运行历史分区与设计器同壳（Agent 应用同路径分区不受影响）
  const setShellFullscreen = useShellStore((s) => s.setFullscreen)
  const workflowShellActive = !isAgent && !!detail?.appId
  useEffect(() => {
    setShellFullscreen(workflowShellActive)
    return () => setShellFullscreen(false)
  }, [workflowShellActive, setShellFullscreen])

  // 流程设计分区：不渲染面包屑/页头（设计器自带 FastGPT 风格头部），画布近全屏
  if (validSection === 'design') {
    return (
      <>
        {loading ? (
          <div style={{ padding: spacing.xl, textAlign: 'center' }}>
            <Spin />
          </div>
        ) : detail?.appId ? (
          <WorkflowDesigner teamId={teamId} appId={appId} appName={detail?.name ?? undefined} canManage={canManage} />
        ) : (
          <Result status="404" title={t('appManage.notFound')} />
        )}
      </>
    )
  }

  const renderSection = () => {
    if (validSection === 'runs') {
      return <AppWorkflowRunsSection teamId={teamId} appId={appId} canManage={canManage} />
    }
    if (validSection === 'debug') {
      return <AppWorkflowDebugSection teamId={teamId} appId={appId} detail={detail} canManage={canManage} />
    }
    if (validSection === 'config') {
      return (
        <AppConfigSection
          teamId={teamId}
          appId={appId}
          detail={detail}
          loading={loading}
          canManage={canManage}
          configStatus={configStatus}
          onConfigStatusChange={setConfigStatus}
        />
      )
    }
    if (validSection === 'info') {
      return (
        <AppInfoSection
          teamId={teamId}
          appId={appId}
          detail={detail}
          canManage={canManage}
          onReload={() => load(true)}
        />
      )
    }
    if (validSection === 'access') {
      return (
        <AppAccessSection
          appId={appId}
          isExternal={detail?.isExternal ?? false}
          isAuth={detail?.isAuth ?? false}
          canManage={canManage}
          userRole={detail?.myRole ?? -1}
        />
      )
    }
    if (validSection === 'logs') return <AppLogsSection appId={appId} />
    if (validSection === 'monitor') return <AppMonitorSection appId={appId} />
    if (validSection === 'channels') {
      return <AppChannelsSection teamId={teamId} appId={appId} canManage={canManage} isPublished={isPublished} />
    }
    return <Placeholder text={t('appWorkspace.accessComingSoon')} />
  }

  // 流程应用非 design 分区（design 已在上方提前返回）：与设计器共用同一全屏外壳
  //（返回/名称/状态 + 设计·调试·配置 Tab + 右侧操作），「配置」二级菜单挂在外壳头部之下，
  // 内容区滚动——三个视图布局一致，不再回到面包屑工作台页
  if (workflowShellActive) {
    const headerActiveKey = validSection === 'debug' ? 'debug' : inConfigGroup ? 'info' : ''
    return (
      <div className="wf-designer">
        <WorkflowAppHeader
          teamId={teamId}
          appId={appId}
          appName={detail?.name ?? undefined}
          activeKey={headerActiveKey}
          statusLine={
            <>
              <span className={`wf-status-dot ${isPublished ? 'wf-status-dot-published' : 'wf-status-dot-draft'}`} />
              {isPublished ? t('appManage.published') : t('appManage.unpublished')}
            </>
          }
          right={
            canManage && (
              <Tooltip title={t('workflowDesigner.runsTip')}>
                <Button icon={<HistoryOutlined />} onClick={() => navigate(`/team/${teamId}/app/${appId}/runs`)} />
              </Tooltip>
            )
          }
        />
        <div className="wf-app-body">
          {inConfigGroup && (
            <aside className="wf-app-menu">
              <Menu
                mode="inline"
                items={configGroupItems}
                selectedKeys={[validSection]}
                onClick={({ key }) => goToSection(key)}
              />
            </aside>
          )}
          <div className="wf-app-body-content">{renderSection()}</div>
        </div>
      </div>
    )
  }

  return (
    <Page
      breadcrumb={[
        { title: <Link to="/team">{t('team.title')}</Link> },
        {
          title: (
            <Link to={isExternal ? `/team/${teamId}/externalApps` : `/team/${teamId}/apps`}>
              {t(isExternal ? 'team.externalApps' : 'team.apps')}
            </Link>
          ),
        },
        { title: detail?.name ?? '' },
      ]}
      extra={
        <Space>
          {isPublished ? (
            <Tag color="green">{t('appManage.published')}</Tag>
          ) : (
            <Tag>{t('appManage.unpublished')}</Tag>
          )}
          {isAgent && canManage && isPublished && configStatus === 0 && (
            <Popconfirm
              title={t('appManage.republishConfirm')}
              onConfirm={() => void handleRepublish()}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
            >
              <Button type="primary" loading={publishing}>
                {t('appManage.republish')}
              </Button>
            </Popconfirm>
          )}
          {isAgent && canManage && (
            <Popconfirm
              title={isPublished ? t('appManage.unpublishConfirm') : t('appManage.publishConfirm')}
              onConfirm={() => void togglePublish()}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
            >
              {/* 已发布且有草稿变更时主操作是「重新发布」，取消发布降为次按钮 */}
              <Button type={isPublished && configStatus === 0 ? 'default' : 'primary'} loading={publishing}>
                {isPublished ? t('appManage.unpublish') : t('appManage.publish')}
              </Button>
            </Popconfirm>
          )}
          {/* Agent 与流程应用发布后均可进入对话；流程应用仅发布走编排设计器 */}
          {isPublished && (
            <Button onClick={() => navigate(`/team/${teamId}/app/${appId}/chat`)}>{t('appManage.enterChat')}</Button>
          )}
          <Button onClick={() => navigate(`/team/${teamId}/apps`)}>{t('appManage.backToList')}</Button>
        </Space>
      }
    >
      {loading ? (
        <div style={{ padding: spacing.xl, textAlign: 'center' }}>
          <Spin />
        </div>
      ) : detail?.appId ? (
        <Layout style={{ background: 'transparent', gap: spacing.md }}>
          <Sider width={200} style={{ background: 'transparent' }}>
              <Menu
                mode="inline"
                items={menuItems}
                selectedKeys={[validSection]}
                onClick={({ key }) => navigate(key === 'config' ? `/team/${teamId}/app/${appId}` : `/team/${teamId}/app/${appId}/${key}`)}
                style={{ borderRadius: spacing.sm }}
              />
            </Sider>
          <Content>{renderSection()}</Content>
        </Layout>
      ) : (
        <Result status="404" title={t('appManage.notFound')} />
      )}
    </Page>
  )
}

function Placeholder({ text }: { text: string }) {
  return (
    <div style={{ padding: spacing.xl, textAlign: 'center' }}>
      <Text type="secondary">{text}</Text>
    </div>
  )
}
