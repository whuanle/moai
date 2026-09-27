import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Layout, Menu, Tag } from 'antd'
import type { MenuProps } from 'antd'
import { ApartmentOutlined, ApiOutlined, ClusterOutlined, ExperimentOutlined, ImportOutlined, SettingOutlined, ToolOutlined } from '@ant-design/icons'
import { Card, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getKnowledgeGraphDetail, type KnowledgeGraphDetail as GraphDetail } from '@/api/knowledgeGraph'
import { KnowledgeGraphCanvas } from './KnowledgeGraphCanvas'
import { KnowledgeGraphImportPage } from './KnowledgeGraphImportPage'
import { KnowledgeGraphMcpPage } from './KnowledgeGraphMcpPage'
import { KnowledgeGraphRecallTest } from './KnowledgeGraphRecallTest'
import { KnowledgeGraphMaintenance } from './KnowledgeGraphMaintenance'
import { KnowledgeGraphSchema } from './KnowledgeGraphSchema'
import { KnowledgeGraphSettings } from './KnowledgeGraphSettings'

const { Sider, Content } = Layout

const SECTION_KEYS = ['canvas', 'maintenance', 'import', 'recall', 'mcp', 'schema', 'settings'] as const
type SectionKey = (typeof SECTION_KEYS)[number]
// 接入图只读：没有维护页，模型页为只读内省
const CONNECTED_SECTIONS: SectionKey[] = ['canvas', 'schema', 'settings']
// 旧版独立菜单路径（模型/实例/关系）→ 统一重定向到维护页对应步骤，旧链接不 404
const LEGACY_STEP: Record<string, string> = { schema: 'schema', entities: 'entities', relations: 'relations' }

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
  // 旧链接（/schema、/entities、/relations 独立菜单）重定向到维护页对应步骤；接入图 schema 仍是合法菜单。
  // 用声明式 <Navigate> 在渲染期提交，避免 effect 时序导致旧链接停留在图览
  const legacyStep = graph !== null && !isConnected && rawSection ? LEGACY_STEP[rawSection] : undefined

  // 进入图谱默认处于图览；旧路径仅作重定向中转，落到图览
  const defaultSection: SectionKey = 'canvas'
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
            { key: 'maintenance', icon: <ToolOutlined />, label: t('knowledgegraph.menuMaintenance') },
            { key: 'import', icon: <ImportOutlined />, label: t('knowledgegraph.menuImport') },
            { key: 'recall', icon: <ExperimentOutlined />, label: t('knowledgegraph.menuRecall') },
            { key: 'mcp', icon: <ApiOutlined />, label: t('knowledgegraph.menuMcp') },
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
      ) : legacyStep ? (
        <Navigate to={`/team/${teamId}/kg/${graphId}/maintenance?step=${legacyStep}`} replace />
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
          <Content style={{ minWidth: 0 }}>
            {isConnected && (
              <Tag color="blue" style={{ marginBottom: spacing.md }}>{t('knowledgegraph.connectedBadge')}</Tag>
            )}
            {section === 'canvas' ? (
              // 图览：无头部步骤条与导入入口，画布占满剩余高度与全部宽度
              <KnowledgeGraphCanvas
                graphId={graphId}
                mode={graph.mode}
                myRole={graph?.myRole ?? null}
                graphEnabled={graph?.enabled !== false}
              />
            ) : section === 'maintenance' ? (
              <KnowledgeGraphMaintenance graph={graph} />
            ) : section === 'import' ? (
              <KnowledgeGraphImportPage graph={graph} onChanged={load} />
            ) : section === 'recall' ? (
              <KnowledgeGraphRecallTest kgId={graphId} teamId={teamId} />
            ) : section === 'mcp' ? (
              <KnowledgeGraphMcpPage kgId={graphId} />
            ) : section === 'schema' ? (
              // 接入图只读模型页（托管图旧路径已重定向，正常不会进入）
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
