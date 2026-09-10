import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Form, Input, Modal, Popconfirm, Select, Space } from 'antd'
import type { TableColumnsType } from 'antd'
import { PlusOutlined, SearchOutlined } from '@ant-design/icons'
import { DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  createKnowledgeGraphNode,
  deleteKnowledgeGraphNode,
  getKnowledgeGraphNodes,
  getKnowledgeGraphSchema,
  updateKnowledgeGraphNode,
  type KnowledgeGraphEntityTypeItem,
  type KnowledgeGraphNodeItem,
} from '@/api/knowledgeGraph'

interface FormValues {
  entityTypeId: number
  name: string
  description?: string
}

interface KnowledgeGraphEntitiesProps {
  graphId: number
  graphEnabled?: boolean
}

export function KnowledgeGraphEntities({ graphId, graphEnabled = true }: KnowledgeGraphEntitiesProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<KnowledgeGraphNodeItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [keyword, setKeyword] = useState('')
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeGraphNodeItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<FormValues>()
  const pageSize = 20

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    try {
      const res = await getKnowledgeGraphNodes(graphId, { keyword, pageNo, pageSize })
      setItems(res.items ?? [])
      setTotal(Number(res.total ?? 0))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [graphId, keyword, pageNo])

  const loadSchema = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    try {
      const schema = await getKnowledgeGraphSchema(graphId)
      setEntityTypes(schema.entityTypes ?? [])
    } catch {
      setEntityTypes([])
    }
  }, [graphId])

  useEffect(() => { void load() }, [load])
  useEffect(() => { void loadSchema() }, [loadSchema])

  const typeName = useMemo(() => {
    const map = new Map<number, string>()
    for (const x of entityTypes) map.set(Number(x.entityTypeId), x.name ?? '')
    return map
  }, [entityTypes])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setOpen(true)
  }

  const openEdit = (record: KnowledgeGraphNodeItem) => {
    setEditing(record)
    form.setFieldsValue({
      entityTypeId: Number(record.entityTypeId),
      name: record.name ?? '',
      description: record.description ?? undefined,
    })
    setOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing?.nodeId) {
        await updateKnowledgeGraphNode(graphId, editing.nodeId, values)
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        await createKnowledgeGraphNode(graphId, values)
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

  const handleDelete = async (record: KnowledgeGraphNodeItem) => {
    if (!record.nodeId) return
    try {
      await deleteKnowledgeGraphNode(graphId, record.nodeId)
      feedback.success(t('knowledgegraph.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const columns: TableColumnsType<KnowledgeGraphNodeItem> = [
    {
      title: t('knowledgegraph.entity.colName'),
      dataIndex: 'name',
      key: 'name',
      render: (value: string | null | undefined) => value || '-',
    },
    {
      title: t('knowledgegraph.entity.colType'),
      key: 'type',
      render: (_, record) => typeName.get(Number(record.entityTypeId)) || '-',
    },
    {
      title: t('knowledgegraph.entity.colDesc'),
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
      render: (value: string | null | undefined) => value || '-',
    },
    {
      title: '',
      key: 'action',
      width: 130,
      fixed: 'right',
      render: (_, record) => (
        <Space size={4}>
          <Button type="link" size="small" disabled={!graphEnabled} onClick={() => openEdit(record)}>
            {t('knowledgegraph.edit')}
          </Button>
          <Popconfirm title={t('knowledgegraph.entity.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
            <Button type="link" size="small" danger disabled={!graphEnabled}>
              {t('knowledgegraph.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      <Space style={{ marginBottom: spacing.md }} wrap>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          disabled={!graphEnabled || entityTypes.length === 0}
          onClick={openCreate}
        >
          {t('knowledgegraph.entity.create')}
        </Button>
        <Input.Search
          allowClear
          placeholder={t('knowledgegraph.searchPlaceholder')}
          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
          onSearch={(value) => {
            setPageNo(1)
            setKeyword(value)
          }}
          style={{ width: 240 }}
        />
      </Space>
      <DataTable<KnowledgeGraphNodeItem>
        rowKey={(record) => String(record.nodeId)}
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
        title={editing ? t('knowledgegraph.entity.edit') : t('knowledgegraph.entity.create')}
        onOk={() => void handleSubmit()}
        onCancel={() => setOpen(false)}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="entityTypeId"
            label={t('knowledgegraph.entity.colType')}
            rules={[{ required: true, message: t('knowledgegraph.entity.typePlaceholder') }]}
          >
            <Select
              placeholder={t('knowledgegraph.entity.typePlaceholder')}
              options={entityTypes.map((x) => ({ value: Number(x.entityTypeId), label: x.name ?? '' }))}
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
        </Form>
      </Modal>
    </>
  )
}
