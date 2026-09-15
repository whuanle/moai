import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Layout, Menu, Steps, Tag } from 'antd'
import type { MenuProps } from 'antd'
import { ApartmentOutlined, ClusterOutlined, DeploymentUnitOutlined, ProfileOutlined, SettingOutlined } from '@ant-design/icons'
import { Card, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getKnowledgeGraphDetail, type KnowledgeGraphDetail as GraphDetail } from '@/api/knowledgeGraph'
import { KnowledgeGraphCanvas } from './KnowledgeGraphCanvas'
import { KnowledgeGraphEntities } from './KnowledgeGraphEntities'
import { KnowledgeGraphRelations } from './KnowledgeGraphRelations'
import { KnowledgeGraphSchema } from './KnowledgeGraphSchema'
import { KnowledgeGraphSettings } from './KnowledgeGraphSettings'

const { Sider, Content } = Layout

const SECTION_KEYS = ['canvas', 'schema', 'entities', 'relations', 'settings'] as const
type SectionKey = (typeof SECTION_KEYS)[number]
const CONNECTED_SECTIONS: SectionKey[] = ['canvas', 'schema', 'settings']

export function KnowledgeGraphDetail() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; graphId: string; section?: string }>()
  const teamId = Number(params.teamId)
  const graphId = Number(params.graphId)
  const rawSection = params.section
  const [graph, setGraph] = useState<GraphDetail | null>(null)

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    try {
      setGraph(await getKnowledgeGraphDetail(graphId))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [graphId])

  useEffect(() => { void load() }, [load])

  const isConnected = graph?.mode === 'connected'
  const defaultSection: SectionKey = isConnected ? 'schema' : 'canvas'
  const section: SectionKey =
    rawSection &&
    SECTION_KEYS.includes(rawSection as SectionKey) &&
    (!isConnected || CONNECTED_SECTIONS.includes(rawSection as SectionKey))
      ? (rawSection as SectionKey)
      : defaultSection

  const menuItems: Required<MenuProps>['items'] = useMemo(
    () =>
      isConnected
        ? [
            { key: 'canvas', icon: <ClusterOutlined />, label: t('knowledgegraph.menuCanvas') },
            { key: 'schema', icon: <ApartmentOutlined />, label: t('knowledgegraph.menuSchema') },
            { key: 'settings', icon: <SettingOutlined />, label: t('knowledgegraph.menuSettings') },
          ]
        : [
            { key: 'canvas', icon: <ClusterOutlined />, label: t('knowledgegraph.menuCanvas') },
            { key: 'schema', icon: <ApartmentOutlined />, label: t('knowledgegraph.menuSchema') },
            { key: 'entities', icon: <ProfileOutlined />, label: t('knowledgegraph.menuEntities') },
            { key: 'relations', icon: <DeploymentUnitOutlined />, label: t('knowledgegraph.menuRelations') },
            { key: 'settings', icon: <SettingOutlined />, label: t('knowledgegraph.menuSettings') },
          ],
    [t, isConnected],
  )

  return (
    <Page
      breadcrumb={[
        { title: <Link to={`/team/${teamId}`}>{t('knowledgegraph.title')}</Link> },
        { title: graph?.name ?? '' },
      ]}
    >
      {graph && graph.enabled === false && (
        <Alert type="warning" showIcon message={t('knowledgegraph.disabled')} style={{ marginBottom: spacing.md }} />
      )}
      {graph === null ? (
        <Card loading styles={{ body: { padding: spacing.lg, minHeight: 160 } }} />
      ) : (
        <Layout style={{ background: 'transparent', gap: spacing.md }}>
          <Sider width={200} style={{ background: 'transparent' }}>
            <Menu
              mode="inline"
              items={menuItems}
              selectedKeys={[section]}
              onClick={({ key }) => navigate(`/team/${teamId}/kg/${graphId}/${key}`)}
              style={{ borderRadius: spacing.sm }}
            />
          </Sider>
          <Content>
            {isConnected && (
              <Tag color="blue" style={{ marginBottom: spacing.md }}>{t('knowledgegraph.connectedBadge')}</Tag>
            )}
            {/* 托管图四步使用引导：模型 → 实体 → 关系 → 图览，点步骤可直接跳转 */}
            {!isConnected && (
              <Steps
                size="small"
                type="navigation"
                style={{ marginBottom: spacing.md }}
                current={section === 'schema' ? 0 : section === 'entities' ? 1 : section === 'relations' ? 2 : 3}
                onChange={(current) => navigate(`/team/${teamId}/kg/${graphId}/${['schema', 'entities', 'relations', 'canvas'][current]}`)}
                items={[
                  { title: t('knowledgegraph.guide.step1') },
                  { title: t('knowledgegraph.guide.step2') },
                  { title: t('knowledgegraph.guide.step3') },
                  { title: t('knowledgegraph.guide.step4') },
                ]}
              />
            )}
            {section === 'canvas' ? (
              <Card styles={{ body: { padding: spacing.lg } }}>
                <KnowledgeGraphCanvas graphId={graphId} teamId={teamId} mode={graph.mode} myRole={graph?.myRole ?? null} graphEnabled={graph?.enabled !== false} />
              </Card>
            ) : section === 'entities' ? (
              <Card styles={{ body: { padding: spacing.lg } }}>
                <KnowledgeGraphEntities graphId={graphId} teamId={teamId} graphEnabled={graph?.enabled !== false} myRole={graph?.myRole ?? null} />
              </Card>
            ) : section === 'relations' ? (
              <Card styles={{ body: { padding: spacing.lg } }}>
                <KnowledgeGraphRelations graphId={graphId} teamId={teamId} graphEnabled={graph?.enabled !== false} myRole={graph?.myRole ?? null} />
              </Card>
            ) : section === 'schema' ? (
              <Card styles={{ body: { padding: spacing.lg } }}>
                <KnowledgeGraphSchema graphId={graphId} teamId={teamId} myRole={graph?.myRole ?? null} mode={graph.mode} />
              </Card>
            ) : (
              <KnowledgeGraphSettings graph={graph} onChanged={load} />
            )}
          </Content>
        </Layout>
      )}
    </Page>
  )
}
