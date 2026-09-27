import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Descriptions, Drawer, Empty, Input, Modal, Select, Space, Spin, Tag, Tooltip, Typography, theme } from 'antd'
import { Form } from 'antd'
import { FullscreenExitOutlined, FullscreenOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons'
import {
  Background,
  BackgroundVariant,
  ConnectionMode,
  Controls,
  Handle,
  MarkerType,
  Panel,
  Position,
  ReactFlow,
  ReactFlowProvider,
  applyNodeChanges,
  type Connection,
  type Edge,
  type Node,
  type NodeChange,
  type NodeProps,
  type ReactFlowInstance,
} from '@xyflow/react'
import { forceCenter, forceCollide, forceLink, forceManyBody, forceSimulation, type SimulationLinkDatum, type SimulationNodeDatum } from 'd3-force'
import '@xyflow/react/dist/style.css'
import { chartColors, feedback } from '@/design-system'
import { PropertyInputs, serializePropertyValues, toPropertyDefs } from './PropertyFields'
import { spacing } from '@/design-system/theme'
import {
  createKnowledgeGraphEdge,
  createKnowledgeGraphNode,
  deleteKnowledgeGraphEdge,
  deleteKnowledgeGraphNode,
  getKnowledgeGraphCanvas,
  getKnowledgeGraphNodeNeighbors,
  getKnowledgeGraphSchema,
  type KnowledgeGraphEntityTypeItem,
  type KnowledgeGraphEntityTypeProperty,
  type KnowledgeGraphRelationTypeItem,
} from '@/api/knowledgeGraph'

/** 画布最小高度：小屏/极端布局下保底可用 */
const MIN_CANVAS_HEIGHT = 320
/** 页面底部留白：画布撑满视口时预留 */
const PAGE_BOTTOM_PADDING = 24
const DEFAULT_LIMIT = 200
const LIMIT_OPTIONS = [100, 200, 500]

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举）；画布写操作仅限 Admin+ 的托管图 */
const ROLE_MEMBER = 0

interface CanvasNode {
  id: string
  label: string
  /** 托管图：实体类型 id；接入图：节点标签；用于着色与过滤 */
  typeKey: string
  entityTypeId?: number
  description?: string
  /** 实例属性值（航段距离/运价等） */
  properties?: Record<string, string>
}

interface CanvasEdge {
  id: string
  source: string
  target: string
  relationTypeId?: number
  /** 接入图直接带关系名；托管图按 relationTypeId 从模型解析 */
  relationName?: string
}

interface KgNodeData extends Record<string, unknown> {
  label: string
  color: string
  typeName?: string
  /** 控制连接桩显示（可编辑的托管图才允许拖线建边） */
  editable: boolean
}

type KgFlowNode = Node<KgNodeData>
type KgFlowEdge = Edge

interface LayoutPos { x: number; y: number }

interface SimNode extends SimulationNodeDatum { id: string }
type SimLink = SimulationLinkDatum<SimNode>

/** 力导向布局（d3-force 同步模拟）：老节点沿用上次坐标作初值保证展开时视觉连续，新节点环形落点后收敛 */
function computeLayout(nodes: CanvasNode[], edges: CanvasEdge[], previous: Map<string, LayoutPos>): Map<string, LayoutPos> {
  const simNodes: SimNode[] = nodes.map((n, i) => {
    const prev = previous.get(n.id)
    const angle = (i / Math.max(nodes.length, 1)) * Math.PI * 2
    return {
      id: n.id,
      x: prev?.x ?? Math.cos(angle) * 300,
      y: prev?.y ?? Math.sin(angle) * 300,
    }
  })
  const simLinks: SimLink[] = edges
    .filter((e) => e.source !== e.target)
    .map((e) => ({ source: e.source, target: e.target }))
  forceSimulation(simNodes)
    .force('charge', forceManyBody().strength(-420))
    .force('link', forceLink<SimNode, SimLink>(simLinks).id((d) => d.id).distance(150).strength(0.4))
    .force('center', forceCenter(0, 0))
    .force('collide', forceCollide(56))
    .stop()
    .tick(280)
  const out = new Map<string, LayoutPos>()
  for (const n of simNodes) out.set(n.id, { x: n.x ?? 0, y: n.y ?? 0 })
  return out
}

/** 自定义节点：类型色点 + 名称 + 类型名；上下连接桩仅编辑态用于拖线建边 */
function KgNodeCard({ data }: NodeProps<KgFlowNode>) {
  const { token } = theme.useToken()
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 2,
        padding: '8px 14px',
        borderRadius: 10,
        border: `1px solid ${token.colorBorderSecondary}`,
        background: token.colorBgContainer,
        boxShadow: token.boxShadowTertiary,
        minWidth: 84,
        maxWidth: 220,
        fontSize: 13,
      }}
    >
      <span
        aria-hidden
        style={{
          position: 'absolute',
          top: 8,
          left: 10,
          width: 8,
          height: 8,
          borderRadius: 4,
          background: data.color,
        }}
      />
      <Handle type="target" position={Position.Top} style={{ visibility: data.editable ? 'visible' : 'hidden' }} isConnectableStart={false} />
      <span style={{ maxWidth: 192, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontWeight: 500 }}>{data.label}</span>
      {data.typeName && (
        <span style={{ fontSize: 11, opacity: 0.65, maxWidth: 192, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{data.typeName}</span>
      )}
      <Handle type="source" position={Position.Bottom} style={{ visibility: data.editable ? 'visible' : 'hidden' }} isConnectableEnd={false} />
    </div>
  )
}

const nodeTypes = { kgNode: KgNodeCard }

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

/** 图览画布（React Flow + d3-force）：占满剩余高度与全部宽度 + 有界子图（加载上限可调）+ 类型过滤 + 关键字搜索 + 点选一跳展开 + 缩放/适应/全屏控件；托管图 Admin+ 可画布编辑（右键建实体、连接桩拖线建关系、右键删除） */
export function KnowledgeGraphCanvas({ graphId, mode, myRole = null, graphEnabled = true }: {
  graphId: number
  mode?: string | null
  myRole?: number | null
  graphEnabled?: boolean
}) {
  return (
    <ReactFlowProvider>
      <KnowledgeGraphCanvasInner graphId={graphId} mode={mode} myRole={myRole} graphEnabled={graphEnabled} />
    </ReactFlowProvider>
  )
}

function KnowledgeGraphCanvasInner({ graphId, mode, myRole = null, graphEnabled = true }: {
  graphId: number
  mode?: string | null
  myRole?: number | null
  graphEnabled?: boolean
}) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const rfInstanceRef = useRef<ReactFlowInstance | null>(null)
  const wrapperRef = useRef<HTMLDivElement | null>(null)
  const canvasShellRef = useRef<HTMLDivElement | null>(null)
  const nodesRef = useRef(new Map<string, CanvasNode>())
  const edgesRef = useRef(new Map<string, CanvasEdge>())
  /** 节点布局坐标缓存：布局初值 + 拖动位置回写，展开邻接时视觉连续 */
  const layoutPositionsRef = useRef(new Map<string, LayoutPos>())
  /** 置位后下一次数据同步直接按布局 bounds 设视口（初次加载/重新加载/筛选/搜索/上限变化），点选展开不重置视口 */
  const fitPendingRef = useRef(false)
  const [loading, setLoading] = useState(true)
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [relationTypes, setRelationTypes] = useState<KnowledgeGraphRelationTypeItem[]>([])
  const [typeFilter, setTypeFilter] = useState<string | null>(null)
  const [keyword, setKeyword] = useState('')
  const [limit, setLimit] = useState(DEFAULT_LIMIT)
  const [truncated, setTruncated] = useState(false)
  const [empty, setEmpty] = useState(false)
  const [canvasHeight, setCanvasHeight] = useState(MIN_CANVAS_HEIGHT)
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [flowNodes, setFlowNodes] = useState<KgFlowNode[]>([])
  const [flowEdges, setFlowEdges] = useState<KgFlowEdge[]>([])
  const [nodeOpen, setNodeOpen] = useState(false)
  const [nodeSaving, setNodeSaving] = useState(false)
  const [edgeOpen, setEdgeOpen] = useState(false)
  const [edgeSaving, setEdgeSaving] = useState(false)
  const [pendingEdge, setPendingEdge] = useState<PendingEdge | null>(null)
  const [detailNode, setDetailNode] = useState<CanvasNode | null>(null)
  const [detailEdge, setDetailEdge] = useState<CanvasEdge | null>(null)
  const [nodeForm] = Form.useForm<NodeFormValues>()
  const [edgeForm] = Form.useForm<EdgeFormValues>()

  const isConnected = mode === 'connected'
  const canEdit = !isConnected && graphEnabled && myRole !== null && myRole !== ROLE_MEMBER

  const colorOf = useMemo(() => {
    const map = new Map<string, string>()
    entityTypes.forEach((x, index) => {
      // 托管图实体类型有 id；接入图以标签名（name）为 key
      const value = x.entityTypeId != null ? String(x.entityTypeId) : (x.name ?? '')
      map.set(value, x.color || chartColors[index % chartColors.length])
    })
    return map
  }, [entityTypes])

  const colorFor = useCallback(
    (typeKey: string) => colorOf.get(typeKey) ?? chartColors[0],
    [colorOf],
  )

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

  /** 边显示文本：优先接入图 relationName，托管图按关系类型解析 */
  const edgeLabelOf = useCallback(
    (edge: CanvasEdge) => {
      if (edge.relationName) return edge.relationName
      if (edge.relationTypeId != null) return relationInfo.get(edge.relationTypeId)?.name ?? ''
      return ''
    },
    [relationInfo],
  )

  const typeNameOf = useCallback(
    (node: CanvasNode) => {
      if (node.entityTypeId != null) return typeInfo.get(node.entityTypeId)?.name ?? ''
      return isConnected ? node.typeKey : ''
    },
    [typeInfo, isConnected],
  )

  /**
   * 按布局 bounds 直接计算并设置视口（自实现 fitView：布局坐标与节点估计外包尺寸完全已知，不依赖节点异步测量）。
   * 必须用 onInit 下发的实例：组件内 useReactFlow 的 viewportHelper 在部分挂载时序下拿不到 panZoom（setViewport 静默失败），
   * 而 onInit 实例与画布内部 store 绑定可靠。
   */
  const fitViewportToNodes = useCallback((instance: ReactFlowInstance, attempts = 0) => {
    const positions = layoutPositionsRef.current
    const shell = canvasShellRef.current
    if (positions.size === 0 || !shell) return
    let minX = Infinity
    let minY = Infinity
    let maxX = -Infinity
    let maxY = -Infinity
    for (const p of positions.values()) {
      minX = Math.min(minX, p.x)
      minY = Math.min(minY, p.y)
      maxX = Math.max(maxX, p.x)
      maxY = Math.max(maxY, p.y)
    }
    const width = shell.clientWidth || 800
    const height = shell.clientHeight || 600
    const pad = 72
    // 节点卡片估计外包尺寸（布局坐标为卡片中心，含两行文本与边标签余量）
    const boundsW = maxX - minX + 260
    const boundsH = maxY - minY + 120
    const zoom = Math.min(Math.max(Math.min((width - pad * 2) / boundsW, (height - pad * 2) / boundsH), 0.1), 2.5)
    const centerX = (minX + maxX) / 2
    const centerY = (minY + maxY) / 2
    void instance.setViewport({ x: width / 2 - centerX * zoom, y: height / 2 - centerY * zoom, zoom }).then((ok) => {
      if (ok) {
        fitPendingRef.current = false
      } else if (attempts < 30) {
        // 视口底座（panZoom）尚未就绪，稍后重试
        window.setTimeout(() => fitViewportToNodes(instance, attempts + 1), 100)
      }
    })
  }, [])

  /** 数据源 → React Flow 节点/边（力导向布局，fitView 由数据同步 effect 消费置位标记） */
  const syncFlow = useCallback(() => {
    const nodes = [...nodesRef.current.values()]
    const edges = [...edgesRef.current.values()]
    setEmpty(nodes.length === 0)
    const positions = computeLayout(nodes, edges, layoutPositionsRef.current)
    layoutPositionsRef.current = positions
    setFlowNodes(nodes.map((n) => ({
      id: n.id,
      type: 'kgNode' as const,
      position: positions.get(n.id) ?? { x: 0, y: 0 },
      data: { label: n.label, color: colorFor(n.typeKey), typeName: typeNameOf(n) || undefined, editable: canEdit },
    })))
    setFlowEdges(edges.map((e) => ({
      id: e.id,
      source: e.source,
      target: e.target,
      label: edgeLabelOf(e) || undefined,
      markerEnd: { type: MarkerType.ArrowClosed },
    })))
  }, [colorFor, edgeLabelOf, typeNameOf, canEdit])

  // 数据同步后消费 fitView 置位；onInit 前数据先到则保持置位，由 onInit 消费
  useEffect(() => {
    const inst = rfInstanceRef.current
    if (!fitPendingRef.current || flowNodes.length === 0 || !inst) return
    fitViewportToNodes(inst)
  }, [flowNodes, fitViewportToNodes])

  const handleCanvasInit = useCallback((instance: ReactFlowInstance) => {
    rfInstanceRef.current = instance
    if (fitPendingRef.current && flowNodes.length > 0) {
      fitViewportToNodes(instance)
    }
  }, [fitViewportToNodes])

  // ===== 画布高度自适应：撑满视口剩余高度；进入原生全屏时铺满整屏 =====
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

  // ===== 全屏状态跟随浏览器 =====
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

  // ===== 新建实体（画布空白右键） =====
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
      syncFlow()
      feedback.success(t('knowledgegraph.createSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setNodeSaving(false)
    }
  }

  // ===== 新建关系（编辑态从节点连接桩拖线） =====
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
      syncFlow()
      feedback.success(t('knowledgegraph.createSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示（如违反起止约束 400）
    } finally {
      setEdgeSaving(false)
    }
  }

  // ===== 删除（右键节点/边） =====
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
          syncFlow()
          feedback.success(t('knowledgegraph.deleteSuccess'))
        } catch {
          // 错误已由全局请求中间件统一提示
        }
      },
    })
  }, [graphId, syncFlow, t])

  const confirmDeleteEdge = useCallback((edgeId: string) => {
    Modal.confirm({
      title: t('knowledgegraph.canvasEdit.deleteEdgeTitle'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteKnowledgeGraphEdge(graphId, edgeId)
          edgesRef.current.delete(edgeId)
          syncFlow()
          feedback.success(t('knowledgegraph.deleteSuccess'))
        } catch {
          // 错误已由全局请求中间件统一提示
        }
      },
    })
  }, [graphId, syncFlow, t])

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

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    fitPendingRef.current = true
    try {
      const res = await getKnowledgeGraphCanvas(graphId, {
        entityTypeId: !isConnected && typeFilter != null ? Number(typeFilter) : null,
        label: isConnected ? typeFilter : null,
        keyword: keyword || undefined,
        limit,
      })
      nodesRef.current.clear()
      edgesRef.current.clear()
      for (const n of res.nodes) {
        if (n.nodeId != null) {
          nodesRef.current.set(String(n.nodeId), {
            id: String(n.nodeId),
            label: n.name ?? '',
            typeKey: n.entityLabel != null ? String(n.entityLabel) : String(n.entityTypeId ?? 0),
            entityTypeId: n.entityTypeId != null ? Number(n.entityTypeId) : undefined,
            description: n.description ?? undefined,
            properties: n.properties ?? undefined,
          })
        }
      }
      for (const e of res.edges) {
        if (e.edgeId != null) {
          edgesRef.current.set(String(e.edgeId), {
            id: String(e.edgeId),
            source: String(e.sourceNodeId ?? ''),
            target: String(e.targetNodeId ?? ''),
            relationTypeId: e.relationTypeId != null ? Number(e.relationTypeId) : undefined,
            relationName: e.relationName ?? undefined,
          })
        }
      }
      setTruncated(res.truncated)
      syncFlow()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [graphId, typeFilter, keyword, limit, syncFlow, isConnected])

  useEffect(() => { void loadSchema() }, [loadSchema])
  useEffect(() => { void load() }, [load])

  const handleExpand = useCallback(async (nodeId: string) => {
    try {
      const res = await getKnowledgeGraphNodeNeighbors(graphId, nodeId, 100)
      for (const n of res.nodes) {
        const key = String(n.nodeId)
        if (n.nodeId != null && !nodesRef.current.has(key)) {
          nodesRef.current.set(key, {
            id: key,
            label: n.name ?? '',
            typeKey: n.entityLabel != null ? String(n.entityLabel) : String(n.entityTypeId ?? 0),
            entityTypeId: n.entityTypeId != null ? Number(n.entityTypeId) : undefined,
            description: n.description ?? undefined,
            properties: n.properties ?? undefined,
          })
        }
      }
      for (const e of res.edges) {
        const key = String(e.edgeId)
        if (e.edgeId != null && !edgesRef.current.has(key)) {
          edgesRef.current.set(key, {
            id: key,
            source: String(e.sourceNodeId ?? ''),
            target: String(e.targetNodeId ?? ''),
            relationTypeId: e.relationTypeId != null ? Number(e.relationTypeId) : undefined,
            relationName: e.relationName ?? undefined,
          })
        }
      }
      if (res.truncated) setTruncated(true)
      syncFlow()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [graphId, syncFlow])

  // 点选节点展开一跳邻接并查看详情；点选边查看关系信息
  const handleNodeClick = useCallback((_: unknown, node: KgFlowNode) => {
    setDetailNode(nodesRef.current.get(node.id) ?? null)
    setDetailEdge(null)
    void handleExpand(node.id)
  }, [handleExpand])

  const handleEdgeClick = useCallback((_: unknown, edge: KgFlowEdge) => {
    setDetailEdge(edgesRef.current.get(edge.id) ?? null)
    setDetailNode(null)
  }, [])

  // 拖动节点：应用位置并回写布局缓存，重布局（展开）时保持拖后位置
  const handleNodesChange = useCallback((changes: NodeChange<KgFlowNode>[]) => {
    setFlowNodes((nds) => applyNodeChanges(changes, nds))
    for (const c of changes) {
      if (c.type === 'position' && c.position) {
        layoutPositionsRef.current.set(c.id, { x: c.position.x, y: c.position.y })
      }
    }
  }, [])

  const handleConnect = useCallback((connection: Connection) => {
    if (!canEdit || !connection.source || !connection.target) return
    openCreateEdge(connection.source, connection.target)
  }, [canEdit, openCreateEdge])

  // 新建实例弹窗：按所选实体类型渲染属性输入
  const selectedEntityTypeId = Form.useWatch('entityTypeId', nodeForm)
  const selectedEntityDefs = selectedEntityTypeId != null
    ? toPropertyDefs(typeInfo.get(Number(selectedEntityTypeId))?.properties)
    : []

  // 新建关系弹窗：选中关系类型的起止约束提示与候选校验
  const selectedRelationTypeId = Form.useWatch('relationTypeId', edgeForm)
  const constraint = relationInfo.get(Number(selectedRelationTypeId ?? NaN))
  const sourceNode = pendingEdge ? nodesRef.current.get(pendingEdge.source) : undefined
  const targetNode = pendingEdge ? nodesRef.current.get(pendingEdge.target) : undefined
  const constraintViolated = pendingEdge && constraint
    ? (constraint.sourceTypeId != null && sourceNode?.entityTypeId != null && Number(constraint.sourceTypeId) !== sourceNode.entityTypeId) ||
      (constraint.targetTypeId != null && targetNode?.entityTypeId != null && Number(constraint.targetTypeId) !== targetNode.entityTypeId)
    : false

  // 节点详情：属性按模型定义顺序优先，未定义的属性追加在后
  const detailNodeTypeName = detailNode ? typeNameOf(detailNode) : ''
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
  const detailEdgeLabel = detailEdge ? edgeLabelOf(detailEdge) : ''
  const detailEdgeSource = detailEdge ? nodesRef.current.get(detailEdge.source)?.label ?? detailEdge.source : ''
  const detailEdgeTarget = detailEdge ? nodesRef.current.get(detailEdge.target)?.label ?? detailEdge.target : ''
  const closeDetail = () => { setDetailNode(null); setDetailEdge(null) }

  return (
    <div ref={wrapperRef} onContextMenu={(e) => e.preventDefault()}>
      <Space style={{ marginBottom: spacing.md }} wrap>
        {entityTypes.map((x) => {
          const value = x.entityTypeId != null ? String(x.entityTypeId) : (x.name ?? '')
          const active = typeFilter === value
          return (
            <Tag
              key={value}
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
        <Tooltip title={t('knowledgegraph.canvasLimitTip')}>
          <Select
            value={limit}
            onChange={(value) => setLimit(value)}
            options={LIMIT_OPTIONS.map((x) => ({ value: x, label: `${x}` }))}
            style={{ width: 128 }}
            popupMatchSelectWidth={false}
          />
        </Tooltip>
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          {t('knowledgegraph.canvasReload')}
        </Button>
      </Space>

      {truncated && (
        <Alert type="warning" showIcon message={t('knowledgegraph.canvasTruncated', { limit })} style={{ marginBottom: spacing.md }} />
      )}

      <Spin spinning={loading}>
        <div
          ref={canvasShellRef}
          style={{
            position: 'relative',
            height: canvasHeight,
            border: `1px solid ${token.colorBorderSecondary}`,
            borderRadius: 8,
            background: token.colorBgLayout,
            overflow: 'hidden',
          }}
        >
          <ReactFlowProvider>
            <ReactFlow
              onInit={handleCanvasInit}
              nodes={flowNodes}
              edges={flowEdges}
              nodeTypes={nodeTypes}
              onNodesChange={handleNodesChange}
              onNodeClick={handleNodeClick}
              onEdgeClick={handleEdgeClick}
              onConnect={handleConnect}
              connectionMode={ConnectionMode.Loose}
              nodesConnectable={canEdit}
              onNodeContextMenu={canEdit
                ? (event, node) => {
                    event.preventDefault()
                    confirmDeleteNode(node.id)
                  }
                : undefined}
              onEdgeContextMenu={canEdit
                ? (event, edge) => {
                    event.preventDefault()
                    confirmDeleteEdge(edge.id)
                  }
                : undefined}
              onPaneContextMenu={canEdit
                ? (event) => {
                    event.preventDefault()
                    openCreateNode()
                  }
                : undefined}
              fitView={false}
              minZoom={0.1}
              maxZoom={2.5}
              proOptions={{ hideAttribution: true }}
              defaultEdgeOptions={{
                type: 'default',
                style: { stroke: token.colorBorder },
                labelStyle: { fontSize: 11, fill: token.colorTextSecondary },
                labelBgStyle: { fill: token.colorBgContainer, fillOpacity: 0.9 },
                labelBgPadding: [4, 2],
                labelBgBorderRadius: 4,
              }}
            >
              <Background variant={BackgroundVariant.Dots} gap={18} size={1} color={token.colorBorderSecondary} />
              <Controls position="bottom-right" showInteractive={false} />
              <Panel position="top-right">
                <Tooltip title={t(isFullscreen ? 'knowledgegraph.canvasFullscreenExit' : 'knowledgegraph.canvasFullscreen')} placement="left">
                  <Button size="small" icon={isFullscreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />} onClick={toggleFullscreen} />
                </Tooltip>
              </Panel>
            </ReactFlow>
          </ReactFlowProvider>

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
        </div>
      </Spin>

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
      {!canEdit && (
        <div style={{ marginTop: spacing.sm, opacity: 0.65 }}>{t('knowledgegraph.canvasHint')}</div>
      )}

      <Drawer
        open={detailNode != null || detailEdge != null}
        onClose={closeDetail}
        title={detailNode ? detailNode.label : detailEdge ? detailEdgeLabel : ''}
        width={360}
        mask={false}
      >
        {detailNode ? (
          <>
            <Descriptions column={1} size="small">
              <Descriptions.Item label={t('knowledgegraph.entity.colType')}>{detailNodeTypeName || '-'}</Descriptions.Item>
              <Descriptions.Item label={t('knowledgegraph.entity.colDesc')}>{detailNode.description || '-'}</Descriptions.Item>
            </Descriptions>
            <Typography.Title level={5} style={{ marginTop: spacing.md, marginBottom: spacing.xs, fontSize: 13 }}>
              {t('knowledgegraph.detail.propsTitle')}
            </Typography.Title>
            {detailProps.length > 0 ? (
              <Descriptions column={1} size="small">
                {detailProps.map(([key, value]) => (
                  <Descriptions.Item key={key} label={key}>{value}</Descriptions.Item>
                ))}
              </Descriptions>
            ) : (
              <div style={{ opacity: 0.65 }}>{t('knowledgegraph.detail.noProps')}</div>
            )}
          </>
        ) : detailEdge ? (
          <Descriptions column={1} size="small">
            <Descriptions.Item label={t('knowledgegraph.relation.colType')}>{detailEdgeLabel || '-'}</Descriptions.Item>
            <Descriptions.Item label={t('knowledgegraph.detail.endpoints')}>
              {detailEdgeSource} → {detailEdgeTarget}
            </Descriptions.Item>
          </Descriptions>
        ) : null}
      </Drawer>
    </div>
  )
}
