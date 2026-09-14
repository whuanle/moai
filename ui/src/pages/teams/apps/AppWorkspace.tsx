import { useCallback, useEffect, useState } from 'react'
import { ApiOutlined, AreaChartOutlined, ProfileOutlined, SettingOutlined } from '@ant-design/icons'
import { Button, Layout, Menu, Popconfirm, Result, Space, Spin, Tag, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppDetail, publishApp, unpublishApp } from '@/api/app'
import { AppConfigSection, type AppDetail } from './AppConfigSection'
import { AppLogsSection } from './AppLogsSection'
import { AppMonitorSection } from './AppMonitorSection'

const { Sider, Content } = Layout
const { Text } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

const SECTIONS = ['config', 'logs', 'monitor', 'access'] as const
type SectionKey = (typeof SECTIONS)[number]

/**
 * 应用工作台外壳：左侧菜单承载配置/日志/监控（外部应用另有访问点），
 * 「配置」分区为左配置栏 + 右调试对话；日志与监控本阶段为占位。
 * 面包屑与发布/取消发布/进入对话等页级操作集中在此，配置分区只负责表单。
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
      } else {
        await publishApp(appId)
        feedback.success(t('appManage.publishSuccess'))
      }
      await load(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublishing(false)
    }
  }

  const menuItems: Required<MenuProps>['items'] = []
  menuItems.push({ key: 'config', icon: <SettingOutlined />, label: t('appWorkspace.menuConfig') })
  if (canManage) {
    menuItems.push(
      { key: 'logs', icon: <ProfileOutlined />, label: t('appWorkspace.menuLogs') },
      { key: 'monitor', icon: <AreaChartOutlined />, label: t('appWorkspace.menuMonitor') },
    )
  }
  if (canManage && isExternal) {
    menuItems.push({ key: 'access', icon: <ApiOutlined />, label: t('appWorkspace.menuAccess') })
  }
  const validSection = menuItems.some((i) => i?.key === section) ? section : 'config'

  const renderSection = () => {
    if (validSection === 'config') {
      return (
        <AppConfigSection
          teamId={teamId}
          appId={appId}
          detail={detail}
          loading={loading}
          canManage={canManage}
          onReload={() => load(true)}
        />
      )
    }
    if (validSection === 'logs') return <AppLogsSection appId={appId} />
    if (validSection === 'monitor') return <AppMonitorSection appId={appId} />
    return <Placeholder text={t('appWorkspace.accessComingSoon')} />
  }

  return (
    <Page
      breadcrumb={[
        { title: <Link to="/team">{t('team.title')}</Link> },
        { title: <Link to={`/team/${teamId}/apps`}>{t('team.apps')}</Link> },
        { title: detail?.name ?? '' },
      ]}
      extra={
        <Space>
          {isAgent &&
            (isPublished ? (
              <Tag color="green">{t('appManage.published')}</Tag>
            ) : (
              <Tag>{t('appManage.unpublished')}</Tag>
            ))}
          {isAgent && canManage && (
            <Popconfirm
              title={isPublished ? t('appManage.unpublishConfirm') : t('appManage.publishConfirm')}
              onConfirm={() => void togglePublish()}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
            >
              <Button type="primary" loading={publishing}>
                {isPublished ? t('appManage.unpublish') : t('appManage.publish')}
              </Button>
            </Popconfirm>
          )}
          {isAgent && isPublished && (
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
