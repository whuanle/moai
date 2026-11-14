import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router'
import { Alert, Dropdown, Empty, Form, Input, Modal, Select, Spin, Typography, theme, type MenuProps } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { chartColors, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { PropertyInputs, serializePropertyValues, toPropertyDefs } from './PropertyFields'
import {
  createKnowledgeGraphEdge,
  createKnowledgeGraphNode,
  deleteKnowledgeGraphEdge,
  deleteKnowledgeGraphNode,
  getKnowledgeGraphCanvas,
  getKnowledgeGraphNodeNeighbors,
  getKnowledgeGraphSchema,
  text2CypherQuery,
  type KnowledgeGraphEntityTypeItem,
  type KnowledgeGraphNodeItem,
  type KnowledgeGraphRelationTypeItem,
  type KnowledgeGraphText2CypherResult,
} from '@/api/knowledgeGraph'
import { GraphCanvas, type ContextMenuTarget, type GraphCanvasHandle } from './canvas/GraphCanvas'
import { CanvasToolbar } from './canvas/CanvasToolbar'
import { LegendPanel, type LegendTypeItem } from './canvas/LegendPanel'
import { DetailSidebar, type RelatedEdgeItem } from './canvas/DetailSidebar'
import { MergeNodesModal } from './canvas/MergeNodesModal'
import { computeLayout, nodeRadius, type CanvasEdge, type CanvasNode, type LayoutKind } from './canvas/types'

/** 画布最小高度：小屏/极端布局下保底可用 */
const MIN_CANVAS_HEIGHT = 320
/** 页面底部留白：画布撑满视口时预留 */
const PAGE_BOTTOM_PADDING = 24
const DEFAULT_LIMIT = 200
/** 双击展开一次拉取的邻居上限 */
const EXPAND_LIMIT = 100
/** URL highlight/merge 参数最多补拉邻居的节点数 */
const URL_PARAM_FETCH_LIMIT = 10

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举）；画布写操作仅限 Admin+ 的托管图 */
const ROLE_MEMBER = 0

interface NodeFormValues {
  entityTypeId: number
  name: string
  description?: string
  props?: Record<string, unknown>
}

interface EdgeFormValues {
  relationTypeId: number
}

interface PendingEdge {
  source: string
  target: string
}

function toCanvasNode(n: KnowledgeGraphNodeItem): CanvasNode | null {
  if (n.nodeId == null) return null
  return {
    id: String(n.nodeId),
    label: n.name ?? '',
    typeKey: n.entityLabel != null ? String(n.entityLabel) : String(n.entityTypeId ?? 0),
    entityTypeId: n.entityTypeId != null ? Number(n.entityTypeId) : undefined,
    description: n.description ?? undefined,
    degree: n.degree != null ? Number(n.degree) : undefined,
    properties: n.properties ?? undefined,
  }
}

function toCanvasEdge(e: { edgeId?: string | null; sourceNodeId?: string | null; targetNodeId?: string | null; relationTypeId?: string | number | null; relationName?: string | null }): CanvasEdge | null {
  if (e.edgeId == null) return null
  return {
    id: String(e.edgeId),
    source: String(e.sourceNodeId ?? ''),
    target: String(e.targetNodeId ?? ''),
    relationTypeId: e.relationTypeId != null ? Number(e.relationTypeId) : undefined,
    relationName: e.relationName ?? undefined,
  }
}

/**
 * 图览画布（G6 v5）：搜索定位/布局切换（力导向·环形·网格）/深度与上限/缩放适配全屏/图例多选过滤/
 * 双击展开收起/详情侧栏（关联边跳转）/实体合并（多选 → 合并对话框）；托管图 Admin+ 保留画布编辑
 * （右键新建实体、多选两节点连关系、右键删除）。召回测试可经 ?highlight= 联动高亮，?merge= 预选合并。
 */
export function KnowledgeGraphCanvas({ graphId, mode, myRole = null, graphEnabled = true, canvasStyle = null, teamId = null }: {
  graphId: number
  mode?: string | null
  myRole?: number | null
  graphEnabled?: boolean
  /** 画布样式规则（B8）：实体类型 id → 属性名 / "__type__" / ""（节点第二行 caption） */
  canvasStyle?: Record<string, string> | null
  /** 团队 id（编辑实体跳维护页用） */
  teamId?: number | null
}) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const themeKey = useAppStore((s) => s.themeKey)
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()

  const wrapperRef = useRef<HTMLDivElement | null>(null)
  const canvasShellRef = useRef<HTMLDivElement | null>(null)
  const graphRef = useRef<GraphCanvasHandle | null>(null)
  const nodesRef = useRef(new Map<string, CanvasNode>())
  const edgesRef = useRef(new Map<string, CanvasEdge>())
  /** 节点布局坐标缓存：布局初值 + 拖动位置回写，展开邻接时视觉连续 */
  const positionsRef = useRef(new Map<string, { x: number; y: number }>())
  /** 已展开节点 → 本次展开引入的节点 id 集合（收起时回收） */
  const expandedByRef = useRef(new Map<string, Set<string>>())
  /** 多选（有序）：连关系起点→终点顺序、合并集合 */
  const [selection, setSelection] = useState<string[]>([])
  const selectionRef = useRef<string[]>([])
  /** 高亮节点（搜索定位/召回联动），优先于选中渲染 */
  const highlightRef = useRef<Set<string>>(new Set())

  const [loading, setLoading] = useState(true)
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [relationTypes, setRelationTypes] = useState<KnowledgeGraphRelationTypeItem[]>([])
  const [hiddenTypes, setHiddenTypes] = useState<Set<string>>(new Set())
  const [keyword, setKeyword] = useState<string | null>(null)
  const [layout, setLayout] = useState<LayoutKind>('force')
  const [limit, setLimit] = useState(DEFAULT_LIMIT)
  const [depth, setDepth] = useState<1 | 2>(1)
  const [truncated, setTruncated] = useState(false)
  const [empty, setEmpty] = useState(false)
  const [canvasHeight, setCanvasHeight] = useState(MIN_CANVAS_HEIGHT)
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [showEdgeLabels, setShowEdgeLabels] = useState(true)
  const [detailNodeId, setDetailNodeId] = useState<string | null>(null)
  const [detailEdgeId, setDetailEdgeId] = useState<string | null>(null)
  const [mergeOpen, setMergeOpen] = useState(false)
  const [ctxMenu, setCtxMenu] = useState<(ContextMenuTarget & { open: boolean }) | null>(null)
  /** 问图模式与结果（B7 Text2Cypher） */
  const [searchMode, setSearchMode] = useState<'entity' | 'nl'>('entity')
  const [asking, setAsking] = useState(false)
  const [askResult, setAskResult] = useState<KnowledgeGraphText2CypherResult | null>(null)

  const isConnected = mode === 'connected'
  const canEdit = !isConnected && graphEnabled && myRole !== null && myRole !== ROLE_MEMBER

  // ===== 新建实体/关系弹窗（沿用画布编辑） =====
  const [nodeOpen, setNodeOpen] = useState(false)
  const [nodeSaving, setNodeSaving] = useState(false)
  const [edgeOpen, setEdgeOpen] = useState(false)
  const [edgeSaving, setEdgeSaving] = useState(false)
  const [pendingEdge, setPendingEdge] = useState<PendingEdge | null>(null)
  const [nodeForm] = Form.useForm<NodeFormValues>()
  const [edgeForm] = Form.useForm<EdgeFormValues>()

  const colorOf = useMemo(() => {
    const map = new Map<string, string>()
    entityTypes.forEach((x, index) => {
      const value = x.entityTypeId != null ? String(x.entityTypeId) : (x.name ?? '')
      map.set(value, x.color || chartColors[index % chartColors.length])
    })
    return map
  }, [entityTypes])

  const colorFor = useCallback((typeKey: string) => colorOf.get(typeKey) ?? chartColors[0], [colorOf])

  const typeInfo = useMemo(() => {
    const map = new Map<number, KnowledgeGraphEntityTypeItem>()
    entityTypes.forEach((x) => {
      if (x.entityTypeId != null) map.set(Number(x.entityTypeId), x)
    })
    return map
  }, [entityTypes])

  const relationInfo = useMemo(() => {
    const map = new Map<number, KnowledgeGraphRelationTypeItem>()
    relationTypes.forEach((x) => {
      if (x.relationTypeId != null) map.set(Number(x.relationTypeId), x)
    })
    return map
  }, [relationTypes])

  const typeNameOf = useCallback(
    (node: CanvasNode) => {
      if (node.entityTypeId != null) return typeInfo.get(node.entityTypeId)?.name ?? ''
      return isConnected ? node.typeKey : ''
    },
    [typeInfo, isConnected],
  )

  const edgeLabelOf = useCallback(
    (edge: CanvasEdge) => {
      if (edge.relationName) return edge.relationName
      if (edge.relationTypeId != null) return relationInfo.get(edge.relationTypeId)?.name ?? ''
      return ''
    },
    [relationInfo],
  )

  // ===== 样式规则（B8）：节点 label 追加 caption（属性值/类型名），单行「名 · caption」 =====
  const captionOf = useCallback(
    (node: CanvasNode) => {
      const rule = canvasStyle?.[node.entityTypeId != null ? String(node.entityTypeId) : '']
      if (!rule) return node.label
      const value = rule === '__type__' ? typeNameOf(node) : node.properties?.[rule] ?? ''
      return value ? `${node.label} · ${value}` : node.label
    },
    [canvasStyle, typeNameOf],
  )

  // ===== 渲染数据 → G6（过滤隐藏类型 + 布局 + 状态回放） =====
  // 渲染函数经 ref 被 load 引用：布局/图例/边标签等纯视觉变化只重渲染不重新请求（否则图库不可用时切换布局会刷 503）
  const renderDataRef = useRef<(options?: { fit?: boolean }) => void>(() => {})
  const renderData = useCallback((options?: { fit?: boolean }) => {
    const graph = graphRef.current
    const all = [...nodesRef.current.values()]
    const visibleNodes = all.filter((n) => !hiddenTypes.has(n.typeKey))
    const visibleIds = new Set(visibleNodes.map((n) => n.id))
    const visibleEdges = [...edgesRef.current.values()].filter((e) => visibleIds.has(e.source) && visibleIds.has(e.target))
    setEmpty(all.length === 0)

    // 拖动后的真实坐标优先（连续性），再算布局
    const dragged = graph?.getPositions() ?? new Map<string, { x: number; y: number }>()
    for (const [id, pos] of dragged) positionsRef.current.set(id, pos)
    const positions = computeLayout(layout, visibleNodes, visibleEdges, positionsRef.current)
    positionsRef.current = positions

    // fit 随数据传入：渲染完成后自适应视口（G6 实例未就绪时由画布层挂起补做）
    graph?.setData(
      visibleNodes.map((n) => ({
        id: n.id,
        x: positions.get(n.id)?.x ?? 0,
        y: positions.get(n.id)?.y ?? 0,
        fill: colorFor(n.typeKey),
        r: nodeRadius(n.degree),
        labelText: captionOf(n),
      })),
      visibleEdges.map((e) => ({
        id: e.id,
        source: e.source,
        target: e.target,
        stroke: token.colorBorder,
        labelText: showEdgeLabels ? edgeLabelOf(e) || undefined : undefined,
      })),
      { fit: options?.fit },
    )

    // 状态回放：高亮 > 选中 > 已展开
    for (const id of selectionRef.current) graph?.setElementState(id, { selected: true })
    for (const id of highlightRef.current) graph?.setElementState(id, { highlight: true })
    for (const id of expandedByRef.current.keys()) graph?.setElementState(id, { expanded: true })
  }, [hiddenTypes, layout, colorFor, edgeLabelOf, captionOf, showEdgeLabels, token.colorBorder])
  renderDataRef.current = renderData

  const mergeSubgraph = useCallback((nodes: CanvasNode[], edges: CanvasEdge[], originId?: string) => {
    const addedIds = new Set<string>()
    for (const n of nodes) {
      if (!nodesRef.current.has(n.id)) {
        nodesRef.current.set(n.id, n)
        addedIds.add(n.id)
      } else if (n.degree != null) {
        // 已在画布上的节点顺带刷新度数
        const existing = nodesRef.current.get(n.id)
        if (existing) nodesRef.current.set(n.id, { ...existing, degree: n.degree })
      }
    }
    for (const e of edges) {
      if (!edgesRef.current.has(e.id)) edgesRef.current.set(e.id, e)
    }
    if (originId != null && addedIds.size > 0) {
      const set = expandedByRef.current.get(originId) ?? new Set<string>()
      for (const id of addedIds) set.add(id)
      expandedByRef.current.set(originId, set)
    }
    return addedIds
  }, [])

  const load = useCallback(async (options?: { fit?: boolean; keyword?: string | null }) => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    try {
      const res = await getKnowledgeGraphCanvas(graphId, {
        keyword: options?.keyword ?? undefined,
        limit,
        depth,
      })
      nodesRef.current.clear()
      edgesRef.current.clear()
      expandedByRef.current.clear()
      positionsRef.current.clear()
      for (const n of res.nodes) {
        const node = toCanvasNode(n)
        if (node) nodesRef.current.set(node.id, node)
      }
      for (const e of res.edges) {
        const edge = toCanvasEdge(e)
        if (edge) edgesRef.current.set(edge.id, edge)
      }
      setTruncated(res.truncated)
      renderDataRef.current({ fit: options?.fit ?? true })
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [graphId, limit, depth])

  const loadSchema = useCallback(async () => {
    try {
      const schema = await getKnowledgeGraphSchema(graphId)
      setEntityTypes(schema.entityTypes ?? [])
      setRelationTypes(schema.relationTypes ?? [])
    } catch {
      setEntityTypes([])
      setRelationTypes([])
    }
  }, [graphId])

  useEffect(() => { void loadSchema() }, [loadSchema])
  useEffect(() => { void load({ fit: true }) }, [load])

  // ===== 展开 / 收起（双击） =====
  const expandNode = useCallback(async (nodeId: string) => {
    try {
      const res = await getKnowledgeGraphNodeNeighbors(graphId, nodeId, EXPAND_LIMIT)
      const newNodes: CanvasNode[] = []
      for (const n of res.nodes) {
        const node = toCanvasNode(n)
        if (node) newNodes.push(node)
      }
      const newEdges: CanvasEdge[] = []
      for (const e of res.edges) {
        const edge = toCanvasEdge(e)
        if (edge) newEdges.push(edge)
      }
      mergeSubgraph(newNodes, newEdges, nodeId)
      if (res.truncated) setTruncated(true)
      renderData()
      setDetailNodeId(nodeId)
      setDetailEdgeId(null)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [graphId, mergeSubgraph, renderData])

  const collapseNode = useCallback((nodeId: string) => {
    const added = expandedByRef.current.get(nodeId)
    expandedByRef.current.delete(nodeId)
    if (added && added.size > 0) {
      // 回收仅因本次展开而引入、且无其它连边、未被选中/高亮/自身已展开的节点
      const removable = new Set<string>()
      for (const id of added) {
        if (selectionRef.current.includes(id) || highlightRef.current.has(id) || expandedByRef.current.has(id)) continue
        const hasOtherEdge = [...edgesRef.current.values()].some((e) =>
          (e.source === id || e.target === id) && e.source !== nodeId && e.target !== nodeId)
        if (!hasOtherEdge) removable.add(id)
      }
      for (const id of removable) nodesRef.current.delete(id)
      for (const [id, e] of edgesRef.current) {
        if (removable.has(e.source) || removable.has(e.target)) edgesRef.current.delete(id)
      }
    }
    renderData()
    if (detailNodeId != null && !nodesRef.current.has(detailNodeId)) setDetailNodeId(null)
  }, [renderData, detailNodeId])

  const handleNodeDoubleClick = useCallback((nodeId: string) => {
    if (expandedByRef.current.has(nodeId)) collapseNode(nodeId)
    else void expandNode(nodeId)
  }, [collapseNode, expandNode])

  // ===== 点选 / 多选 =====
  const applySelection = useCallback((next: string[]) => {
    const graph = graphRef.current
    for (const id of selectionRef.current) graph?.setElementState(id, {})
    selectionRef.current = next
    setSelection(next)
    for (const id of next) graph?.setElementState(id, { selected: true })
  }, [])

  const handleNodeClick = useCallback((nodeId: string, ctrlKey: boolean) => {
    if (ctrlKey) {
      const current = selectionRef.current
      applySelection(current.includes(nodeId) ? current.filter((x) => x !== nodeId) : [...current, nodeId])
    } else {
      applySelection([nodeId])
    }
    setDetailNodeId(nodeId)
    setDetailEdgeId(null)
  }, [applySelection])

  const handleEdgeClick = useCallback((edgeId: string) => {
    setDetailEdgeId(edgeId)
    setDetailNodeId(null)
  }, [])

  const handleCanvasClick = useCallback(() => {
    applySelection([])
  }, [applySelection])

  // ===== 搜索定位：画布内命中高亮居中；未命中提示服务端重载 =====
  const handleLocate = useCallback((term: string) => {
    if (searchMode === 'nl') {
      // 自然语言问图：生成 Cypher → 结果卡展示 → nodeIds 高亮画布
      setAsking(true)
      setAskResult(null)
      text2CypherQuery(graphId, { question: term })
        .then((res) => {
          setAskResult(res)
          if (res.ok && (res.nodeIds?.length ?? 0) > 0) {
            const ids = res.nodeIds!.slice(0, 20)
            const graph = graphRef.current
            for (const id of highlightRef.current) graph?.setElementState(id, {})
            highlightRef.current = new Set(ids.filter((id) => nodesRef.current.has(id)))
            for (const id of highlightRef.current) graph?.setElementState(id, { highlight: true })
            const firstVisible = ids.find((id) => nodesRef.current.has(id))
            if (firstVisible) graph?.focusNode(firstVisible)
          }
        })
        .catch(() => {
          // 错误已由全局请求中间件统一提示
        })
        .finally(() => setAsking(false))
      return
    }
    const lower = term.toLowerCase()
    const hits = [...nodesRef.current.values()].filter((n) => n.label.toLowerCase().includes(lower))
    const graph = graphRef.current
    for (const id of highlightRef.current) graph?.setElementState(id, {})
    if (hits.length === 0) {
      highlightRef.current = new Set()
      Modal.confirm({
        title: t('knowledgegraph.canvas.locateMissTitle'),
        content: t('knowledgegraph.canvas.locateMissDesc', { term }),
        okText: t('knowledgegraph.canvas.locateServerSearch'),
        onOk: () => {
          setKeyword(term)
          void load({ fit: true, keyword: term })
        },
      })
      return
    }
    highlightRef.current = new Set(hits.slice(0, 20).map((n) => n.id))
    for (const id of highlightRef.current) graph?.setElementState(id, { highlight: true })
    graph?.focusNode(hits[0].id)
  }, [searchMode, graphId, load, t])

  // ===== URL 参数联动：召回测试高亮 / 导入页去合并 =====
  useEffect(() => {
    const highlightParam = searchParams.get('highlight')
    const mergeParam = searchParams.get('merge')
    if (!highlightParam && !mergeParam) return
    if (loading || nodesRef.current.size === 0 && !empty) return
    void (async () => {
      const ids = Array.from(new Set((mergeParam ?? highlightParam ?? '').split(',').map((x) => x.trim()).filter(Boolean)))
        .slice(0, URL_PARAM_FETCH_LIMIT)
      // 画布上缺失的节点经邻接接口补拉（受限数量）
      const missing = ids.filter((id) => !nodesRef.current.has(id))
      for (const id of missing) {
        try {
          const res = await getKnowledgeGraphNodeNeighbors(graphId, id, EXPAND_LIMIT)
          mergeSubgraph(
            res.nodes.map((n) => toCanvasNode(n)).filter((n): n is CanvasNode => n != null),
            res.edges.map((e) => toCanvasEdge(e)).filter((e): e is CanvasEdge => e != null),
          )
        } catch {
          // 单个节点补拉失败不阻断整体联动
        }
      }
      renderData()
      if (mergeParam) {
        applySelection(ids)
        setMergeOpen(true)
        setSearchParams((prev) => {
          const next = new URLSearchParams(prev)
          next.delete('merge')
          return next
        }, { replace: true })
      } else if (highlightParam) {
        highlightRef.current = new Set(ids)
        for (const id of ids) graphRef.current?.setElementState(id, { highlight: true })
        if (ids.length > 0) graphRef.current?.focusNode(ids[0])
        setSearchParams((prev) => {
          const next = new URLSearchParams(prev)
          next.delete('highlight')
          return next
        }, { replace: true })
      }
    })()
    // 仅在参数存在且首载完成时执行一次
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams, loading, empty])

  // ===== 画布高度自适应 / 全屏 =====
  useEffect(() => {
    const update = () => {
      const shell = canvasShellRef.current
      if (!shell) return
      if (document.fullscreenElement === shell) {
        setCanvasHeight(window.innerHeight)
        return
      }
      const top = shell.getBoundingClientRect().top
      setCanvasHeight(Math.max(MIN_CANVAS_HEIGHT, window.innerHeight - Math.max(top, 0) - PAGE_BOTTOM_PADDING))
    }
    update()
    window.addEventListener('resize', update)
    document.addEventListener('fullscreenchange', update)
    const observer = new ResizeObserver(update)
    if (wrapperRef.current) observer.observe(wrapperRef.current)
    return () => {
      window.removeEventListener('resize', update)
      document.removeEventListener('fullscreenchange', update)
      observer.disconnect()
    }
  }, [])

  useEffect(() => {
    const onFullscreenChange = () => setIsFullscreen(document.fullscreenElement === canvasShellRef.current)
    document.addEventListener('fullscreenchange', onFullscreenChange)
    return () => document.removeEventListener('fullscreenchange', onFullscreenChange)
  }, [])

  const toggleFullscreen = useCallback(() => {
    const shell = canvasShellRef.current
    if (!shell) return
    if (document.fullscreenElement === shell) {
      void document.exitFullscreen?.()
    } else {
      shell.requestFullscreen?.().catch(() => {
        // 用户拒绝或环境不支持时静默降级
      })
    }
  }, [])

  // ===== 图例（类型多选显隐，内存过滤） =====
  const legendTypes: LegendTypeItem[] = useMemo(() => {
    const counts = new Map<string, number>()
    for (const n of nodesRef.current.values()) counts.set(n.typeKey, (counts.get(n.typeKey) ?? 0) + 1)
    return entityTypes
      .map((x) => {
        const key = x.entityTypeId != null ? String(x.entityTypeId) : (x.name ?? '')
        return { key, name: x.name ?? key, color: colorFor(key), count: counts.get(key) ?? 0 }
      })
      .filter((x) => x.count > 0 || entityTypes.length <= 8)
  }, [entityTypes, colorFor, loading, truncated, selection, renderData])

  const toggleTypeHidden = useCallback((key: string) => {
    setHiddenTypes((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }, [])

  useEffect(() => { renderData() }, [hiddenTypes, layout, showEdgeLabels, renderData])

  // ===== 画布编辑：新建实体 / 连关系 / 删除（沿用既有表单与约束校验） =====
  const openCreateNode = useCallback(() => {
    if (entityTypes.length === 0) {
      feedback.warning(t('knowledgegraph.canvasEdit.needType'))
      return
    }
    nodeForm.resetFields()
    setNodeOpen(true)
  }, [entityTypes, nodeForm, t])

  const submitCreateNode = async () => {
    const values = await nodeForm.validateFields()
    const defs = toPropertyDefs(typeInfo.get(Number(values.entityTypeId))?.properties)
    const properties = serializePropertyValues(values.props, defs)
    setNodeSaving(true)
    try {
      const nodeId = await createKnowledgeGraphNode(graphId, { ...values, properties })
      nodesRef.current.set(nodeId, {
        id: nodeId,
        label: values.name,
        typeKey: String(values.entityTypeId),
        entityTypeId: values.entityTypeId,
        description: values.description,
      })
      setNodeOpen(false)
      setEmpty(false)
      renderData()
      feedback.success(t('knowledgegraph.createSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setNodeSaving(false)
    }
  }

  const openCreateEdge = useCallback((source: string, target: string) => {
    if (source === target) return
    if (relationTypes.length === 0) {
      feedback.warning(t('knowledgegraph.canvasEdit.needRelationType'))
      return
    }
    setPendingEdge({ source, target })
    edgeForm.resetFields()
    setEdgeOpen(true)
  }, [relationTypes, edgeForm, t])

  const submitCreateEdge = async () => {
    if (!pendingEdge) return
    const values = await edgeForm.validateFields()
    setEdgeSaving(true)
    try {
      const edgeId = await createKnowledgeGraphEdge(graphId, {
        relationTypeId: values.relationTypeId,
        sourceNodeId: pendingEdge.source,
        targetNodeId: pendingEdge.target,
      })
      edgesRef.current.set(edgeId, { id: edgeId, source: pendingEdge.source, target: pendingEdge.target })
      setEdgeOpen(false)
      renderData()
      feedback.success(t('knowledgegraph.createSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示（如违反起止约束 400）
    } finally {
      setEdgeSaving(false)
    }
  }

  const confirmDeleteNode = useCallback((nodeId: string) => {
    const node = nodesRef.current.get(nodeId)
    Modal.confirm({
      title: t('knowledgegraph.canvasEdit.deleteNodeTitle'),
      content: node ? `${node.label}` : nodeId,
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteKnowledgeGraphNode(graphId, nodeId)
          nodesRef.current.delete(nodeId)
          for (const [id, e] of edgesRef.current) {
            if (e.source === nodeId || e.target === nodeId) edgesRef.current.delete(id)
          }
          applySelection(selectionRef.current.filter((x) => x !== nodeId))
          renderData()
          feedback.success(t('knowledgegraph.deleteSuccess'))
        } catch {
          // 错误已由全局请求中间件统一提示
        }
      },
    })
  }, [graphId, renderData, applySelection, t])

  const confirmDeleteEdge = useCallback((edgeId: string) => {
    Modal.confirm({
      title: t('knowledgegraph.canvasEdit.deleteEdgeTitle'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteKnowledgeGraphEdge(graphId, edgeId)
          edgesRef.current.delete(edgeId)
          renderData()
          feedback.success(t('knowledgegraph.deleteSuccess'))
        } catch {
          // 错误已由全局请求中间件统一提示
        }
      },
    })
  }, [graphId, renderData, t])

  // ===== 右键菜单 =====
  const contextMenuItems = useMemo<MenuProps['items']>(() => {
    if (!ctxMenu) return []
    if (ctxMenu.kind === 'canvas' && canEdit) {
      return [{ key: 'create', icon: <PlusOutlined />, label: t('knowledgegraph.canvasEdit.createNodeTitle') }]
    }
    if (ctxMenu.kind === 'node') {
      const items: NonNullable<MenuProps['items']> = [{
        key: 'toggle-expand',
        label: expandedByRef.current.has(ctxMenu.id ?? '')
          ? t('knowledgegraph.canvas.collapse')
          : t('knowledgegraph.canvas.expand'),
      }]
      if (canEdit) {
        items.push({ key: 'delete', danger: true, label: t('knowledgegraph.canvas.deleteNodeAction') })
      }
      return items
    }
    if (ctxMenu.kind === 'edge' && canEdit) {
      return [{ key: 'delete', danger: true, label: t('knowledgegraph.canvas.deleteEdgeAction') }]
    }
    return []
  }, [ctxMenu, canEdit, t])

  const handleContextMenuClick = useCallback(({ key }: { key: string }) => {
    const target = ctxMenu
    setCtxMenu(null)
    if (!target) return
    if (key === 'create') openCreateNode()
    else if (key === 'toggle-expand' && target.id) handleNodeDoubleClick(target.id)
    else if (key === 'delete' && target.kind === 'node' && target.id) confirmDeleteNode(target.id)
    else if (key === 'delete' && target.kind === 'edge' && target.id) confirmDeleteEdge(target.id)
  }, [ctxMenu, openCreateNode, handleNodeDoubleClick, confirmDeleteNode, confirmDeleteEdge])

  // ===== 详情侧栏数据 =====
  const detailNode = detailNodeId != null ? nodesRef.current.get(detailNodeId) ?? null : null
  const detailEdge = detailEdgeId != null ? edgesRef.current.get(detailEdgeId) ?? null : null
  const detailDefs = detailNode?.entityTypeId != null ? toPropertyDefs(typeInfo.get(detailNode.entityTypeId)?.properties) : []
  const detailProps: [string, string][] = (() => {
    const props = detailNode?.properties ?? {}
    const ordered: [string, string][] = []
    const seen = new Set<string>()
    for (const def of detailDefs) {
      const value = props[def.name]
      if (value) {
        ordered.push([def.name, value])
        seen.add(def.name)
      }
    }
    for (const [key, value] of Object.entries(props)) {
      if (!seen.has(key)) ordered.push([key, value])
    }
    return ordered
  })()
  const relatedEdges: RelatedEdgeItem[] = useMemo(() => {
    if (detailNode == null) return []
    return [...edgesRef.current.values()]
      .filter((e) => e.source === detailNode.id || e.target === detailNode.id)
      .map((e) => {
        const direction: 'out' | 'in' = e.source === detailNode.id ? 'out' : 'in'
        const otherId = direction === 'out' ? e.target : e.source
        return { edge: e, relationLabel: edgeLabelOf(e), otherLabel: nodesRef.current.get(otherId)?.label ?? otherId, direction, otherNodeId: otherId }
      })
      .slice(0, 100)
  }, [detailNode, detailNodeId, edgeLabelOf, renderData, selection, loading])

  const selectedEntityTypeId = Form.useWatch('entityTypeId', nodeForm)
  const selectedEntityDefs = selectedEntityTypeId != null
    ? toPropertyDefs(typeInfo.get(Number(selectedEntityTypeId))?.properties)
    : []

  const selectedRelationTypeId = Form.useWatch('relationTypeId', edgeForm)
  const constraint = relationInfo.get(Number(selectedRelationTypeId ?? NaN))
  const sourceNode = pendingEdge ? nodesRef.current.get(pendingEdge.source) : undefined
  const targetNode = pendingEdge ? nodesRef.current.get(pendingEdge.target) : undefined
  const constraintViolated = pendingEdge && constraint
    ? (constraint.sourceTypeId != null && sourceNode?.entityTypeId != null && Number(constraint.sourceTypeId) !== sourceNode.entityTypeId) ||
      (constraint.targetTypeId != null && targetNode?.entityTypeId != null && Number(constraint.targetTypeId) !== targetNode.entityTypeId)
    : false

  const mergeNodes = useMemo(
    () => selection.map((id) => nodesRef.current.get(id)).filter((n): n is CanvasNode => n != null),
    [selection, renderData],
  )

  return (
    <div ref={wrapperRef} onContextMenu={(e) => e.preventDefault()}>
      <CanvasToolbar
        searchMode={searchMode}
        onSearchModeChange={(mode) => {
          setSearchMode(mode)
          if (mode === 'entity') setAskResult(null)
        }}
        asking={asking}
        onLocate={handleLocate}
        onServerSearch={(term) => {
          setKeyword(term)
          void load({ fit: true, keyword: term })
        }}
        layout={layout}
        onLayoutChange={setLayout}
        depth={depth}
        onDepthChange={setDepth}
        depthDisabled={isConnected}
        limit={limit}
        onLimitChange={setLimit}
        onReload={() => {
          setKeyword(null)
          void load({ fit: true })
        }}
        onZoomIn={() => graphRef.current?.zoomBy(1.25)}
        onZoomOut={() => graphRef.current?.zoomBy(0.8)}
        onFit={() => graphRef.current?.fitView()}
        isFullscreen={isFullscreen}
        onToggleFullscreen={toggleFullscreen}
        showEdgeLabels={showEdgeLabels}
        onToggleEdgeLabels={setShowEdgeLabels}
        canEdit={canEdit}
        selectionCount={selection.length}
        onConnect={() => {
          if (selection.length === 2) openCreateEdge(selection[0], selection[1])
        }}
        onMerge={() => setMergeOpen(true)}
        onClearSelection={() => applySelection([])}
      />

      {/* 问图结果卡（B7）：Cypher 可复制 + 结果表格 + 高亮说明 */}
      {askResult && (
        <Alert
          type={askResult.ok ? 'info' : 'error'}
          showIcon
          closable
          onClose={() => setAskResult(null)}
          style={{ marginBottom: spacing.md }}
          message={askResult.ok
            ? t('knowledgegraph.canvas.askResultTitle', { count: askResult.rowCount ?? 0 })
            : t('knowledgegraph.canvas.askFailed')}
          description={(
            <div style={{ marginTop: 4 }}>
              <Typography.Paragraph code copyable style={{ marginBottom: 8, fontSize: 12, whiteSpace: 'pre-wrap' }}>
                {askResult.cypher}
              </Typography.Paragraph>
              {askResult.ok && (askResult.columns?.length ?? 0) > 0 && (
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                  {t('knowledgegraph.canvas.askRowsHint', {
                    rows: askResult.rowCount ?? 0,
                    truncated: askResult.truncated ? t('knowledgegraph.canvas.askTruncatedSuffix') : '',
                  })}
                  {(askResult.nodeIds?.length ?? 0) > 0 && ` · ${t('knowledgegraph.canvas.askHighlightHint', { count: askResult.nodeIds!.length })}`}
                </Typography.Paragraph>
              )}
              {!askResult.ok && askResult.error && (
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                  {askResult.error}
                </Typography.Paragraph>
              )}
            </div>
          )}
        />
      )}

      {(truncated || keyword) && (
        <Alert
          type="warning"
          showIcon
          message={keyword
            ? t('knowledgegraph.canvas.keywordFiltered', { keyword })
            : t('knowledgegraph.canvasTruncated', { limit })}
          style={{ marginBottom: spacing.md }}
        />
      )}

      <Spin spinning={loading}>
        <div
          ref={canvasShellRef}
          style={{
            position: 'relative',
            height: canvasHeight,
            border: `1px solid ${token.colorBorderSecondary}`,
            borderRadius: 8,
            overflow: 'hidden',
          }}
          onClick={() => setCtxMenu(null)}
        >
          <GraphCanvas
            key={themeKey}
            ref={graphRef}
            theme={{
              background: token.colorBgLayout,
              edgeStroke: token.colorBorder,
              labelFill: token.colorText,
              primary: token.colorPrimary,
            }}
            onNodeClick={handleNodeClick}
            onNodeDoubleClick={handleNodeDoubleClick}
            onEdgeClick={handleEdgeClick}
            onCanvasClick={handleCanvasClick}
            onContextMenu={(target) => setCtxMenu({ ...target, open: true })}
          />

          <LegendPanel
            types={legendTypes}
            hidden={hiddenTypes}
            onToggle={toggleTypeHidden}
            onShowAll={() => setHiddenTypes(new Set())}
          />

          {empty && !loading && (
            <Empty
              description={t('knowledgegraph.canvasEmpty')}
              style={{
                position: 'absolute',
                top: '50%',
                left: '50%',
                transform: 'translate(-50%, -50%)',
                margin: 0,
                zIndex: 1,
                pointerEvents: 'none',
              }}
            />
          )}

          {ctxMenu?.open && (contextMenuItems?.length ?? 0) > 0 && (
            <div style={{ position: 'fixed', left: ctxMenu.clientX, top: ctxMenu.clientY, zIndex: 1000 }}>
              <Dropdown
                open
                menu={{ items: contextMenuItems ?? [], onClick: handleContextMenuClick }}
                onOpenChange={(open) => {
                  if (!open) setCtxMenu(null)
                }}
              >
                <span />
              </Dropdown>
            </div>
          )}
        </div>
      </Spin>

      {!canEdit && (
        <div style={{ marginTop: spacing.sm, opacity: 0.65 }}>{t('knowledgegraph.canvasHint')}</div>
      )}

      {/* 新建实体（画布右键） */}
      <Modal
        open={nodeOpen}
        title={t('knowledgegraph.canvasEdit.createNodeTitle')}
        onOk={() => void submitCreateNode()}
        onCancel={() => setNodeOpen(false)}
        confirmLoading={nodeSaving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={nodeForm} layout="vertical">
          <Form.Item
            name="entityTypeId"
            label={t('knowledgegraph.entity.colType')}
            rules={[{ required: true, message: t('knowledgegraph.entity.typePlaceholder') }]}
          >
            <Select
              placeholder={t('knowledgegraph.entity.typePlaceholder')}
              options={entityTypes.filter((x) => x.entityTypeId != null).map((x) => ({ value: Number(x.entityTypeId), label: x.name ?? '' }))}
            />
          </Form.Item>
          <Form.Item
            name="name"
            label={t('knowledgegraph.entity.colName')}
            rules={[{ required: true, message: t('knowledgegraph.entity.namePlaceholder') }]}
          >
            <Input maxLength={200} placeholder={t('knowledgegraph.entity.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('knowledgegraph.entity.colDesc')}>
            <Input.TextArea maxLength={1000} rows={2} />
          </Form.Item>
          {selectedEntityDefs.length > 0 && (
            <>
              <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 4, fontSize: 13 }}>
                {t('knowledgegraph.props.instanceSection')}
              </Typography.Title>
              <PropertyInputs properties={selectedEntityDefs} />
            </>
          )}
        </Form>
      </Modal>

      {/* 连关系（多选两节点） */}
      <Modal
        open={edgeOpen}
        title={t('knowledgegraph.canvasEdit.createEdgeTitle')}
        onOk={() => void submitCreateEdge()}
        onCancel={() => setEdgeOpen(false)}
        confirmLoading={edgeSaving}
        okButtonProps={{ disabled: constraintViolated }}
        destroyOnHidden
        maskClosable={false}
      >
        <div style={{ marginBottom: spacing.md }}>
          {sourceNode?.label ?? pendingEdge?.source} → {targetNode?.label ?? pendingEdge?.target}
        </div>
        <Form form={edgeForm} layout="vertical">
          <Form.Item
            name="relationTypeId"
            label={t('knowledgegraph.relation.colType')}
            rules={[{ required: true, message: t('knowledgegraph.relation.typePlaceholder') }]}
          >
            <Select
              placeholder={t('knowledgegraph.relation.typePlaceholder')}
              options={relationTypes.filter((x) => x.relationTypeId != null).map((x) => ({ value: Number(x.relationTypeId), label: x.name ?? '' }))}
            />
          </Form.Item>
        </Form>
        {constraint && constraint.sourceTypeId == null && constraint.targetTypeId == null && (
          <div style={{ opacity: 0.65 }}>{t('knowledgegraph.relation.noConstraint')}</div>
        )}
        {constraintViolated ? (
          <Alert type="error" showIcon message={t('knowledgegraph.canvasEdit.constraintViolated')} style={{ marginTop: spacing.sm }} />
        ) : constraint && (constraint.sourceTypeId != null || constraint.targetTypeId != null) ? (
          <div style={{ opacity: 0.65 }}>
            {t('knowledgegraph.relation.constraintHint')}：
            {[
              constraint.sourceTypeId != null ? `${typeInfo.get(Number(constraint.sourceTypeId))?.name ?? '-'}` : null,
              constraint.targetTypeId != null ? `${typeInfo.get(Number(constraint.targetTypeId))?.name ?? '-'}` : null,
            ].filter(Boolean).join(' → ')}
          </div>
        ) : null}
      </Modal>

      {/* 实体合并（多选 / 导入疑似重复深链） */}
      <MergeNodesModal
        open={mergeOpen && mergeNodes.length >= 2}
        graphId={graphId}
        nodes={mergeNodes}
        typeNameOf={typeNameOf}
        onCancel={() => setMergeOpen(false)}
        onMerged={() => {
          setMergeOpen(false)
          applySelection([])
          void load({ fit: true })
        }}
      />

      <DetailSidebar
        node={detailNode}
        edge={detailEdge}
        typeNameOf={typeNameOf}
        edgeLabelOf={edgeLabelOf}
        orderedProps={detailProps}
        relatedEdges={relatedEdges}
        expanded={detailNode != null && expandedByRef.current.has(detailNode.id)}
        canEdit={canEdit}
        onNeighborClick={(nodeId) => {
          setDetailNodeId(nodeId)
          setDetailEdgeId(null)
          graphRef.current?.focusNode(nodeId)
        }}
        onEdgeClick={(edgeId) => {
          setDetailEdgeId(edgeId)
          setDetailNodeId(null)
        }}
        onToggleExpand={() => {
          if (detailNode) handleNodeDoubleClick(detailNode.id)
        }}
        onEditNode={() => {
          if (detailNode && teamId) {
            navigate(`/team/${teamId}/kg/${graphId}/maintenance?step=entities&keyword=${encodeURIComponent(detailNode.label)}`)
          }
        }}
        onDeleteNode={() => {
          if (detailNode) confirmDeleteNode(detailNode.id)
        }}
        onDeleteEdge={() => {
          if (detailEdge) confirmDeleteEdge(detailEdge.id)
        }}
        onClose={() => {
          setDetailNodeId(null)
          setDetailEdgeId(null)
        }}
      />
    </div>
  )
}
