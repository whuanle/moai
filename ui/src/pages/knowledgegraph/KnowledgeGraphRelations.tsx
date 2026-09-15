import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Form, Modal, Popconfirm, Select, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { ApartmentOutlined, PlusOutlined, ProfileOutlined } from '@ant-design/icons'
import { DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  createKnowledgeGraphEdge,
  deleteKnowledgeGraphEdge,
  getKnowledgeGraphEdges,
  getKnowledgeGraphNodes,
  getKnowledgeGraphSchema,
  updateKnowledgeGraphEdge,
  type KnowledgeGraphEdgeItem,
  type KnowledgeGraphNodeItem,
  type KnowledgeGraphRelationTypeItem,
} from '@/api/knowledgeGraph'

interface FormValues {
  relationTypeId: number
  sourceNodeId?: string
  targetNodeId?: string
}

interface KnowledgeGraphRelationsProps {
  graphId: number
  teamId: number
  graphEnabled?: boolean
  myRole?: number | null
}

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举）；节点/边写操作仅限 Admin+ */
const ROLE_MEMBER = 0

export function KnowledgeGraphRelations({ graphId, teamId, graphEnabled = true, myRole = null }: KnowledgeGraphRelationsProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<KnowledgeGraphEdgeItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [relationTypes, setRelationTypes] = useState<KnowledgeGraphRelationTypeItem[]>([])
  const [entityTypes, setEntityTypes] = useState<{ id: number; name: string }[]>([])
  const [nodes, setNodes] = useState<KnowledgeGraphNodeItem[]>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeGraphEdgeItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<FormValues>()
  const pageSize = 20
  const canWrite = graphEnabled && myRole !== null && myRole !== ROLE_MEMBER
  // 支持从实体页/模型页带 ?nodeId= / ?relationTypeId= 跳入并按条件过滤
  const nodeFilter = useMemo(() => searchParams.get('nodeId'), [searchParams])
  const relationFilter = useMemo(() => {
    const raw = searchParams.get('relationTypeId')
    return raw != null && Number.isFinite(Number(raw)) ? Number(raw) : null
  }, [searchParams])

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    try {
      const res = await getKnowledgeGraphEdges(graphId, { relationTypeId: relationFilter, nodeId: nodeFilter ?? undefined, pageNo, pageSize })
      setItems(res.items ?? [])
      setTotal(Number(res.total ?? 0))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [graphId, relationFilter, nodeFilter, pageNo])

  const loadSchema = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    try {
      const schema = await getKnowledgeGraphSchema(graphId)
      setRelationTypes(schema.relationTypes ?? [])
      setEntityTypes((schema.entityTypes ?? []).map((x) => ({ id: Number(x.entityTypeId), name: x.name ?? '' })))
    } catch {
      setRelationTypes([])
    }
  }, [graphId])

  // v1 手动图谱节点量少，直接拉取前 200 个用于构建名称映射与选择项（后续可加按 id 批量查询）
  const loadNodes = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    try {
      const res = await getKnowledgeGraphNodes(graphId, { pageSize: 200 })
      setNodes(res.items ?? [])
    } catch {
      setNodes([])
    }
  }, [graphId])

  useEffect(() => { void load() }, [load])
  useEffect(() => { void loadSchema() }, [loadSchema])
  useEffect(() => { void loadNodes() }, [loadNodes])

  const relationInfo = useMemo(() => {
    const map = new Map<number, KnowledgeGraphRelationTypeItem>()
    for (const x of relationTypes) map.set(Number(x.relationTypeId), x)
    return map
  }, [relationTypes])

  const nodeName = useMemo(() => {
    const typeMap = new Map(entityTypes.map((x) => [x.id, x.name]))
    const map = new Map<string, string>()
    for (const x of nodes) if (x.nodeId) map.set(x.nodeId, typeMap.get(Number(x.entityTypeId)) ? `${x.name ?? ''}（${typeMap.get(Number(x.entityTypeId))}）` : (x.name ?? ''))
    return map
  }, [nodes, entityTypes])

  const nodeOptions = useMemo(
    () => nodes.filter((x) => x.nodeId).map((x) => ({ value: String(x.nodeId), label: nodeName.get(String(x.nodeId)) ?? x.name ?? '' })),
    [nodes, nodeName],
  )

  // 当前选中关系类型的起止约束：只允许约束类型的节点作为起点/终点（无约束则全部可选）
  const [selectedRelationTypeId, setSelectedRelationTypeId] = useState<number | null>(null)
  const constraint = relationInfo.get(Number(selectedRelationTypeId ?? NaN))
  const typeName = useMemo(() => new Map(entityTypes.map((x) => [x.id, x.name])), [entityTypes])
  const sourceConstraintId = constraint?.sourceTypeId != null ? Number(constraint.sourceTypeId) : null
  const targetConstraintId = constraint?.targetTypeId != null ? Number(constraint.targetTypeId) : null
  const constraintText = constraint
    ? [
        sourceConstraintId != null ? `${t('knowledgegraph.relation.colSource')}: ${typeName.get(sourceConstraintId) ?? '-'}` : null,
        targetConstraintId != null ? `${t('knowledgegraph.relation.colTarget')}: ${typeName.get(targetConstraintId) ?? '-'}` : null,
      ].filter(Boolean).join(' → ')
    : null

  const filterOptionsByConstraint = (options: { value: string; label: string }[], allowedTypeId: number | null) => {
    if (allowedTypeId == null) return options
    const allowed = new Set(nodes.filter((x) => Number(x.entityTypeId) === allowedTypeId && x.nodeId).map((x) => String(x.nodeId)))
    return options.filter((x) => allowed.has(x.value))
  }
  const sourceOptions = filterOptionsByConstraint(nodeOptions, sourceConstraintId)
  const targetOptions = filterOptionsByConstraint(nodeOptions, targetConstraintId)

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setSelectedRelationTypeId(null)
    setOpen(true)
  }

  const openEdit = (record: KnowledgeGraphEdgeItem) => {
    setEditing(record)
    setSelectedRelationTypeId(Number(record.relationTypeId))
    form.setFieldsValue({
      relationTypeId: Number(record.relationTypeId),
      sourceNodeId: record.sourceNodeId ?? undefined,
      targetNodeId: record.targetNodeId ?? undefined,
    })
    setOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing?.edgeId) {
        await updateKnowledgeGraphEdge(graphId, editing.edgeId, { relationTypeId: values.relationTypeId })
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else if (values.sourceNodeId && values.targetNodeId) {
        await createKnowledgeGraphEdge(graphId, {
          relationTypeId: values.relationTypeId,
          sourceNodeId: values.sourceNodeId,
          targetNodeId: values.targetNodeId,
        })
        feedback.success(t('knowledgegraph.createSuccess'))
      }
      setOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: KnowledgeGraphEdgeItem) => {
    if (!record.edgeId) return
    try {
      await deleteKnowledgeGraphEdge(graphId, record.edgeId)
      feedback.success(t('knowledgegraph.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const columns: TableColumnsType<KnowledgeGraphEdgeItem> = [
    {
      title: t('knowledgegraph.relation.colSource'),
      key: 'source',
      render: (_, record) => nodeName.get(record.sourceNodeId ?? '') || record.sourceNodeId || '-',
    },
    {
      title: t('knowledgegraph.relation.colType'),
      key: 'type',
      render: (_, record) => relationInfo.get(Number(record.relationTypeId))?.name || '-',
    },
    {
      title: t('knowledgegraph.relation.colTarget'),
      key: 'target',
      render: (_, record) => nodeName.get(record.targetNodeId ?? '') || record.targetNodeId || '-',
    },
    ...(canWrite
      ? [
          {
            title: '',
            key: 'action',
            width: 130,
            fixed: 'right' as const,
            render: (_: unknown, record: KnowledgeGraphEdgeItem) => (
              <Space size={4}>
                <Button type="link" size="small" onClick={() => openEdit(record)}>
                  {t('knowledgegraph.edit')}
                </Button>
                <Popconfirm title={t('knowledgegraph.relation.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                  <Button type="link" size="small" danger>
                    {t('knowledgegraph.delete')}
                  </Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]
      : []),
  ]

  const gotoEntities = () => navigate(`/team/${teamId}/kg/${graphId}/entities`)
  const gotoSchema = () => navigate(`/team/${teamId}/kg/${graphId}/schema`)

  return (
    <>
      {/* 引导：连接关系的前置条件是"有关系类型 + 有节点"，缺哪个就指去哪 */}
      {canWrite && !loading && relationTypes.length === 0 && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: spacing.md }}
          message={t('knowledgegraph.relation.needTypeTitle')}
          description={t('knowledgegraph.relation.needTypeDesc')}
          action={<Button size="small" type="primary" icon={<ApartmentOutlined />} onClick={gotoSchema}>{t('knowledgegraph.entity.goSchema')}</Button>}
        />
      )}
      {canWrite && !loading && relationTypes.length > 0 && nodes.length === 0 && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: spacing.md }}
          message={t('knowledgegraph.relation.needNodeTitle')}
          description={t('knowledgegraph.relation.needNodeDesc')}
          action={<Button size="small" type="primary" icon={<ProfileOutlined />} onClick={gotoEntities}>{t('knowledgegraph.relation.goEntities')}</Button>}
        />
      )}
      <Space style={{ marginBottom: spacing.md }} wrap>
        {canWrite && (
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('knowledgegraph.relation.create')}
          </Button>
        )}
        <Select
          allowClear
          placeholder={t('knowledgegraph.relation.filterType')}
          style={{ width: 180 }}
          value={relationFilter}
          onChange={(value) => {
            setPageNo(1)
            const next = new URLSearchParams(value != null ? { relationTypeId: String(value) } : {})
            if (nodeFilter) next.set('nodeId', nodeFilter)
            setSearchParams(next, { replace: true })
          }}
          options={relationTypes.map((x) => ({ value: Number(x.relationTypeId), label: x.name ?? '' }))}
        />
        {nodeFilter && nodeName.get(nodeFilter) && (
          <Tag
            closable
            onClose={() => {
              setPageNo(1)
              const next = new URLSearchParams()
              if (relationFilter != null) next.set('relationTypeId', String(relationFilter))
              setSearchParams(next, { replace: true })
            }}
          >
            {t('knowledgegraph.relation.nodeFilterPrefix')}: {nodeName.get(nodeFilter)}
          </Tag>
        )}
      </Space>
      <DataTable<KnowledgeGraphEdgeItem>
        rowKey={(record) => String(record.edgeId)}
        columns={columns}
        dataSource={items}
        loading={loading}
        onRefresh={() => void load()}
        refreshLoading={loading}
        pagination={{
          current: pageNo,
          pageSize,
          total,
          onChange: (page) => setPageNo(page),
        }}
      />
      <Modal
        open={open}
        title={editing ? t('knowledgegraph.relation.edit') : t('knowledgegraph.relation.create')}
        onOk={() => void handleSubmit()}
        onCancel={() => setOpen(false)}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="relationTypeId"
            label={t('knowledgegraph.relation.colType')}
            rules={[{ required: true, message: t('knowledgegraph.relation.typePlaceholder') }]}
          >
            <Select
              placeholder={t('knowledgegraph.relation.typePlaceholder')}
              options={relationTypes.map((x) => ({ value: Number(x.relationTypeId), label: x.name ?? '' }))}
              onChange={(value) => setSelectedRelationTypeId(Number(value))}
            />
          </Form.Item>
          {constraintText && (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: spacing.md }}
              message={`${t('knowledgegraph.relation.constraintHint')}：${constraintText}`}
            />
          )}
          <Form.Item
            name="sourceNodeId"
            label={t('knowledgegraph.relation.colSource')}
            rules={editing ? [] : [{ required: true, message: t('knowledgegraph.relation.sourcePlaceholder') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder={t('knowledgegraph.relation.sourcePlaceholder')}
              options={sourceOptions}
              disabled={!!editing}
              notFoundContent={constraint?.sourceTypeId != null ? t('knowledgegraph.relation.noCandidateByConstraint') : undefined}
            />
          </Form.Item>
          <Form.Item
            name="targetNodeId"
            label={t('knowledgegraph.relation.colTarget')}
            rules={editing ? [] : [{ required: true, message: t('knowledgegraph.relation.targetPlaceholder') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder={t('knowledgegraph.relation.targetPlaceholder')}
              options={targetOptions}
              disabled={!!editing}
              notFoundContent={constraint?.targetTypeId != null ? t('knowledgegraph.relation.noCandidateByConstraint') : undefined}
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
