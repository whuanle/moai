import { useCallback, useEffect, useState } from 'react'
import { ApiOutlined, CloudUploadOutlined, HistoryOutlined, InfoCircleOutlined, ProfileOutlined, SafetyCertificateOutlined, SettingOutlined, ShareAltOutlined } from '@ant-design/icons'
import { Button, Layout, Menu, Result, Space, Spin, Tag, Tooltip, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useShellStore } from '@/store/shell'
import { getAppDetail } from '@/api/app'
import { AppAccessSection } from './AppAccessSection'
import { AppAcpSection } from './AppAcpSection'
import { AppA2aSection } from './AppA2aSection'
import { AppChannelsSection } from './AppChannelsSection'
import { AppDingTalkChannelsSection } from './AppDingTalkChannelsSection'
import { AppWeixinWorkChannelsSection } from './AppWeixinWorkChannelsSection'
import { AppConfigSection, type AppDetail } from './AppConfigSection'
import { AppInfoSection } from './AppInfoSection'
import { AppLogsSection } from './AppLogsSection'
import { AppPublicationSection } from './AppPublicationSection'
import { AppSecuritySection } from './AppSecuritySection'
import { AppWorkflowDebugSection } from './AppWorkflowDebugSection'
import { AppWorkflowRunsSection } from './AppWorkflowRunsSection'
import { WorkflowAppHeader } from './workflow/WorkflowAppHeader'
import { WorkflowDesigner } from './workflow'

const { Sider, Content } = Layout
const { Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

const SECTIONS = ['config', 'info', 'publication', 'design', 'runs', 'logs', 'monitor', 'access', 'acp', 'a2a', 'channels', 'security', 'debug'] as const
type SectionKey = (typeof SECTIONS)[number]

/** 流程应用「配置」Tab 承载的二级分区（与内部应用的左侧菜单一致） */
const CONFIG_GROUP_SECTIONS = ['info', 'publication', 'security', 'logs', 'monitor', 'acp', 'a2a', 'channels'] as const

/**
 * 应用工作台外壳：
 * - 内部应用：左侧菜单承载 配置/信息/上架管理/日志/外部渠道（外部应用另有访问点），「配置」分区为左配置栏 + 右调试对话，
 *   安全（内容脱敏）与长期记忆并入「配置」分区收缩卡片；「日志」为 Tab 容器（日志/安全事件/使用分析，原「监控」分区并入、菜单移除）；
 * - 流程应用：头部 Tab（设计/调试/配置/运行历史），「调试」为会话式对话，「配置」下挂二级菜单（信息/上架管理/安全/日志/外部渠道，功能与内部应用一致）。
 * 面包屑与「进入对话/返回列表」页级操作在此；发布/重新发布与撤回发布统一收敛在「上架管理」分区的「团队应用」卡（发布弹窗内设置版本号）；
 * 外部应用同一分区收敛为「发布」（仅团队发布卡，无上架审批与申请记录，外部应用不公开到平台）。
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
  // 配置状态（0=草稿有未发布变更 1=一致）：由配置分区加载/保存/重新发布时上报，配置区警告条据此展示「重新发布」
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

  const menuItems: Required<MenuProps>['items'] = []
  if (isAgent) {
    menuItems.push(
      { key: 'config', icon: <SettingOutlined />, label: t('appWorkspace.menuConfig') },
      { key: 'info', icon: <InfoCircleOutlined />, label: t('appWorkspace.menuInfo') },
    )
    // 上架管理：内部应用单独成页（申请记录/拒绝原因/版本与草稿）；外部应用无上架审批，仅「发布」入口（发布弹窗带版本号）
    menuItems.push({
      key: 'publication',
      icon: <CloudUploadOutlined />,
      label: t(isExternal ? 'appWorkspace.menuPublish' : 'appWorkspace.menuPublication'),
    })
    if (canManage) {
      menuItems.push(
        // 安全（内容脱敏）与长期记忆不再单独成菜单：Agent 应用并入「配置」分区收缩卡片（流程应用仍保留安全二级页）
        { key: 'logs', icon: <ProfileOutlined />, label: t('appWorkspace.menuLogs') },
        // ACP：agent-to-agent 接入（Agent 与流程应用均支持）
        { key: 'acp', icon: <ApiOutlined />, label: t('appWorkspace.menuAcp') },
        // A2A：Google Agent2Agent 协议接入（Agent 与流程应用均支持）
        { key: 'a2a', icon: <ApiOutlined />, label: t('appWorkspace.menuA2a') },
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

  // 流程应用分区集合：设计 / 调试 / 配置（二级：信息·上架管理·安全·日志·ACP·外部渠道）/ 运行历史；外部应用另有访问点
  //（monitor 仍是合法 section：旧 /monitor 深链渲染日志分区并落到「使用分析」Tab，菜单不再单列）
  const workflowSectionKeys = new Set<string>(['debug', 'runs', 'info'])
  if (canManage) {
    workflowSectionKeys.add('design')
    workflowSectionKeys.add('security')
    workflowSectionKeys.add('logs')
    workflowSectionKeys.add('monitor')
    workflowSectionKeys.add('acp')
    workflowSectionKeys.add('a2a')
  }
  if (!isExternal) {
    workflowSectionKeys.add('channels')
    workflowSectionKeys.add('publication')
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

  // 「配置」分区左侧菜单：信息 / 上架管理 / 安全 / 日志 / ACP / 外部渠道（功能与内部应用一致，复用既有分区组件）
  const configGroupItems: MenuProps['items'] = [{ key: 'info', icon: <InfoCircleOutlined />, label: t('appWorkspace.menuInfo') }]
  if (!isExternal) {
    configGroupItems.push({ key: 'publication', icon: <CloudUploadOutlined />, label: t('appWorkspace.menuPublication') })
  }
  if (canManage) {
    configGroupItems.push(
      { key: 'security', icon: <SafetyCertificateOutlined />, label: t('appWorkspace.menuSecurity') },
      { key: 'logs', icon: <ProfileOutlined />, label: t('appWorkspace.menuLogs') },
      { key: 'acp', icon: <ApiOutlined />, label: t('appWorkspace.menuAcp') },
      { key: 'a2a', icon: <ApiOutlined />, label: t('appWorkspace.menuA2a') },
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
          appId={appId}
          detail={detail}
          canManage={canManage}
          onReload={() => load(true)}
          onDeleted={() => navigate(isExternal ? `/team/${teamId}/externalApps` : `/team/${teamId}/apps`, { replace: true })}
        />
      )
    }
    if (validSection === 'publication') {
      return (
        <AppPublicationSection
          teamId={teamId}
          appId={appId}
          detail={detail}
          canManage={canManage}
          configStatus={configStatus}
          onConfigStatusChange={setConfigStatus}
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
    if (validSection === 'logs') return <AppLogsSection appId={appId} canManage={canManage} />
    // 旧 /monitor 深链：同一 Tab 容器落到「使用分析」
    if (validSection === 'monitor') return <AppLogsSection appId={appId} canManage={canManage} defaultTab="usage" />
    if (validSection === 'security') return <AppSecuritySection appId={appId} canManage={canManage} />
    if (validSection === 'acp') return <AppAcpSection appId={appId} />
    if (validSection === 'a2a') return <AppA2aSection appId={appId} />
    if (validSection === 'channels') {
      return (
        <Space direction="vertical" style={{ width: '100%' }}>
          <AppChannelsSection teamId={teamId} appId={appId} canManage={canManage} isPublished={isPublished} />
          <AppDingTalkChannelsSection teamId={teamId} appId={appId} canManage={canManage} isPublished={isPublished} />
          <AppWeixinWorkChannelsSection teamId={teamId} appId={appId} canManage={canManage} isPublished={isPublished} />
        </Space>
      )
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
          {/* 发布/重新发布入口收敛在「上架管理」分区的「团队应用」卡（版本号在发布弹窗中设置）；流程应用发布走编排设计器 */}
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
