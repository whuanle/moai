import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Form, Input, Modal, Select, Space, Tag, Tabs, Tooltip } from 'antd'
import type { TableColumnsType } from 'antd'
import { EditOutlined, PlayCircleOutlined, ReloadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router'
import { classifyApi, type PluginClassify } from '@/api/classify'
import {
  pluginApi,
  type ClassifyFilter,
  type PluginKind,
  type PluginManageItem,
  type StaticPluginManageItem,
} from '@/api/plugin'
import { DataTable, feedback, Page } from '@/design-system'
import { useAppStore } from '@/store/app'
import { CustomPluginPanel } from './CustomPluginPanel'
import { DynamicPluginPanel } from './DynamicPluginPanel'
import { PluginRunDrawer } from './components/PluginRunDrawer'

const TAB_KINDS = ['custom', 'dynamic', 'static'] as const

const TYPE_LABEL_KEY: Record<number, string> = {
  0: 'plugins.typeMcp',
  1: 'plugins.typeOpenapi',
  2: 'plugins.typeNative',
  3: 'plugins.typeTool',
}

function typeLabel(t: (k: string) => string, type: number): string {
  return TYPE_LABEL_KEY[type] ? t(TYPE_LABEL_KEY[type]) : String(type)
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function PluginPanel({ kind, classifies }: { kind: PluginKind; classifies: PluginClassify[] }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<StaticPluginManageItem[]>([])
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState<ClassifyFilter>('all')
  const [drawerTarget, setDrawerTarget] = useState<StaticPluginManageItem | null>(null)
  const [editTarget, setEditTarget] = useState<PluginManageItem | null>(null)
  const [editOpen, setEditOpen] = useState(false)
  const [editSubmitting, setEditSubmitting] = useState(false)
  const [form] = Form.useForm<{ title: string; description?: string; classifyId?: number }>()
  const isStatic = kind === 'static'

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await pluginApi.getManagePlugins(kind))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [kind])

  useEffect(() => {
    void load()
  }, [load])

  const openEdit = (record: PluginManageItem) => {
    setEditTarget(record)
    form.setFieldsValue({
      title: record.title ?? '',
      description: record.description ?? '',
      classifyId: record.classifyId || undefined,
    })
    setEditOpen(true)
  }

  const closeEdit = () => {
    setEditOpen(false)
    setEditTarget(null)
    form.resetFields()
  }

  const handleEditSave = async () => {
    const values = await form.validateFields()
    const pluginKey = editTarget?.pluginKey ?? editTarget?.pluginName ?? ''
    if (!pluginKey) return
    setEditSubmitting(true)
    try {
      await pluginApi.saveStaticPlugin({
        pluginKey,
        title: values.title,
        description: values.description ?? '',
        classifyId: values.classifyId ?? 0,
      })
      feedback.success(t('plugins.updateSuccess'))
      closeEdit()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setEditSubmitting(false)
    }
  }

  const filtered = useMemo(() => {
    if (filter === 'all') return items
    if (filter === 'uncategorized') return items.filter((i) => i.classifyId === 0 || !i.classifyId)
    const id = Number(filter)
    return items.filter((i) => i.classifyId === id)
  }, [items, filter])

  const filterTags = useMemo(
    () => [
      { value: 'all' as const, label: t('plugins.classifyAll') },
      { value: 'uncategorized' as const, label: t('plugins.classifyUncategorized') },
      ...classifies.map((c) => ({ value: String(c.classifyId), label: c.name })),
    ],
    [classifies, t],
  )

  const staticColumns: TableColumnsType<StaticPluginManageItem> = [
    { title: t('plugins.colPluginName'), dataIndex: 'pluginName', width: 200 },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 160, ellipsis: true },
    {
      title: t('plugins.colType'),
      dataIndex: 'type',
      width: 100,
      render: (v: number) => <Tag>{typeLabel(t, v)}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 130,
      render: (v: string | null) =>
        v ? <Tag>{v}</Tag> : <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>,
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isSystem',
      width: 90,
      render: (v: boolean) =>
        v ? <Tag color="geekblue">{t('plugins.isSystem')}</Tag> : <Tag>{t('plugins.notSystem')}</Tag>,
    },
    {
      title: t('plugins.colIsPublic'),
      dataIndex: 'isPublic',
      width: 90,
      render: (v: boolean) =>
        v ? <Tag color="green">{t('plugins.isPublic')}</Tag> : <Tag>{t('plugins.notPublic')}</Tag>,
    },
    { title: t('plugins.colCreateUser'), dataIndex: 'createUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colCreateTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => formatDateTime(v) },
    { title: t('plugins.colUpdateUser'), dataIndex: 'updateUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colUpdateTime'), dataIndex: 'updateTime', width: 160, render: (v: string | null) => formatDateTime(v) },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 120,
      fixed: 'right',
      render: (_: unknown, record: StaticPluginManageItem) => (
        <Space size={0}>
          <Tooltip title={t('plugins.run')}>
            <Button
              type="text"
              size="small"
              icon={<PlayCircleOutlined />}
              aria-label={t('plugins.run')}
              onClick={() => setDrawerTarget(record)}
            />
          </Tooltip>
          <Tooltip title={t('plugins.editPlugin')}>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('plugins.editPlugin')}
              onClick={() => openEdit(record)}
            />
          </Tooltip>
        </Space>
      ),
    },
  ]

  const baseColumns: TableColumnsType<StaticPluginManageItem> = [
    { title: t('plugins.colPluginName'), dataIndex: 'pluginName', width: 200 },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 160, ellipsis: true },
    {
      title: t('plugins.colType'),
      dataIndex: 'type',
      width: 100,
      render: (v: number) => <Tag>{typeLabel(t, v)}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 130,
      render: (v: string | null) =>
        v ? <Tag>{v}</Tag> : <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>,
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isSystem',
      width: 90,
      render: (v: boolean) =>
        v ? <Tag color="geekblue">{t('plugins.isSystem')}</Tag> : <Tag>{t('plugins.notSystem')}</Tag>,
    },
    {
      title: t('plugins.colIsPublic'),
      dataIndex: 'isPublic',
      width: 90,
      render: (v: boolean) =>
        v ? <Tag color="green">{t('plugins.isPublic')}</Tag> : <Tag>{t('plugins.notPublic')}</Tag>,
    },
  ]

  const columns = isStatic ? staticColumns : baseColumns

  return (
    <>
      <DataTable<StaticPluginManageItem>
        rowKey={(r) => r.id ?? r.pluginKey ?? ''}
        columns={columns}
        dataSource={filtered}
        loading={loading}
        pagination={false}
        toolbar={
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
            <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
              {t('plugins.refresh')}
            </Button>
            {filterTags.map((item, index) => (
              <span key={item.value} style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                {index > 0 && (
                  <span className="classify-divider" style={{ color: 'rgba(0,0,0,0.25)' }}>
                    |
                  </span>
                )}
                <Tag.CheckableTag checked={filter === item.value} onChange={() => setFilter(item.value)}>
                  {item.label}
                </Tag.CheckableTag>
              </span>
            ))}
          </div>
        }
      />
      {isStatic && (
        <PluginRunDrawer
          open={Boolean(drawerTarget)}
          onClose={() => setDrawerTarget(null)}
          pluginKey={drawerTarget?.pluginKey ?? drawerTarget?.pluginName}
          paramsExample={drawerTarget?.paramsExample}
        />
      )}
      {isStatic && (
        <Modal
          open={editOpen}
          title={t('plugins.editPlugin')}
          onCancel={closeEdit}
          onOk={handleEditSave}
          okText={t('plugins.save')}
          confirmLoading={editSubmitting}
          maskClosable={false}
          destroyOnClose
        >
          <Form form={form} layout="vertical">
            <Form.Item name="title" label={t('plugins.formPluginTitle')} rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}>
              <Input maxLength={50} />
            </Form.Item>
            <Form.Item name="description" label={t('plugins.formDescription')}>
              <Input.TextArea maxLength={255} />
            </Form.Item>
            <Form.Item name="classifyId" label={t('plugins.formClassify')}>
              <Select
                allowClear
                placeholder={t('plugins.formClassifyPlaceholder')}
                options={classifies.map((c) => ({ value: c.classifyId, label: c.name }))}
              />
            </Form.Item>
          </Form>
        </Modal>
      )}
    </>
  )
}

export function Plugins() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const [searchParams, setSearchParams] = useSearchParams()

  const [classifies, setClassifies] = useState<PluginClassify[]>([])

  const loadClassifies = useCallback(async () => {
    try {
      setClassifies(await classifyApi.getPluginClassifies())
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [])

  useEffect(() => {
    if (!isAdmin) return
    void loadClassifies()
  }, [isAdmin, loadClassifies])

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  const tabParam = searchParams.get('tab')
  const activeKey = TAB_KINDS.includes(tabParam as (typeof TAB_KINDS)[number])
    ? (tabParam as (typeof TAB_KINDS)[number])
    : 'custom'

  const handleTabChange = (key: string) => {
    setSearchParams({ tab: key })
  }

  const items = [
    {
      key: 'custom',
      label: t('plugins.tabCustom'),
      children: <CustomPluginPanel classifies={classifies} />,
    },
    { key: 'dynamic', label: t('plugins.tabDynamic'), children: <DynamicPluginPanel classifies={classifies} /> },
    { key: 'static', label: t('plugins.tabStatic'), children: <PluginPanel kind="static" classifies={classifies} /> },
  ]

  return (
    <Page>
      <Tabs activeKey={activeKey} onChange={handleTabChange} items={items} />
    </Page>
  )
}
