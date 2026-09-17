import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import { Button, Form, Input, Modal, Popconfirm, Select, Space, Tag, Tooltip } from 'antd'
import type { TableColumnsType } from 'antd'
import Editor from '@monaco-editor/react'
import { useTranslation } from 'react-i18next'
import type { PluginClassify } from '@/api/classify'
import type { DynamicPluginTemplate } from '@/api/plugin'
import {
  deleteTeamPlugin,
  getTeamDynamicTemplates,
  runTeamPlugin,
  saveTeamDynamicPlugin,
  type TeamDynamicPluginItem,
} from '@/api/team-plugin'
import { DataTable, feedback } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import { PluginRunDrawer } from '@/pages/plugins/components/PluginRunDrawer'

interface DynamicFormValues {
  instanceKey: string
  templeteKey?: string
  title: string
  description?: string
  classifyId?: number
  config: string
}

interface TeamDynamicPluginPanelProps {
  teamId: number
  items: TeamDynamicPluginItem[]
  loading: boolean
  canManage: boolean
  classifies: PluginClassify[]
  reload: () => void
}

export function TeamDynamicPluginPanel({
  teamId,
  items,
  loading,
  canManage,
  classifies,
  reload,
}: TeamDynamicPluginPanelProps) {
  const { t } = useTranslation()
  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [modalOpen, setModalOpen] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [editing, setEditing] = useState<TeamDynamicPluginItem | null>(null)
  const [drawerTarget, setDrawerTarget] = useState<TeamDynamicPluginItem | null>(null)
  const [filter, setFilter] = useState('all')
  const [form] = Form.useForm<DynamicFormValues>()

  const loadTemplates = useCallback(async () => {
    try {
      const res = await getTeamDynamicTemplates(teamId)
      setTemplates((res.items ?? []).filter((i) => i.isDynamic === true))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [teamId])

  useEffect(() => {
    void loadTemplates()
  }, [loadTemplates])

  const reset = () => {
    setEditing(null)
    form.resetFields()
  }

  const openCreate = () => {
    reset()
    setModalOpen(true)
  }

  const openEdit = (record: TeamDynamicPluginItem) => {
    setEditing(record)
    form.setFieldsValue({
      instanceKey: record.instanceKey ?? record.pluginName ?? '',
      templeteKey: record.templeteKey ?? undefined,
      title: record.title ?? '',
      description: record.description ?? '',
      classifyId: record.classifyId || undefined,
      config: record.config ?? '{}',
    })
    setModalOpen(true)
  }

  const isDuplicateKey = (instanceKey: string) => items.some((i) => (i.instanceKey ?? i.pluginName) === instanceKey)

  const handleSubmit = async () => {
    const values = await form.validateFields()
    const instanceKey = (values.instanceKey ?? '').trim()
    const templeteKey = editing ? editing.templeteKey : values.templeteKey
    if (!instanceKey || !templeteKey) return

    if (!editing && isDuplicateKey(instanceKey)) {
      feedback.error(t('plugins.dynamicKeyExists'))
      return
    }

    setSubmitting(true)
    try {
      await saveTeamDynamicPlugin({
        teamId,
        instanceKey,
        templeteKey,
        title: values.title,
        description: values.description ?? '',
        config: values.config ?? '{}',
        classifyId: values.classifyId ?? 0,
      })
      feedback.success(t('plugins.updateSuccess'))
      reset()
      setModalOpen(false)
      reload()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (record: TeamDynamicPluginItem) => {
    if (!record.pluginId) return
    await deleteTeamPlugin(teamId, String(record.pluginId))
    feedback.success(t('plugins.deletePluginSuccess'))
    if (editing?.pluginId === record.pluginId) reset()
    reload()
  }

  const filtered = useMemo(() => {
    if (filter === 'all') return items
    if (filter === 'uncategorized') return items.filter((i) => (i.classifyId ?? 0) === 0)
    const id = Number(filter)
    return items.filter((i) => i.classifyId === id)
  }, [items, filter])

  const filterTags = useMemo(
    () => [
      { value: 'all', label: t('plugins.classifyAll') },
      { value: 'uncategorized', label: t('plugins.classifyUncategorized') },
      ...classifies.map((c) => ({ value: String(c.classifyId), label: c.name })),
    ],
    [classifies, t],
  )

  const columns: TableColumnsType<TeamDynamicPluginItem> = [
    { title: t('plugins.colPluginName'), dataIndex: 'pluginName', width: 180 },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 150, ellipsis: true },
    {
      title: t('plugins.dynamicTemplate'),
      dataIndex: 'templeteKey',
      width: 160,
      render: (v: string | null) => (v ? <Tag color="blue">{v}</Tag> : '-'),
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isTeamOwned',
      width: 100,
      render: (v: boolean | null) =>
        v ? <Tag color="green">{t('plugins.teamOwned')}</Tag> : <Tag color="geekblue">{t('plugins.isSystem')}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 130,
      render: (v: string | null) =>
        v ? <Tag>{v}</Tag> : <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>,
    },
    { title: t('plugins.colCreateUser'), dataIndex: 'createUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colCreateTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => formatDateTime(v) },
    { title: t('plugins.colUpdateUser'), dataIndex: 'updateUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colUpdateTime'), dataIndex: 'updateTime', width: 160, render: (v: string | null) => formatDateTime(v) },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 150,
      fixed: 'right',
      render: (_, record) => (
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
          {canManage && record.isTeamOwned && (
            <>
              <Tooltip title={t('plugins.editPlugin')}>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={t('plugins.editPlugin')}
                  onClick={() => openEdit(record)}
                />
              </Tooltip>
              <Popconfirm
                title={t('plugins.deletePluginConfirm')}
                okButtonProps={{ danger: true }}
                onConfirm={() => void handleDelete(record)}
              >
                <Tooltip title={t('plugins.deletePlugin')}>
                  <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('plugins.deletePlugin')} />
                </Tooltip>
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ]

  return (
    <>
      <DataTable<TeamDynamicPluginItem>
        rowKey={(r) => String(r.pluginId ?? r.pluginName ?? '')}
        columns={columns}
        dataSource={filtered}
        loading={loading}
        pagination={false}
        scroll={{ x: 1300 }}
        toolbar={
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
            {canManage && (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('plugins.createDynamicInstance')}
              </Button>
            )}
            <Button icon={<ReloadOutlined />} onClick={reload} loading={loading}>
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

      {drawerTarget && (
        <PluginRunDrawer
          open={Boolean(drawerTarget)}
          onClose={() => setDrawerTarget(null)}
          pluginKey={drawerTarget.pluginName ?? drawerTarget.instanceKey}
          paramsExample={drawerTarget.paramsExample}
          runPlugin={(payload) => runTeamPlugin({ teamId, ...payload })}
        />
      )}

      <Modal
        open={modalOpen}
        title={editing ? t('plugins.editDynamicInstance') : t('plugins.createDynamicInstance')}
        onCancel={() => {
          reset()
          setModalOpen(false)
        }}
        onOk={handleSubmit}
        okText={t('plugins.save')}
        confirmLoading={submitting}
        maskClosable={false}
        destroyOnClose
        width={640}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="instanceKey"
            label={t('plugins.pluginKey')}
            rules={[
              { required: true, message: t('plugins.pluginKeyRequired') },
              { pattern: /^[a-z_][a-z0-9_]*$/, message: t('plugins.pluginKeyRule') },
              { max: 30, message: t('plugins.pluginKeyMax') },
            ]}
          >
            <Input disabled={Boolean(editing)} maxLength={30} placeholder={t('plugins.pluginKeyPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="templeteKey"
            label={t('plugins.dynamicTemplate')}
            rules={[{ required: true, message: t('plugins.dynamicTemplateRequired') }]}
          >
            <Select
              disabled={Boolean(editing)}
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('plugins.dynamicTemplatePlaceholder')}
              options={templates.map((tp) => ({ value: tp.key, label: `${tp.name} (${tp.key})` }))}
              onChange={(v) => {
                const tp = templates.find((x) => x.key === v)
                if (tp) form.setFieldValue('config', tp.configExample ?? '{}')
              }}
            />
          </Form.Item>
          <Form.Item name="title" label={t('plugins.formPluginTitle')} rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}>
            <Input maxLength={30} />
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
          <Form.Item name="config" label={t('plugins.config')} rules={[{ required: true, message: t('plugins.configRequired') }]}>
            <Editor
              height="200px"
              language="json"
              value={form.getFieldValue('config') ?? '{}'}
              onChange={(v) => form.setFieldValue('config', v ?? '{}')}
              options={{ minimap: { enabled: false }, fontSize: 14 }}
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
