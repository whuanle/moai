import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Form, Modal, Popconfirm, Select, Space } from 'antd'
import type { TableColumnsType } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
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
}

export function KnowledgeGraphRelations({ graphId }: KnowledgeGraphRelationsProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<KnowledgeGraphEdgeItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [relationTypes, setRelationTypes] = useState<KnowledgeGraphRelationTypeItem[]>([])
  const [nodes, setNodes] = useState<KnowledgeGraphNodeItem[]>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeGraphEdgeItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<FormValues>()
  const pageSize = 20

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    try {
      const res = await getKnowledgeGraphEdges(graphId, { pageNo, pageSize })
      setItems(res.items ?? [])
      setTotal(Number(res.total ?? 0))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [graphId, pageNo])

  const loadSchema = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    try {
      const schema = await getKnowledgeGraphSchema(graphId)
      setRelationTypes(schema.relationTypes ?? [])
    } catch {
      setRelationTypes([])
    }
  }, [graphId])

  // v1 手动图谱节点量少，直接拉取前 100 个用于构建名称映射与选择项（后续可加按 id 批量查询）
  const loadNodes = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    try {
      const res = await getKnowledgeGraphNodes(graphId, { pageSize: 100 })
      setNodes(res.items ?? [])
    } catch {
      setNodes([])
    }
  }, [graphId])

  useEffect(() => { void load() }, [load])
  useEffect(() => { void loadSchema() }, [loadSchema])
  useEffect(() => { void loadNodes() }, [loadNodes])

  const relationName = useMemo(() => {
    const map = new Map<number, string>()
    for (const x of relationTypes) map.set(Number(x.relationTypeId), x.name ?? '')
    return map
  }, [relationTypes])

  const nodeName = useMemo(() => {
    const map = new Map<string, string>()
    for (const x of nodes) if (x.nodeId) map.set(x.nodeId, x.name ?? '')
    return map
  }, [nodes])

  const nodeOptions = useMemo(
    () => nodes.filter((x) => x.nodeId).map((x) => ({ value: String(x.nodeId), label: x.name ?? '' })),
    [nodes],
  )

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setOpen(true)
  }

  const openEdit = (record: KnowledgeGraphEdgeItem) => {
    setEditing(record)
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
      render: (_, record) => relationName.get(Number(record.relationTypeId)) || '-',
    },
    {
      title: t('knowledgegraph.relation.colTarget'),
      key: 'target',
      render: (_, record) => nodeName.get(record.targetNodeId ?? '') || record.targetNodeId || '-',
    },
    {
      title: '',
      key: 'action',
      width: 130,
      fixed: 'right',
      render: (_, record) => (
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

  return (
    <>
      <Space style={{ marginBottom: spacing.md }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('knowledgegraph.relation.create')}
        </Button>
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
            />
          </Form.Item>
          <Form.Item
            name="sourceNodeId"
            label={t('knowledgegraph.relation.colSource')}
            rules={editing ? [] : [{ required: true, message: t('knowledgegraph.relation.sourcePlaceholder') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder={t('knowledgegraph.relation.sourcePlaceholder')}
              options={nodeOptions}
              disabled={!!editing}
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
              options={nodeOptions}
              disabled={!!editing}
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
