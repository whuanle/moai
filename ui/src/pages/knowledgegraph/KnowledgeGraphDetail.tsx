import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Layout, Menu } from 'antd'
import type { MenuProps } from 'antd'
import { ApartmentOutlined, DeploymentUnitOutlined, ProfileOutlined, SettingOutlined } from '@ant-design/icons'
import { Card, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getKnowledgeGraphDetail, type KnowledgeGraphDetail as GraphDetail } from '@/api/knowledgeGraph'
import { KnowledgeGraphEntities } from './KnowledgeGraphEntities'
import { KnowledgeGraphRelations } from './KnowledgeGraphRelations'
import { KnowledgeGraphSchema } from './KnowledgeGraphSchema'
import { KnowledgeGraphSettings } from './KnowledgeGraphSettings'

const { Sider, Content } = Layout

const SECTION_KEYS = ['entities', 'relations', 'schema', 'settings'] as const
type SectionKey = (typeof SECTION_KEYS)[number]

export function KnowledgeGraphDetail() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; graphId: string; section?: string }>()
  const teamId = Number(params.teamId)
  const graphId = Number(params.graphId)
  const rawSection = params.section ?? 'entities'
  const section: SectionKey = SECTION_KEYS.includes(rawSection as SectionKey) ? (rawSection as SectionKey) : 'entities'
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

  const menuItems: Required<MenuProps>['items'] = useMemo(
    () => [
      { key: 'entities', icon: <ProfileOutlined />, label: t('knowledgegraph.menuEntities') },
      { key: 'relations', icon: <DeploymentUnitOutlined />, label: t('knowledgegraph.menuRelations') },
      { key: 'schema', icon: <ApartmentOutlined />, label: t('knowledgegraph.menuSchema') },
      { key: 'settings', icon: <SettingOutlined />, label: t('knowledgegraph.menuSettings') },
    ],
    [t],
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
          {section === 'entities' ? (
            <Card styles={{ body: { padding: spacing.lg } }}>
              <KnowledgeGraphEntities graphId={graphId} graphEnabled={graph?.enabled !== false} />
            </Card>
          ) : section === 'relations' ? (
            <Card styles={{ body: { padding: spacing.lg } }}>
              <KnowledgeGraphRelations graphId={graphId} />
            </Card>
          ) : section === 'schema' ? (
            <Card styles={{ body: { padding: spacing.lg } }}>
              <KnowledgeGraphSchema graphId={graphId} myRole={graph?.myRole ?? null} />
            </Card>
          ) : (
            <KnowledgeGraphSettings graph={graph} onChanged={load} />
          )}
        </Content>
      </Layout>
    </Page>
  )
}
