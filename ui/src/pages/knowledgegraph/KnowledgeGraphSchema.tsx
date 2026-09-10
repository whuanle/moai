import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Form, Input, Modal, Popconfirm, Select, Space } from 'antd'
import type { TableColumnsType } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  createEntityType,
  createRelationType,
  deleteEntityType,
  deleteRelationType,
  getKnowledgeGraphSchema,
  updateEntityType,
  updateRelationType,
  type KnowledgeGraphEntityTypeItem,
  type KnowledgeGraphRelationTypeItem,
} from '@/api/knowledgeGraph'

const ROLE_MEMBER = 0

interface EntityTypeFormValues {
  name: string
  color?: string
  description?: string
}

interface RelationTypeFormValues {
  name: string
  color?: string
  description?: string
  sourceTypeId?: number
  targetTypeId?: number
}

interface KnowledgeGraphSchemaProps {
  graphId: number
  myRole: number | null
}

function ColorCell({ color }: { color?: string | null }) {
  if (!color) return <span>-</span>
  return (
    <Space size={6}>
      <span
        style={{
          display: 'inline-block',
          width: 14,
          height: 14,
          borderRadius: 3,
          border: '1px solid rgba(0,0,0,0.15)',
          backgroundColor: color,
        }}
      />
      <span>{color}</span>
    </Space>
  )
}

export function KnowledgeGraphSchema({ graphId, myRole }: KnowledgeGraphSchemaProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [relationTypes, setRelationTypes] = useState<KnowledgeGraphRelationTypeItem[]>([])
  const [entityOpen, setEntityOpen] = useState(false)
  const [relationOpen, setRelationOpen] = useState(false)
  const [editingEntityType, setEditingEntityType] = useState<KnowledgeGraphEntityTypeItem | null>(null)
  const [editingRelationType, setEditingRelationType] = useState<KnowledgeGraphRelationTypeItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [entityForm] = Form.useForm<EntityTypeFormValues>()
  const [relationForm] = Form.useForm<RelationTypeFormValues>()

  const canManage = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    try {
      const schema = await getKnowledgeGraphSchema(graphId)
      setEntityTypes(schema.entityTypes ?? [])
      setRelationTypes(schema.relationTypes ?? [])
    } catch {
      setEntityTypes([])
      setRelationTypes([])
    } finally {
      setLoading(false)
    }
  }, [graphId])

  useEffect(() => { void load() }, [load])

  const typeName = useMemo(() => {
    const map = new Map<number, string>()
    for (const x of entityTypes) map.set(Number(x.entityTypeId), x.name ?? '')
    return map
  }, [entityTypes])

  const typeOptions = useMemo(
    () => entityTypes.map((x) => ({ value: Number(x.entityTypeId), label: x.name ?? '' })),
    [entityTypes],
  )

  const openCreateEntityType = () => {
    setEditingEntityType(null)
    entityForm.resetFields()
    setEntityOpen(true)
  }

  const openEditEntityType = (record: KnowledgeGraphEntityTypeItem) => {
    setEditingEntityType(record)
    entityForm.setFieldsValue({
      name: record.name ?? '',
      color: record.color ?? undefined,
      description: record.description ?? undefined,
    })
    setEntityOpen(true)
  }

  const handleSubmitEntityType = async () => {
    const values = await entityForm.validateFields()
    setSaving(true)
    try {
      if (editingEntityType) {
        await updateEntityType(graphId, Number(editingEntityType.entityTypeId), values)
      } else {
        await createEntityType(graphId, values)
      }
      feedback.success(t('knowledgegraph.saveSuccess'))
      setEntityOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDeleteEntityType = async (record: KnowledgeGraphEntityTypeItem) => {
    try {
      await deleteEntityType(graphId, Number(record.entityTypeId))
      feedback.success(t('knowledgegraph.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const openCreateRelationType = () => {
    setEditingRelationType(null)
    relationForm.resetFields()
    setRelationOpen(true)
  }

  const openEditRelationType = (record: KnowledgeGraphRelationTypeItem) => {
    setEditingRelationType(record)
    relationForm.setFieldsValue({
      name: record.name ?? '',
      color: record.color ?? undefined,
      description: record.description ?? undefined,
      sourceTypeId: record.sourceTypeId != null ? Number(record.sourceTypeId) : undefined,
      targetTypeId: record.targetTypeId != null ? Number(record.targetTypeId) : undefined,
    })
    setRelationOpen(true)
  }

  const handleSubmitRelationType = async () => {
    const values = await relationForm.validateFields()
    setSaving(true)
    try {
      const payload = {
        name: values.name,
        color: values.color,
        description: values.description,
        sourceTypeId: values.sourceTypeId ?? null,
        targetTypeId: values.targetTypeId ?? null,
      }
      if (editingRelationType) {
        await updateRelationType(graphId, Number(editingRelationType.relationTypeId), payload)
      } else {
        await createRelationType(graphId, payload)
      }
      feedback.success(t('knowledgegraph.saveSuccess'))
      setRelationOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDeleteRelationType = async (record: KnowledgeGraphRelationTypeItem) => {
    try {
      await deleteRelationType(graphId, Number(record.relationTypeId))
      feedback.success(t('knowledgegraph.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const entityColumns: TableColumnsType<KnowledgeGraphEntityTypeItem> = [
    { title: t('knowledgegraph.schema.name'), dataIndex: 'name', key: 'name', render: (v: string | null | undefined) => v || '-' },
    { title: t('knowledgegraph.schema.color'), dataIndex: 'color', key: 'color', width: 140, render: (v: string | null | undefined) => <ColorCell color={v} /> },
    { title: t('knowledgegraph.schema.desc'), dataIndex: 'description', key: 'description', ellipsis: true, render: (v: string | null | undefined) => v || '-' },
    ...(canManage
      ? [
          {
            title: '',
            key: 'action',
            width: 130,
            fixed: 'right' as const,
            render: (_: unknown, record: KnowledgeGraphEntityTypeItem) => (
              <Space size={4}>
                <Button type="link" size="small" onClick={() => openEditEntityType(record)}>
                  {t('knowledgegraph.edit')}
                </Button>
                <Popconfirm title={t('knowledgegraph.schema.deleteEntityConfirm')} onConfirm={() => void handleDeleteEntityType(record)}>
                  <Button type="link" size="small" danger>
                    {t('knowledgegraph.delete')}
                  </Button>
                </Popconfirm>
              </Space>
            ),
          } as TableColumnsType<KnowledgeGraphEntityTypeItem>[number],
        ]
      : []),
  ]

  const relationColumns: TableColumnsType<KnowledgeGraphRelationTypeItem> = [
    { title: t('knowledgegraph.schema.name'), dataIndex: 'name', key: 'name', render: (v: string | null | undefined) => v || '-' },
    { title: t('knowledgegraph.schema.color'), dataIndex: 'color', key: 'color', width: 140, render: (v: string | null | undefined) => <ColorCell color={v} /> },
    {
      title: t('knowledgegraph.schema.sourceType'),
      key: 'sourceType',
      render: (_, record) => (record.sourceTypeId != null ? typeName.get(Number(record.sourceTypeId)) || '-' : t('knowledgegraph.schema.anyType')),
    },
    {
      title: t('knowledgegraph.schema.targetType'),
      key: 'targetType',
      render: (_, record) => (record.targetTypeId != null ? typeName.get(Number(record.targetTypeId)) || '-' : t('knowledgegraph.schema.anyType')),
    },
    { title: t('knowledgegraph.schema.desc'), dataIndex: 'description', key: 'description', ellipsis: true, render: (v: string | null | undefined) => v || '-' },
    ...(canManage
      ? [
          {
            title: '',
            key: 'action',
            width: 130,
            fixed: 'right' as const,
            render: (_: unknown, record: KnowledgeGraphRelationTypeItem) => (
              <Space size={4}>
                <Button type="link" size="small" onClick={() => openEditRelationType(record)}>
                  {t('knowledgegraph.edit')}
                </Button>
                <Popconfirm title={t('knowledgegraph.schema.deleteRelationConfirm')} onConfirm={() => void handleDeleteRelationType(record)}>
                  <Button type="link" size="small" danger>
                    {t('knowledgegraph.delete')}
                  </Button>
                </Popconfirm>
              </Space>
            ),
          } as TableColumnsType<KnowledgeGraphRelationTypeItem>[number],
        ]
      : []),
  ]

  return (
    <Space direction="vertical" size={spacing.lg} style={{ width: '100%' }}>
      <div>
        <Space style={{ marginBottom: spacing.md }}>
          <span style={{ fontWeight: 600 }}>{t('knowledgegraph.schema.entityTypes')}</span>
          {canManage && (
            <Button type="primary" size="small" icon={<PlusOutlined />} onClick={openCreateEntityType}>
              {t('knowledgegraph.schema.addEntityType')}
            </Button>
          )}
        </Space>
        <DataTable<KnowledgeGraphEntityTypeItem>
          rowKey={(record) => String(record.entityTypeId)}
          columns={entityColumns}
          dataSource={entityTypes}
          loading={loading}
          pagination={false}
        />
      </div>

      <div>
        <Space style={{ marginBottom: spacing.md }}>
          <span style={{ fontWeight: 600 }}>{t('knowledgegraph.schema.relationTypes')}</span>
          {canManage && (
            <Button type="primary" size="small" icon={<PlusOutlined />} onClick={openCreateRelationType}>
              {t('knowledgegraph.schema.addRelationType')}
            </Button>
          )}
        </Space>
        <DataTable<KnowledgeGraphRelationTypeItem>
          rowKey={(record) => String(record.relationTypeId)}
          columns={relationColumns}
          dataSource={relationTypes}
          loading={loading}
          pagination={false}
        />
      </div>

      <Modal
        open={entityOpen}
        title={editingEntityType ? t('knowledgegraph.schema.editEntityType') : t('knowledgegraph.schema.addEntityType')}
        onOk={() => void handleSubmitEntityType()}
        onCancel={() => setEntityOpen(false)}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={entityForm} layout="vertical">
          <Form.Item name="name" label={t('knowledgegraph.schema.name')} rules={[{ required: true, message: t('knowledgegraph.schema.name') }]}>
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item name="color" label={t('knowledgegraph.schema.color')}>
            <Input type="color" style={{ width: 72, padding: 2 }} />
          </Form.Item>
          <Form.Item name="description" label={t('knowledgegraph.schema.desc')}>
            <Input.TextArea maxLength={255} rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={relationOpen}
        title={editingRelationType ? t('knowledgegraph.schema.editRelationType') : t('knowledgegraph.schema.addRelationType')}
        onOk={() => void handleSubmitRelationType()}
        onCancel={() => setRelationOpen(false)}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={relationForm} layout="vertical">
          <Form.Item name="name" label={t('knowledgegraph.schema.name')} rules={[{ required: true, message: t('knowledgegraph.schema.name') }]}>
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item name="color" label={t('knowledgegraph.schema.color')}>
            <Input type="color" style={{ width: 72, padding: 2 }} />
          </Form.Item>
          <Form.Item name="sourceTypeId" label={t('knowledgegraph.schema.sourceType')}>
            <Select allowClear placeholder={t('knowledgegraph.schema.anyType')} options={typeOptions} />
          </Form.Item>
          <Form.Item name="targetTypeId" label={t('knowledgegraph.schema.targetType')}>
            <Select allowClear placeholder={t('knowledgegraph.schema.anyType')} options={typeOptions} />
          </Form.Item>
          <Form.Item name="description" label={t('knowledgegraph.schema.desc')}>
            <Input.TextArea maxLength={255} rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
