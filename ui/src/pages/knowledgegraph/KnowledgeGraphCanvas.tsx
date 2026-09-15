import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Empty, Input, Modal, Space, Spin, Tag, Typography } from 'antd'
import { Form, Select } from 'antd'
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons'
import { Graph } from '@antv/g6'
import { feedback } from '@/design-system'
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

const FALLBACK_COLORS = ['#5B8FF9', '#5AD8A6', '#F6BD16', '#E8684A', '#6DC8EC', '#9270CA', '#FF9D4D', '#269A99']
const EDGE_COLOR = 'rgba(16, 24, 40, 0.25)'
const CANVAS_HEIGHT = 520
const DEFAULT_LIMIT = 200

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举）；画布写操作仅限 Admin+ 的托管图 */
const ROLE_MEMBER = 0

interface CanvasNode {
  id: string
  label: string
  /** 托管图：实体类型 id；接入图：节点标签；用于着色与过滤 */
  typeKey: string
  entityTypeId?: number
  description?: string
}

interface CanvasEdge {
  id: string
  source: string
  target: string
}

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

/** 图览画布：有界子图 + 类型过滤 + 关键字搜索 + 点选一跳展开；托管图 Admin+ 可画布编辑（右键建实体、拖拽连线建关系、右键删除） */
export function KnowledgeGraphCanvas({ graphId, mode, myRole = null, graphEnabled = true }: {
  graphId: number
  teamId?: number
  mode?: string | null
  myRole?: number | null
  graphEnabled?: boolean
}) {
  const { t } = useTranslation()
  const containerRef = useRef<HTMLDivElement | null>(null)
  const graphRef = useRef<Graph | null>(null)
  const nodesRef = useRef(new Map<string, CanvasNode>())
  const edgesRef = useRef(new Map<string, CanvasEdge>())
  const [loading, setLoading] = useState(true)
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [relationTypes, setRelationTypes] = useState<KnowledgeGraphRelationTypeItem[]>([])
  const [typeFilter, setTypeFilter] = useState<string | null>(null)
  const [keyword, setKeyword] = useState('')
  const [truncated, setTruncated] = useState(false)
  const [empty, setEmpty] = useState(false)
  const [nodeOpen, setNodeOpen] = useState(false)
  const [nodeSaving, setNodeSaving] = useState(false)
  const [edgeOpen, setEdgeOpen] = useState(false)
  const [edgeSaving, setEdgeSaving] = useState(false)
  const [pendingEdge, setPendingEdge] = useState<PendingEdge | null>(null)
  const [nodeForm] = Form.useForm<NodeFormValues>()
  const [edgeForm] = Form.useForm<EdgeFormValues>()

  const isConnected = mode === 'connected'
  const canEdit = !isConnected && graphEnabled && myRole !== null && myRole !== ROLE_MEMBER

  const colorOf = useMemo(() => {
    const map = new Map<string, string>()
    entityTypes.forEach((x, index) => {
      // 托管图实体类型有 id；接入图以标签名（name）为 key
      const value = x.entityTypeId != null ? String(x.entityTypeId) : (x.name ?? '')
      map.set(value, x.color || FALLBACK_COLORS[index % FALLBACK_COLORS.length])
    })
    return map
  }, [entityTypes])

  const colorFor = useCallback(
    (typeKey: string) => colorOf.get(typeKey) ?? FALLBACK_COLORS[0],
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

  const syncGraph = useCallback(() => {
    const graph = graphRef.current
    if (!graph) return
    const nodes = [...nodesRef.current.values()]
    const edges = [...edgesRef.current.values()]
    setEmpty(nodes.length === 0)
    graph.setData({
      nodes: nodes.map((n) => ({ id: n.id, data: { label: n.label, typeKey: n.typeKey } })),
      edges: edges.map((e) => ({ id: e.id, source: e.source, target: e.target, data: {} })),
    })
    void graph.render()
  }, [])

  // ===== 新建实体（画布右键） =====
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
      syncGraph()
      feedback.success(t('knowledgegraph.createSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setNodeSaving(false)
    }
  }

  // ===== 新建关系（拖拽连线） =====
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
      syncGraph()
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
          syncGraph()
          feedback.success(t('knowledgegraph.deleteSuccess'))
        } catch {
          // 错误已由全局请求中间件统一提示
        }
      },
    })
  }, [graphId, syncGraph, t])

  const confirmDeleteEdge = useCallback((edgeId: string) => {
    Modal.confirm({
      title: t('knowledgegraph.canvasEdit.deleteEdgeTitle'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteKnowledgeGraphEdge(graphId, edgeId)
          edgesRef.current.delete(edgeId)
          syncGraph()
          feedback.success(t('knowledgegraph.deleteSuccess'))
        } catch {
          // 错误已由全局请求中间件统一提示
        }
      },
    })
  }, [graphId, syncGraph, t])

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
          fill: (datum: { data?: { typeKey?: string } }) => colorFor(String(datum.data?.typeKey ?? '')),
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
      // 编辑态：拖节点=连线（create-edge），拖空白=平移、滚轮=缩放；只读态：拖节点=移动节点
      behaviors: [
        'drag-canvas',
        'zoom-canvas',
        ...(canEdit ? [] : ['drag-element']),
        // 拖拽连线建关系（仅可编辑的托管图启用）；松手后先移除临时边，弹窗选关系类型确认后才真正创建
        ...(canEdit
          ? [{
              type: 'create-edge' as const,
              trigger: 'drag' as const,
              onFinish: (edge: { id?: string; source: string; target: string }) => {
                // 先移除临时边，弹窗选关系类型确认后才以真实 id 重建
                const id = edge.id ?? ''
                if (id) graph.removeData({ nodes: [], edges: [id] })
                openCreateEdge(String(edge.source), String(edge.target))
              },
            }]
          : []),
      ],
    })
    graphRef.current = graph

    // 右键画布空白 → 新建实体；右键节点/边 → 删除（仅可编辑的托管图）
    const onCanvasContext = (event: { targetType?: string; preventDefault?: () => void }) => {
      if (!canEdit) return
      event.preventDefault?.()
      openCreateNode()
    }
    const onNodeContext = (event: { target?: { id?: string }; preventDefault?: () => void }) => {
      if (!canEdit) return
      event.preventDefault?.()
      const id = event.target?.id
      if (typeof id === 'string') confirmDeleteNode(id)
    }
    const onEdgeContext = (event: { target?: { id?: string }; preventDefault?: () => void }) => {
      if (!canEdit) return
      event.preventDefault?.()
      const id = event.target?.id
      if (typeof id === 'string') confirmDeleteEdge(id)
    }
    graph.on('canvas:contextmenu', onCanvasContext as never)
    graph.on('node:contextmenu', onNodeContext as never)
    graph.on('edge:contextmenu', onEdgeContext as never)

    return () => {
      graphRef.current = null
      void graph.destroy()
    }
  }, [colorFor, canEdit, openCreateNode, openCreateEdge, confirmDeleteNode, confirmDeleteEdge])

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
    try {
      const res = await getKnowledgeGraphCanvas(graphId, {
        entityTypeId: !isConnected && typeFilter != null ? Number(typeFilter) : null,
        label: isConnected ? typeFilter : null,
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
            typeKey: n.entityLabel != null ? String(n.entityLabel) : String(n.entityTypeId ?? 0),
            entityTypeId: n.entityTypeId != null ? Number(n.entityTypeId) : undefined,
            description: n.description ?? undefined,
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
  }, [graphId, typeFilter, keyword, syncGraph, isConnected])

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
          })
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

  return (
    <div onContextMenu={(e) => e.preventDefault()}>
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
        <Button icon={<ReloadOutlined />} onClick={() => void load()}>
          {t('knowledgegraph.canvasReload')}
        </Button>
      </Space>

      {truncated && (
        <Alert type="warning" showIcon message={t('knowledgegraph.canvasTruncated')} style={{ marginBottom: spacing.md }} />
      )}

      {canEdit && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: spacing.md }}
          message={t('knowledgegraph.canvasEdit.hint')}
        />
      )}

      <Spin spinning={loading}>
        <div
          ref={containerRef}
          style={{
            height: CANVAS_HEIGHT,
            border: '1px solid rgba(16, 24, 40, 0.08)',
            borderRadius: 8,
            background: 'transparent',
          }}
        />
      </Spin>

      {empty && !loading && (
        <Empty description={t('knowledgegraph.canvasEmpty')} style={{ marginTop: -CANVAS_HEIGHT / 2 - 16 }} />
      )}

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
        <div style={{ marginBottom: spacing.xs, opacity: 0.65 }}>{t('knowledgegraph.canvasHint')}</div>
      )}
    </div>
  )
}
