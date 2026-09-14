import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Empty, Input, Space, Spin, Tag } from 'antd'
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons'
import { Graph } from '@antv/g6'
import {
  getKnowledgeGraphCanvas,
  getKnowledgeGraphNodeNeighbors,
  getKnowledgeGraphSchema,
  type KnowledgeGraphEntityTypeItem,
} from '@/api/knowledgeGraph'
import { neutralColors, spacing } from '@/design-system/theme'

const FALLBACK_COLORS = ['#5B8FF9', '#5AD8A6', '#F6BD16', '#E8684A', '#6DC8EC', '#9270CA', '#FF9D4D', '#269A99']
const EDGE_COLOR = 'rgba(16, 24, 40, 0.25)'
const CANVAS_HEIGHT = 520
const DEFAULT_LIMIT = 200

interface CanvasNode {
  id: string
  label: string
  entityTypeId: number
}

interface CanvasEdge {
  id: string
  source: string
  target: string
}

/** 图览画布：有界子图 + 类型过滤 + 关键字搜索 + 点选一跳展开（只读） */
export function KnowledgeGraphCanvas({ graphId }: { graphId: number }) {
  const { t } = useTranslation()
  const containerRef = useRef<HTMLDivElement | null>(null)
  const graphRef = useRef<Graph | null>(null)
  const nodesRef = useRef(new Map<string, CanvasNode>())
  const edgesRef = useRef(new Map<string, CanvasEdge>())
  const [loading, setLoading] = useState(true)
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [typeFilter, setTypeFilter] = useState<number | null>(null)
  const [keyword, setKeyword] = useState('')
  const [truncated, setTruncated] = useState(false)
  const [empty, setEmpty] = useState(false)

  const colorOf = useMemo(() => {
    const map = new Map<number, string>()
    entityTypes.forEach((x, index) => {
      const value = Number(x.entityTypeId)
      map.set(value, x.color || FALLBACK_COLORS[index % FALLBACK_COLORS.length])
    })
    return map
  }, [entityTypes])

  const colorFor = useCallback(
    (entityTypeId: number) => colorOf.get(entityTypeId) ?? FALLBACK_COLORS[0],
    [colorOf],
  )

  const syncGraph = useCallback(() => {
    const graph = graphRef.current
    if (!graph) return
    const nodes = [...nodesRef.current.values()]
    const edges = [...edgesRef.current.values()]
    setEmpty(nodes.length === 0)
    graph.setData({
      nodes: nodes.map((n) => ({ id: n.id, data: { label: n.label, entityTypeId: n.entityTypeId } })),
      edges: edges.map((e) => ({ id: e.id, source: e.source, target: e.target, data: {} })),
    })
    void graph.render()
  }, [])

  useEffect(() => {
    if (!containerRef.current) return
    const graph = new Graph({
      container: containerRef.current,
      autoFit: 'center',
      padding: 24,
      data: { nodes: [], edges: [] },
      node: {
        style: {
          size: 28,
          fill: (datum: { data?: { entityTypeId?: number } }) => colorFor(Number(datum.data?.entityTypeId ?? 0)),
          labelText: (datum: { data?: { label?: string } }) => String(datum.data?.label ?? ''),
          labelPlacement: 'bottom',
          labelBackground: true,
          labelMaxWidth: 120,
        },
        palette: undefined,
      },
      edge: {
        style: {
          stroke: EDGE_COLOR,
          endArrow: true,
          endArrowSize: 8,
        },
      },
      layout: { type: 'force', linkDistance: 120, preventOverlap: true, animated: false },
      behaviors: ['drag-canvas', 'zoom-canvas', 'drag-element'],
    })
    graphRef.current = graph
    return () => {
      graphRef.current = null
      void graph.destroy()
    }
  }, [colorFor])

  const loadSchema = useCallback(async () => {
    try {
      const schema = await getKnowledgeGraphSchema(graphId)
      setEntityTypes(schema.entityTypes ?? [])
    } catch {
      setEntityTypes([])
    }
  }, [graphId])

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    try {
      const res = await getKnowledgeGraphCanvas(graphId, {
        entityTypeId: typeFilter,
        keyword: keyword || undefined,
        limit: DEFAULT_LIMIT,
      })
      nodesRef.current.clear()
      edgesRef.current.clear()
      for (const n of res.nodes) {
        if (n.nodeId != null) {
          nodesRef.current.set(String(n.nodeId), {
            id: String(n.nodeId),
            label: n.name ?? '',
            entityTypeId: Number(n.entityTypeId ?? 0),
          })
        }
      }
      for (const e of res.edges) {
        if (e.edgeId != null) {
          edgesRef.current.set(String(e.edgeId), {
            id: String(e.edgeId),
            source: String(e.sourceNodeId ?? ''),
            target: String(e.targetNodeId ?? ''),
          })
        }
      }
      setTruncated(res.truncated)
      syncGraph()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [graphId, typeFilter, keyword, syncGraph])

  useEffect(() => { void loadSchema() }, [loadSchema])
  useEffect(() => { void load() }, [load])

  const handleExpand = useCallback(async (nodeId: string) => {
    try {
      const res = await getKnowledgeGraphNodeNeighbors(graphId, nodeId, 100)
      for (const n of res.nodes) {
        const key = String(n.nodeId)
        if (n.nodeId != null && !nodesRef.current.has(key)) {
          nodesRef.current.set(key, { id: key, label: n.name ?? '', entityTypeId: Number(n.entityTypeId ?? 0) })
        }
      }
      for (const e of res.edges) {
        const key = String(e.edgeId)
        if (e.edgeId != null && !edgesRef.current.has(key)) {
          edgesRef.current.set(key, { id: key, source: String(e.sourceNodeId ?? ''), target: String(e.targetNodeId ?? '') })
        }
      }
      if (res.truncated) setTruncated(true)
      syncGraph()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [graphId, syncGraph])

  // 点选节点即展开一跳邻接
  useEffect(() => {
    const graph = graphRef.current
    if (!graph) return
    const onClick = (event: { target?: { id?: string } }) => {
      const nodeType = (event.target as { type?: string } | undefined)?.type
      const id = event.target?.id
      if (nodeType === 'node' && typeof id === 'string') void handleExpand(id)
    }
    graph.on('click', onClick as never)
    return () => {
      graph.off('click', onClick as never)
    }
  }, [handleExpand])

  return (
    <div>
      <Space style={{ marginBottom: spacing.md }} wrap>
        {entityTypes.map((x) => {
          const value = Number(x.entityTypeId)
          const active = typeFilter === value
          return (
            <Tag
              key={String(x.entityTypeId)}
              style={{ cursor: 'pointer' }}
              color={active ? 'blue' : 'default'}
              onClick={() => setTypeFilter(active ? null : value)}
            >
              <span
                aria-hidden
                style={{
                  display: 'inline-block',
                  width: 8,
                  height: 8,
                  borderRadius: 4,
                  marginRight: 6,
                  background: colorFor(value),
                }}
              />
              {x.name}
            </Tag>
          )
        })}
        <Input.Search
          allowClear
          placeholder={t('knowledgegraph.searchPlaceholder')}
          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
          onSearch={(value) => setKeyword(value)}
          style={{ width: 220 }}
        />
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          {t('knowledgegraph.canvasReload')}
        </Button>
      </Space>

      {truncated && (
        <Alert type="warning" showIcon message={t('knowledgegraph.canvasTruncated')} style={{ marginBottom: spacing.md }} />
      )}

      <Spin spinning={loading}>
        <div
          ref={containerRef}
          style={{
            height: CANVAS_HEIGHT,
            border: `1px solid ${neutralColors.border ?? 'rgba(16, 24, 40, 0.08)'}`,
            borderRadius: 8,
            background: 'transparent',
          }}
        />
      </Spin>

      {empty && !loading && (
        <Empty description={t('knowledgegraph.canvasEmpty')} style={{ marginTop: -CANVAS_HEIGHT / 2 - 16 }} />
      )}
    </div>
  )
}
