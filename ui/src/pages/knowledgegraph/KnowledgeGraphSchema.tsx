import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Form, Input, Modal, Popconfirm, Select, Space, Spin, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { DataTable, feedback } from '@/design-system'
import { PropertyListEditor } from './PropertyFields'
import { neutralColors, spacing } from '@/design-system/theme'
import {
  createEntityType,
  createRelationType,
  deleteEntityType,
  deleteRelationType,
  getKnowledgeGraphSchema,
  updateEntityType,
  updateRelationType,
  type KnowledgeGraphEntityTypeItem,
  type KnowledgeGraphIntrospectionDiff,
  type KnowledgeGraphEntityTypeProperty,
  type KnowledgeGraphRelationTypeItem,
} from '@/api/knowledgeGraph'

const ROLE_MEMBER = 0

interface EntityTypeFormValues {
  name: string
  color?: string
  description?: string
  properties?: KnowledgeGraphEntityTypeProperty[]
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
  teamId: number
  myRole: number | null
  mode?: string | null
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
          border: `1px solid ${neutralColors.border}`,
          backgroundColor: color,
        }}
      />
      <span>{color}</span>
    </Space>
  )
}

function IntrospectionDiffAlert({ changes }: { changes: KnowledgeGraphIntrospectionDiff | null }) {
  const { t } = useTranslation()
  if (!changes) return null
  const isEmpty =
    !(changes.addedLabels?.length) &&
    !(changes.removedLabels?.length) &&
    !(changes.addedRelationTypes?.length) &&
    !(changes.removedRelationTypes?.length)
  if (isEmpty) return null
  const section = (added?: string[] | null, removed?: string[] | null) => {
    const parts: string[] = []
    if (added?.length) parts.push(`${t('knowledgegraph.schemaConnected.diffAdded')}: ${added.join(', ')}`)
    if (removed?.length) parts.push(`${t('knowledgegraph.schemaConnected.diffRemoved')}: ${removed.join(', ')}`)
    return parts.join('；')
  }
  const labelSection = section(changes.addedLabels, changes.removedLabels)
  const relationSection = section(changes.addedRelationTypes, changes.removedRelationTypes)
  const message = [labelSection, relationSection].filter(Boolean).map((x, i) => (i === 0 ? x : ` ${x}`)).join('\n')
  return (
    <Alert
      type="info"
      showIcon
      message={t('knowledgegraph.schemaConnected.diffTitle')}
      description={message}
      style={{ marginBottom: spacing.md, whiteSpace: 'pre-line' }}
    />
  )
}

export function KnowledgeGraphSchema({ graphId, teamId, myRole, mode: modeProp }: KnowledgeGraphSchemaProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [fetchedMode, setFetchedMode] = useState<string | null>(null)
  const [propertyKeys, setPropertyKeys] = useState<string[]>([])
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [relationTypes, setRelationTypes] = useState<KnowledgeGraphRelationTypeItem[]>([])
  const [entityOpen, setEntityOpen] = useState(false)
  const [relationOpen, setRelationOpen] = useState(false)
  const [editingEntityType, setEditingEntityType] = useState<KnowledgeGraphEntityTypeItem | null>(null)
  const [editingRelationType, setEditingRelationType] = useState<KnowledgeGraphRelationTypeItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [changes, setChanges] = useState<KnowledgeGraphIntrospectionDiff | null>(null)
  const [entityForm] = Form.useForm<EntityTypeFormValues>()
  const [relationForm] = Form.useForm<RelationTypeFormValues>()

  const canManage = myRole !== null && myRole !== ROLE_MEMBER
  const mode = modeProp ?? fetchedMode
  const isConnected = mode === 'connected'

  const load = useCallback(async (refresh = false) => {
    if (!Number.isFinite(graphId) || graphId <= 0) {
      setLoading(false)
      return
    }
    setLoading(true)
    try {
      const schema = await getKnowledgeGraphSchema(graphId, refresh)
      setFetchedMode(schema.mode ?? null)
      setPropertyKeys(schema.propertyKeys ?? [])
      setEntityTypes(schema.entityTypes ?? [])
      setRelationTypes(schema.relationTypes ?? [])
      setChanges(schema.changes ?? null)
    } catch {
      setFetchedMode(null)
      setPropertyKeys([])
      setEntityTypes([])
      setRelationTypes([])
      setChanges(null)
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
    entityForm.setFieldValue('properties', [])
    setEntityOpen(true)
  }

  const openEditEntityType = (record: KnowledgeGraphEntityTypeItem) => {
    setEditingEntityType(record)
    entityForm.setFieldsValue({
      name: record.name ?? '',
      color: record.color ?? undefined,
      description: record.description ?? undefined,
      properties: (record.properties ?? []).map((x) => ({ name: x.name ?? '', type: x.type ?? 'string', required: x.required ?? false, description: x.description ?? '' })),
    })
    setEntityOpen(true)
  }

  const handleSubmitEntityType = async () => {
    const values = await entityForm.validateFields()
    const properties = (values.properties ?? []).filter((x) => x.name?.trim())
    setSaving(true)
    try {
      if (editingEntityType) {
        await updateEntityType(graphId, Number(editingEntityType.entityTypeId), { ...values, properties })
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        await createEntityType(graphId, { ...values, properties })
        feedback.success(t('knowledgegraph.createSuccess'))
      }
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
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        await createRelationType(graphId, payload)
        feedback.success(t('knowledgegraph.createSuccess'))
      }
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
    {
      title: t('knowledgegraph.props.column'),
      key: 'properties',
      width: 140,
      render: (_, record) => {
        const props = record.properties ?? []
        if (props.length === 0) return <span style={{ opacity: 0.5 }}>-</span>
        return (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {props.map((x) => x.name).join('、')}
          </Typography.Text>
        )
      },
    },
    {
      title: '',
      key: 'view',
      width: 110,
      render: (_: unknown, record: KnowledgeGraphEntityTypeItem) =>
        record.entityTypeId != null ? (
          <Button type="link" size="small" onClick={() => navigate(`/team/${teamId}/kg/${graphId}/entities?typeId=${record.entityTypeId}`)}>
            {t('knowledgegraph.schema.viewEntities')}
          </Button>
        ) : null,
    },
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
    {
      title: '',
      key: 'view',
      width: 110,
      render: (_: unknown, record: KnowledgeGraphRelationTypeItem) =>
        record.relationTypeId != null ? (
          <Button type="link" size="small" onClick={() => navigate(`/team/${teamId}/kg/${graphId}/relations?relationTypeId=${record.relationTypeId}`)}>
            {t('knowledgegraph.schema.viewRelations')}
          </Button>
        ) : null,
    },
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

  const connectedEntityColumns: TableColumnsType<KnowledgeGraphEntityTypeItem> = [
    { title: t('knowledgegraph.schema.name'), dataIndex: 'name', key: 'name', render: (v: string | null | undefined) => v || '-' },
    { title: t('knowledgegraph.schemaConnected.count'), dataIndex: 'count', key: 'count', width: 160, render: (v: number | string | null | undefined) => v ?? '-' },
  ]

  const connectedRelationColumns: TableColumnsType<KnowledgeGraphRelationTypeItem> = [
    { title: t('knowledgegraph.schema.name'), dataIndex: 'name', key: 'name', render: (v: string | null | undefined) => v || '-' },
    { title: t('knowledgegraph.schemaConnected.count'), dataIndex: 'count', key: 'count', width: 160, render: (v: number | string | null | undefined) => v ?? '-' },
  ]

  if (isConnected) {
    return (
      <Space direction="vertical" size={spacing.lg} style={{ width: '100%' }}>
        <Space>
          <Button icon={<ReloadOutlined />} loading={loading} onClick={() => void load(true)}>
            {t('knowledgegraph.schemaConnected.refresh')}
          </Button>
        </Space>
        <IntrospectionDiffAlert changes={changes} />
        <div>
          <div style={{ fontWeight: 600, marginBottom: spacing.md }}>{t('knowledgegraph.schemaConnected.labels')}</div>
          <DataTable<KnowledgeGraphEntityTypeItem>
            rowKey={(record) => String(record.entityTypeId ?? record.name)}
            columns={connectedEntityColumns}
            dataSource={entityTypes}
            loading={loading}
            pagination={false}
          />
        </div>
        <div>
          <div style={{ fontWeight: 600, marginBottom: spacing.md }}>{t('knowledgegraph.schemaConnected.relationshipTypes')}</div>
          <DataTable<KnowledgeGraphRelationTypeItem>
            rowKey={(record) => String(record.relationTypeId ?? record.name)}
            columns={connectedRelationColumns}
            dataSource={relationTypes}
            loading={loading}
            pagination={false}
          />
        </div>
        <div>
          <div style={{ fontWeight: 600, marginBottom: spacing.md }}>{t('knowledgegraph.schemaConnected.propertyKeys')}</div>
          <Space wrap size={spacing.xs}>
            {propertyKeys.length > 0 ? propertyKeys.map((key) => <Tag key={key}>{key}</Tag>) : <span>-</span>}
          </Space>
        </div>
      </Space>
    )
  }

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: spacing.xl }}>
        <Spin />
      </div>
    )
  }

  return (
    <Space direction="vertical" size={spacing.lg} style={{ width: '100%' }}>
      <Alert type="info" showIcon message={t('knowledgegraph.schema.introTitle')} description={t('knowledgegraph.schema.introDesc')} />
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
          <Form.Item label={t('knowledgegraph.props.editorTitle')} tooltip={t('knowledgegraph.props.editorTooltip')}>
            <PropertyListEditor />
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
