import { useCallback, useEffect, useMemo, useState } from 'react'
import { DeleteOutlined, EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Button, Form, Input, Modal, Popconfirm, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DataTable, feedback } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import {
  deleteTeamPlugin,
  getTeamPlugins,
  saveTeamDynamicPlugin,
  saveTeamMcpPlugin,
  saveTeamOpenApiPlugin,
  type TeamCustomPluginItem,
  type TeamDynamicPluginItem,
  type TeamPluginItemType,
  type TeamPluginsResponse,
} from '@/api/team-plugin'

const { Text } = Typography

const TYPE_LABEL_KEY: Record<string, string> = {
  mcp: 'plugins.typeMcp',
  openApi: 'plugins.typeOpenapi',
  native: 'plugins.typeNative',
  tool: 'plugins.typeTool',
}

function typeLabel(t: (k: string) => string, type: string | null): string {
  return type ? t(TYPE_LABEL_KEY[type] ?? type) : '-'
}

interface TeamPluginsProps {
  teamId: number
}

export function TeamPlugins({ teamId }: TeamPluginsProps) {
  const { t } = useTranslation()
  const [items, setItems] = useState<TeamPluginItemType[]>([])
  const [loading, setLoading] = useState(true)
  const [res, setRes] = useState<TeamPluginsResponse | null>(null)
  const [dynamicOpen, setDynamicOpen] = useState(false)
  const [mcpOpen, setMcpOpen] = useState(false)
  const [openApiOpen, setOpenApiOpen] = useState(false)
  const [editingDynamic, setEditingDynamic] = useState<TeamDynamicPluginItem | null>(null)
  const [editingCustom, setEditingCustom] = useState<TeamCustomPluginItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [dynamicForm] = Form.useForm()
  const [mcpForm] = Form.useForm()
  const [openApiForm] = Form.useForm()
  const [openApiFileId, setOpenApiFileId] = useState<string | null>(null)

  const canManage = res?.canManage === true

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const data = await getTeamPlugins(teamId)
      setRes(data)
      setItems(data.items ?? [])
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    void load()
  }, [load])

  const openCreateDynamic = () => {
    setEditingDynamic(null)
    dynamicForm.resetFields()
    setDynamicOpen(true)
  }

  const openEditDynamic = (record: TeamDynamicPluginItem) => {
    setEditingDynamic(record)
    dynamicForm.setFieldsValue({
      instanceKey: record.instanceKey ?? '',
      templeteKey: record.templeteKey ?? undefined,
      title: record.title ?? '',
      description: record.description ?? '',
      config: record.config ?? '{}',
    })
    setDynamicOpen(true)
  }

  const handleSaveDynamic = async () => {
    const values = await dynamicForm.validateFields()
    if (!values.templeteKey) return
    setSubmitting(true)
    try {
      if (editingDynamic) {
        // 编辑：实例 key 不可变，更新标题/描述/配置
        await saveTeamDynamicPlugin({
          teamId,
          instanceKey: editingDynamic.instanceKey ?? '',
          templeteKey: editingDynamic.templeteKey ?? '',
          title: values.title,
          description: values.description ?? '',
          config: values.config ?? '{}',
        })
      } else {
        await saveTeamDynamicPlugin({
          teamId,
          instanceKey: values.instanceKey,
          templeteKey: values.templeteKey,
          title: values.title,
          description: values.description ?? '',
          config: values.config ?? '{}',
        })
      }
      feedback.success(t('plugins.saveSuccess'))
      setDynamicOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const openCreateMcp = () => {
    setEditingCustom(null)
    mcpForm.resetFields()
    setMcpOpen(true)
  }

  const openEditMcp = (record: TeamCustomPluginItem) => {
    setEditingCustom(record)
    mcpForm.setFieldsValue({
      name: record.pluginName ?? '',
      title: record.title ?? '',
      description: record.description ?? '',
      serverUrl: record.server ?? '',
    })
    setMcpOpen(true)
  }

  const handleSaveMcp = async () => {
    const values = await mcpForm.validateFields()
    if (!values.name || !values.title || !values.serverUrl) return
    setSubmitting(true)
    try {
      const pluginId = editingCustom?.pluginId ?? null
      const id = await saveTeamMcpPlugin({
        teamId,
        pluginId: pluginId ? String(pluginId) : null,
        name: values.name,
        title: values.title,
        description: values.description ?? '',
        serverUrl: values.serverUrl,
      })
      if (id) feedback.success(t('plugins.saveSuccess'))
      setMcpOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const openCreateOpenApi = () => {
    setEditingCustom(null)
    openApiForm.resetFields()
    setOpenApiFileId(null)
    setOpenApiOpen(true)
  }

  const openEditOpenApi = (record: TeamCustomPluginItem) => {
    setEditingCustom(record)
    openApiForm.setFieldsValue({
      name: record.pluginName ?? '',
      title: record.title ?? '',
      description: record.description ?? '',
    })
    setOpenApiFileId(null)
    setOpenApiOpen(true)
  }

  const handleSaveOpenApi = async () => {
    const values = await openApiForm.validateFields()
    if (!values.name || !values.title) return
    setSubmitting(true)
    try {
      const pluginId = editingCustom?.pluginId ?? null
      const id = await saveTeamOpenApiPlugin({
        teamId,
        pluginId: pluginId ? String(pluginId) : null,
        fileId: Number(openApiFileId ?? 0),
        fileName: '',
        name: values.name,
        title: values.title,
        description: values.description ?? '',
      })
      if (id) feedback.success(t('plugins.saveSuccess'))
      setOpenApiOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (record: TeamPluginItemType) => {
    if (!record.isTeamOwned || !record.pluginId) return
    await deleteTeamPlugin(teamId, String(record.pluginId))
    feedback.success(t('plugins.deletePluginSuccess'))
    void load()
  }

  const teamOwnedCount = useMemo(() => items.filter((i) => i.isTeamOwned).length, [items])

  const columns: TableColumnsType<TeamPluginItemType> = [
    {
      title: t('plugins.colPluginName'),
      dataIndex: 'pluginName',
      width: 170,
      render: (v: string | null) => <Text strong>{v || '-'}</Text>,
    },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 150, ellipsis: true, render: (v: string | null) => v || '-' },
    {
      title: t('plugins.colType'),
      dataIndex: 'type',
      width: 110,
      render: (v: string | null) => <Tag>{typeLabel(t, v)}</Tag>,
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isTeamOwned',
      width: 110,
      render: (v: boolean | null) =>
        v ? <Tag color="green">{t('plugins.teamOwned')}</Tag> : <Tag color="geekblue">{t('plugins.isSystem')}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 120,
      render: (v: string | null) => (v ? <Tag>{v}</Tag> : <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>),
    },
    {
      title: t('plugins.colCreateUser'),
      dataIndex: 'createUserId',
      width: 100,
      render: (v: string | null) => v ?? '-',
    },
    { title: t('plugins.colCreateTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => formatDateTime(v) },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 140,
      fixed: 'right',
      render: (_, record) => (
        <Space size={0}>
          {canManage && record.isTeamOwned && (
            <>
              <Tooltip title={t('plugins.editPlugin')}>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={t('plugins.editPlugin')}
                  onClick={() => {
                    if (record.kind === 'dynamic') openEditDynamic(record as TeamDynamicPluginItem)
                    else if (record.kind === 'custom' && record.type === 'mcp') openEditMcp(record as TeamCustomPluginItem)
                    else if (record.kind === 'custom' && record.type !== 'mcp') openEditOpenApi(record as TeamCustomPluginItem)
                  }}
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
      <DataTable<TeamPluginItemType>
        rowKey="pluginId"
        columns={columns}
        dataSource={items}
        loading={loading}
        pagination={false}
        toolbar={
          canManage ? (
            <Space size={8} wrap>
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreateDynamic}>
                {t('plugins.createDynamicInstance')}
              </Button>
              <Button icon={<PlusOutlined />} onClick={openCreateMcp}>
                {t('plugins.importMcp')}
              </Button>
              <Button icon={<PlusOutlined />} onClick={openCreateOpenApi}>
                {t('plugins.importOpenApi')}
              </Button>
              <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
                {t('plugins.refresh')}
              </Button>
              <Text type="secondary">{t('ds.table.total', { total: items.length })}</Text>
            </Space>
          ) : (
            <Space size={8}>
              <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
                {t('plugins.refresh')}
              </Button>
              <Text type="secondary">
                {t('team.pluginsMemberHint', { count: teamOwnedCount })}
              </Text>
            </Space>
          )
        }
      />

      {/* 动态实例弹窗 */}
      <Modal
        open={dynamicOpen}
        title={editingDynamic ? t('plugins.editDynamicInstance') : t('plugins.createDynamicInstance')}
        onOk={() => void handleSaveDynamic()}
        onCancel={() => setDynamicOpen(false)}
        okText={t('plugins.save')}
        confirmLoading={submitting}
        maskClosable={false}
        destroyOnClose
        width={640}
      >
        <Form form={dynamicForm} layout="vertical">
          <Form.Item
            name="instanceKey"
            label={t('plugins.pluginKey')}
            rules={[
              { required: true, message: t('plugins.pluginKeyRequired') },
              { pattern: /^[a-z_][a-z0-9_]*$/, message: t('plugins.pluginKeyRule') },
              { max: 30, message: t('plugins.pluginKeyMax') },
            ]}
          >
            <Input disabled={Boolean(editingDynamic)} maxLength={30} placeholder={t('plugins.pluginKeyPlaceholder')} />
          </Form.Item>
          <Form.Item name="templeteKey" label={t('plugins.dynamicTemplate')} rules={[{ required: true, message: t('plugins.dynamicTemplateRequired') }]}>
            <Input disabled={Boolean(editingDynamic)} placeholder={t('plugins.dynamicTemplatePlaceholder')} />
          </Form.Item>
          <Form.Item name="title" label={t('plugins.formPluginTitle')} rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}>
            <Input maxLength={30} />
          </Form.Item>
          <Form.Item name="description" label={t('plugins.formDescription')}>
            <Input.TextArea maxLength={255} />
          </Form.Item>
          <Form.Item name="config" label={t('plugins.config')} rules={[{ required: true, message: t('plugins.configRequired') }]}>
            <Input.TextArea rows={6} styles={{ textarea: { fontFamily: 'monospace' } }} placeholder="{ }" />
          </Form.Item>
        </Form>
      </Modal>

      {/* MCP 导入弹窗 */}
      <Modal
        open={mcpOpen}
        title={editingCustom ? t('plugins.editMcpTitle') : t('plugins.importMcpTitle')}
        onOk={() => void handleSaveMcp()}
        onCancel={() => setMcpOpen(false)}
        okText={t('plugins.save')}
        confirmLoading={submitting}
        maskClosable={false}
        destroyOnClose
      >
        <Form form={mcpForm} layout="vertical">
          <Form.Item name="name" label={t('plugins.formPluginName')} rules={[{ required: true, message: t('plugins.pluginNameRequired') }]}>
            <Input maxLength={30} />
          </Form.Item>
          <Form.Item name="title" label={t('plugins.formPluginTitle')} rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}>
            <Input maxLength={20} />
          </Form.Item>
          <Form.Item name="serverUrl" label={t('plugins.formServerUrl')} rules={[{ required: true, message: t('plugins.serverUrlRequired') }]}>
            <Input placeholder={t('plugins.formServerUrlPlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('plugins.formDescription')}>
            <Input.TextArea maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>

      {/* OpenAPI 导入弹窗 */}
      <Modal
        open={openApiOpen}
        title={editingCustom ? t('plugins.editOpenApiTitle') : t('plugins.importOpenApiTitle')}
        onOk={() => void handleSaveOpenApi()}
        onCancel={() => setOpenApiOpen(false)}
        okText={t('plugins.save')}
        confirmLoading={submitting}
        maskClosable={false}
        destroyOnClose
      >
        <Form form={openApiForm} layout="vertical">
          <Form.Item name="name" label={t('plugins.formPluginName')} rules={[{ required: true, message: t('plugins.pluginNameRequired') }]}>
            <Input maxLength={30} />
          </Form.Item>
          <Form.Item name="title" label={t('plugins.formPluginTitle')} rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}>
            <Input maxLength={20} />
          </Form.Item>
          <Form.Item name="description" label={t('plugins.formDescription')}>
            <Input.TextArea maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}


