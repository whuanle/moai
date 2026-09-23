import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Form, Input, Modal, Popconfirm, Select, Space, Tag, Typography } from 'antd'
import type { FormInstance, TableColumnsType } from 'antd'
import { ApartmentOutlined, PlusOutlined, SearchOutlined } from '@ant-design/icons'
import { chartColors, DataTable, feedback  } from '@/design-system'
import { PropertyInputs, deserializePropertyValues, serializePropertyValues, toPropertyDefs, type PropertyDef } from './PropertyFields'
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
  props?: Record<string, string | number | boolean | object | null>
}

interface KnowledgeGraphEntitiesProps {
  graphId: number
  teamId: number
  graphEnabled?: boolean
  myRole?: number | null
}

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举）；节点/边写操作仅限 Admin+ */
const ROLE_MEMBER = 0

export function KnowledgeGraphEntities({ graphId, teamId, graphEnabled = true, myRole = null }: KnowledgeGraphEntitiesProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
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
  const canWrite = graphEnabled && myRole !== null && myRole !== ROLE_MEMBER
  // 支持从模型页/关系页带 ?typeId= 跳入并按类型过滤
  const typeFilter = useMemo(() => {
    const raw = searchParams.get('typeId')
    return raw != null && Number.isFinite(Number(raw)) ? Number(raw) : null
  }, [searchParams])

  const load = useCallback(async () => {
    if (!Number.isFinite(graphId) || graphId <= 0) return
    setLoading(true)
    try {
      const res = await getKnowledgeGraphNodes(graphId, { entityTypeId: typeFilter, keyword, pageNo, pageSize })
      setItems(res.items ?? [])
      setTotal(Number(res.total ?? 0))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [graphId, typeFilter, keyword, pageNo])

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

  const typeInfo = useMemo(() => {
    const map = new Map<number, { name: string; color: string; properties: PropertyDef[] }>()
    entityTypes.forEach((x, index) => {
      map.set(Number(x.entityTypeId), {
        name: x.name ?? '',
        color: x.color || chartColors[index % chartColors.length],
        properties: toPropertyDefs(x.properties),
      })
    })
    return map
  }, [entityTypes])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldValue('props', undefined)
    // 从类型筛选跳进来新建时默认选中该类型
    if (typeFilter != null && typeInfo.has(typeFilter)) form.setFieldValue('entityTypeId', typeFilter)
    setOpen(true)
  }

  const openEdit = (record: KnowledgeGraphNodeItem) => {
    setEditing(record)
    const defs = typeInfo.get(Number(record.entityTypeId))?.properties ?? []
    form.setFieldsValue({
      entityTypeId: Number(record.entityTypeId),
      name: record.name ?? '',
      description: record.description ?? undefined,
      props: deserializePropertyValues(record.properties ?? undefined, defs) as FormValues['props'],
    })
    setOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    const defs = typeInfo.get(Number(values.entityTypeId))?.properties ?? []
    const properties = serializePropertyValues(values.props, defs)
    setSaving(true)
    try {
      if (editing?.nodeId) {
        await updateKnowledgeGraphNode(graphId, editing.nodeId, { ...values, properties })
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        await createKnowledgeGraphNode(graphId, { ...values, properties })
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

  const gotoRelationsOf = (record: KnowledgeGraphNodeItem) => {
    navigate(`/team/${teamId}/kg/${graphId}/relations?nodeId=${record.nodeId}`)
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
      render: (_, record) => {
        const info = typeInfo.get(Number(record.entityTypeId))
        if (!info) return '-'
        return (
          <Space size={6}>
            <span aria-hidden style={{ display: 'inline-block', width: 8, height: 8, borderRadius: 4, background: info.color }} />
            <span>{info.name}</span>
          </Space>
        )
      },
    },
    {
      title: t('knowledgegraph.entity.colDesc'),
      key: 'description',
      ellipsis: true,
      render: (_, record) => {
        const defs = typeInfo.get(Number(record.entityTypeId))?.properties ?? []
        const props = Object.entries(record.properties ?? {}).filter(([, v]) => v !== '')
        return (
          <Space direction="vertical" size={2}>
            <Typography.Text type="secondary" ellipsis style={{ maxWidth: 360 }}>{record.description || '-'}</Typography.Text>
            {props.length > 0 && (
              <Space size={4} wrap>
                {props.map(([k, v]) => (
                  <Tag key={k} style={{ marginInlineEnd: 0 }}>
                    {defs.find((d) => d.name === k)?.name ?? k}: {String(v)}
                  </Tag>
                ))}
              </Space>
            )}
          </Space>
        )
      },
    },
    {
      title: '',
      key: 'action',
      width: 190,
      fixed: 'right' as const,
      render: (_: unknown, record: KnowledgeGraphNodeItem) => (
        <Space size={4}>
          <Button type="link" size="small" onClick={() => gotoRelationsOf(record)}>
            {t('knowledgegraph.entity.viewRelations')}
          </Button>
          {canWrite && (
            <>
              <Button type="link" size="small" onClick={() => openEdit(record)}>
                {t('knowledgegraph.edit')}
              </Button>
              <Popconfirm title={t('knowledgegraph.entity.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                <Button type="link" size="small" danger>
                  {t('knowledgegraph.delete')}
                </Button>
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ]

  return (
    <>
      {/* 引导：没有实体类型时先去模型页定义，避免"新增"按钮灰着不知道原因 */}
      {canWrite && entityTypes.length === 0 && !loading && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: spacing.md }}
          message={t('knowledgegraph.entity.needTypeTitle')}
          description={t('knowledgegraph.entity.needTypeDesc')}
          action={(
            <Button size="small" type="primary" icon={<ApartmentOutlined />} onClick={() => navigate(`/team/${teamId}/kg/${graphId}/schema`)}>
              {t('knowledgegraph.entity.goSchema')}
            </Button>
          )}
        />
      )}
      <Space style={{ marginBottom: spacing.md }} wrap>
        {canWrite && (
          <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={entityTypes.length === 0}
            title={entityTypes.length === 0 ? t('knowledgegraph.entity.needTypeTitle') : undefined}
            onClick={openCreate}
          >
            {t('knowledgegraph.entity.create')}
          </Button>
        )}
        <Select
          allowClear
          placeholder={t('knowledgegraph.entity.filterType')}
          style={{ width: 180 }}
          value={typeFilter}
          onChange={(value) => {
            setPageNo(1)
            setSearchParams(value != null ? { typeId: String(value) } : {}, { replace: true })
          }}
          options={entityTypes.map((x) => ({ value: Number(x.entityTypeId), label: x.name ?? '' }))}
        />
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
        sticky
        scroll={{ x: 'max-content' }}
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
              onChange={() => form.setFieldValue('props', {})}
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
          <SelectedTypeProperties entityTypes={entityTypes} form={form} typeInfo={typeInfo} />
        </Form>
      </Modal>
    </>
  )
}

/** 弹窗内按所选实体类型渲染属性输入（Form.useWatch 监听 entityTypeId） */
function SelectedTypeProperties({
  entityTypes,
  form,
  typeInfo,
}: {
  entityTypes: KnowledgeGraphEntityTypeItem[]
  form: FormInstance
typeInfo: Map<number, { name: string; color: string; properties: PropertyDef[] }>
}) {
  const { t } = useTranslation()
  const selectedTypeId = Form.useWatch('entityTypeId', form)
  const defs = selectedTypeId != null ? (typeInfo.get(Number(selectedTypeId))?.properties ?? []) : []
  if (defs.length === 0) return null
  return (
    <>
      <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 4, fontSize: 13 }}>
        {t('knowledgegraph.props.instanceSection')}
      </Typography.Title>
      <PropertyInputs properties={defs.filter((x) => x.name)} />
    </>
  )
}
