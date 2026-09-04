import { useEffect, useState } from 'react'
import {
  AutoComplete,
  Button,
  Checkbox,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Tag,
  Tooltip,
} from 'antd'
import type { TableColumnsType } from 'antd'
import {
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import {
  aichannelApi,
  AI_PROTOCOLS,
  type AIChannelItem,
  type AIModelItem,
  type AIModelMeta,
  type CreateAIChannelPayload,
} from '@/api/aichannel'
import { getCatalogProviders, type CatalogProvider } from '@/api/models-catalog'
import { DataTable, feedback, Page } from '@/design-system'
import { useAppStore } from '@/store/app'

const kindColor: Record<string, string> = {
  conversation: 'blue',
  embedding: 'geekblue',
  'image-generation': 'purple',
  transcription: 'cyan',
  'video-generation': 'magenta',
}

const protocolLabelMap: Record<string, string> = {
  openAIChatCompletions: 'OpenAI ChatCompletions',
  openAIResponses: 'OpenAI Responses',
  anthropicMessages: 'Anthropic Messages',
  googleGemini: 'Google Gemini',
}

function protocolLabel(value: string | null | undefined): string {
  if (!value) return '-'
  return protocolLabelMap[value] ?? value
}

/** 统一展示为 YYYY-MM-DD HH:mm，避免各浏览器 locale 差异. */
function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

interface ChannelFormValues {
  providerKey: string
  name: string
  protocolFamily: string
  baseUrl?: string
  apiKey?: string
  enabled?: boolean
}

interface ModelFormValues {
  modelId: string
  name: string
  family?: string
  modelKind?: string
  supportsVision?: boolean
  supportsAttachments?: boolean
  supportsToolCall?: boolean
  supportsStructuredOutput?: boolean
  supportsReasoning?: boolean
  supportsTemperature?: boolean
  contextWindow?: number
  maxOutput?: number
  enabled?: boolean
}

function kindLabel(kind: string | null | undefined, t: (k: string) => string): string {
  switch (kind) {
    case 'embedding':
      return t('models.kindEmbedding')
    case 'image-generation':
      return t('models.kindImage')
    case 'video-generation':
      return t('models.kindVideo')
    case 'transcription':
      return t('models.kindTranscription')
    default:
      return t('models.kindConversation')
  }
}

function capabilityTags(model: AIModelItem, t: (k: string) => string): string[] {
  const tags: string[] = []
  const input = (model.modalitiesInput ?? '')
    .toLowerCase()
    .match(/[a-z]+/g) ?? []
  if (model.supportsAttachments || input.some((m) => ['image', 'audio', 'video', 'pdf'].includes(m))) {
    tags.push(t('models.capMultimodal'))
  }
  if (model.supportsVision) tags.push(t('models.colSupportsVision'))
  if (model.supportsReasoning) tags.push(t('models.capReasoning'))
  if (model.supportsToolCall) tags.push(t('models.capTool'))
  if (model.supportsStructuredOutput) tags.push(t('models.capStructured'))
  if (model.supportsTemperature) tags.push(t('models.capTemp'))
  if (model.contextWindow) tags.push(`${model.contextWindow} ctx`)
  return tags
}

function metaFromModelForm(values: ModelFormValues): AIModelMeta {
  return {
    modelId: values.modelId,
    name: values.name,
    family: values.family,
    modelKind: values.modelKind,
    supportsVision: values.supportsVision === true,
    supportsAttachments: values.supportsAttachments === true,
    supportsToolCall: values.supportsToolCall === true,
    supportsStructuredOutput: values.supportsStructuredOutput === true,
    supportsReasoning: values.supportsReasoning === true,
    supportsTemperature: values.supportsTemperature === true,
    contextWindow: values.contextWindow,
    maxOutput: values.maxOutput,
  }
}

/** 可编辑的模型类型选项（与后端 AIModelKind 枚举一致）. */
const KIND_OPTIONS = [
  { value: 'conversation', labelKey: 'models.kindConversation' },
  { value: 'embedding', labelKey: 'models.kindEmbedding' },
  { value: 'image-generation', labelKey: 'models.kindImage' },
  { value: 'video-generation', labelKey: 'models.kindVideo' },
  { value: 'transcription', labelKey: 'models.kindTranscription' },
] as const

interface KindCapabilitySettings {
  capabilities: string[]
  contextWindow: boolean
  maxOutput: boolean
}

/** 按模型类型决定下方能力开关与上下文/最大输出的显隐规则. */
function kindCapabilitySettings(kind: string | undefined): KindCapabilitySettings {
  switch (kind) {
    case 'embedding':
      // 向量模型只保留最大输出（向量维度），隐藏上下文与其他能力开关.
      return { capabilities: [], contextWindow: false, maxOutput: true }
    case 'image-generation':
    case 'video-generation':
    case 'transcription':
      // 生图/视频/语音模型隐藏能力开关与上下文，仅保留最大输出.
      return { capabilities: [], contextWindow: false, maxOutput: true }
    default:
      // 文本/对话模型展示全部能力开关与上下文/最大输出.
      return {
        capabilities: [
          'supportsVision',
          'supportsAttachments',
          'supportsToolCall',
          'supportsStructuredOutput',
          'supportsReasoning',
          'supportsTemperature',
        ],
        contextWindow: true,
        maxOutput: true,
      }
  }
}

interface ChannelModelsPanelProps {
  channel: AIChannelItem
  models: AIModelItem[]
  loading: boolean
  syncing: boolean
  onSync: (channel: AIChannelItem) => void
  onAddModel: (channelId: string) => void
  onEditModel: (model: AIModelItem) => void
  onDeleteModel: (model: AIModelItem) => void
  onRefresh: () => void
}

function ChannelModelsPanel({
  channel,
  models,
  loading,
  syncing,
  onSync,
  onAddModel,
  onEditModel,
  onDeleteModel,
  onRefresh,
}: ChannelModelsPanelProps) {
  const { t } = useTranslation()
  const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([])
  const [batchLoading, setBatchLoading] = useState(false)
  const [hideUnrecognized, setHideUnrecognized] = useState(true)

  const isRecognized = (model: AIModelItem): boolean =>
    model.enabled === true || Boolean(model.family) || capabilityTags(model, t).length > 0

  const visibleModels = hideUnrecognized ? models.filter(isRecognized) : models

  const columns: TableColumnsType<AIModelItem> = [
    { title: t('models.colName'), dataIndex: 'name', width: 200 },
    { title: t('models.colModelId'), dataIndex: 'modelId', width: 220, ellipsis: true },
    { title: t('models.colFamily'), dataIndex: 'family', width: 120, render: (v: string | null) => v ?? '-' },
    {
      title: t('models.colModelKind'),
      dataIndex: 'modelKind',
      width: 110,
      render: (v: string | null | undefined) => (
        <Tag color={kindColor[v ?? ''] ?? 'default'}>{kindLabel(v, t)}</Tag>
      ),
    },
    {
      title: t('models.colCapabilities'),
      key: 'capabilities',
      render: (_, row) => {
        const tags = capabilityTags(row, t)
        return tags.length > 0 ? (
          <Space size={[4, 4]} wrap>
            {tags.map((label) => (
              <Tag key={label}>{label}</Tag>
            ))}
          </Space>
        ) : (
          '-'
        )
      },
    },
    {
      title: t('models.colEnabled'),
      dataIndex: 'enabled',
      width: 80,
      render: (v: boolean) => (v ? <Tag color="green">{t('models.enabled')}</Tag> : <Tag color="red">{t('models.disabled')}</Tag>),
    },
    {
      title: t('models.colActions'),
      key: 'actions',
      width: 110,
      render: (_, record) => (
        <Space size={0}>
          <Tooltip title={t('models.edit')}>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('models.edit')}
              onClick={() => onEditModel(record)}
            />
          </Tooltip>
          <Popconfirm
            title={t('models.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void onDeleteModel(record)}
          >
            <Tooltip title={t('models.delete')}>
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                aria-label={t('models.delete')}
              />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  const runBatch = async (action: () => Promise<void>, successKey: string) => {
    if (selectedKeys.length === 0) return
    setBatchLoading(true)
    try {
      await action()
      feedback.success(t(successKey))
      setSelectedKeys([])
      onRefresh()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBatchLoading(false)
    }
  }

  const handleBatchToggle = (enabled: boolean) =>
    runBatch(() => aichannelApi.batchUpdateModel(selectedKeys.map(String), enabled), 'models.saveSuccess')

  const handleBatchDelete = () =>
    runBatch(() => aichannelApi.batchDeleteModel(selectedKeys.map(String)), 'models.deleteSuccess')

  return (
    <DataTable<AIModelItem>
      rowKey="id"
      columns={columns}
      dataSource={visibleModels}
      loading={loading}
      scroll={{ x: 1120 }}
      pagination={false}
      size="small"
      rowSelection={{
        selectedRowKeys: selectedKeys,
        onChange: (keys) => setSelectedKeys(keys),
        getCheckboxProps: (record) => ({ disabled: hideUnrecognized && !isRecognized(record) }),
      }}
      toolbar={
        <Space size={12} wrap>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => channel.id && onAddModel(channel.id)}>
            {t('models.addModel')}
          </Button>
          <Button icon={<SyncOutlined />} loading={syncing} onClick={() => onSync(channel)}>
            {t('models.sync')}
          </Button>
          <Button disabled={selectedKeys.length === 0} loading={batchLoading} onClick={() => handleBatchToggle(true)}>
            {t('models.batchEnable')}
          </Button>
          <Button disabled={selectedKeys.length === 0} loading={batchLoading} onClick={() => handleBatchToggle(false)}>
            {t('models.batchDisable')}
          </Button>
          <Popconfirm
            title={t('models.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleBatchDelete()}
          >
            <Button danger disabled={selectedKeys.length === 0} loading={batchLoading}>
              {t('models.batchDelete')}
            </Button>
          </Popconfirm>
          <Checkbox
            checked={hideUnrecognized}
            onChange={(e) => setHideUnrecognized(e.target.checked)}
          >
            {t('models.hideUnrecognized')}
          </Checkbox>
        </Space>
      }
      onRefresh={onRefresh}
      refreshLoading={loading}
    />
  )
}

export function Models() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const [channelForm] = Form.useForm<ChannelFormValues>()
  const [modelForm] = Form.useForm<ModelFormValues>()

  const [channelLoading, setChannelLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [channels, setChannels] = useState<AIChannelItem[]>([])
  const [catalog, setCatalog] = useState<CatalogProvider[]>([])

  const [expandedKeys, setExpandedKeys] = useState<React.Key[]>([])
  const [modelsByChannel, setModelsByChannel] = useState<Record<string, AIModelItem[]>>({})
  const [loadingByChannel, setLoadingByChannel] = useState<Record<string, boolean>>({})
  const [syncingChannelId, setSyncingChannelId] = useState<string | null>(null)

  const [channelModalOpen, setChannelModalOpen] = useState(false)
  const [editingChannel, setEditingChannel] = useState<AIChannelItem | null>(null)
  const [modelModalOpen, setModelModalOpen] = useState(false)
  const [editingModel, setEditingModel] = useState<AIModelItem | null>(null)
  const [modelChannelId, setModelChannelId] = useState<string | null>(null)
  const [modelKind, setModelKind] = useState<string | undefined>()
  const appliedKind = modelKind ?? editingModel?.modelKind ?? 'conversation'
  const kindCaps = kindCapabilitySettings(appliedKind)

  const loadChannels = async () => {
    setChannelLoading(true)
    try {
      setChannels(await aichannelApi.getChannels())
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setChannelLoading(false)
    }
  }

  const loadCatalog = async () => {
    try {
      setCatalog(await getCatalogProviders())
    } catch {
      // 目录加载失败时记录为空，不阻塞页面
      setCatalog([])
    }
  }

  useEffect(() => {
    if (!isAdmin) return
    void loadChannels()
    void loadCatalog()
  }, [isAdmin])

  const loadModelsForChannel = async (channelId: string) => {
    setLoadingByChannel((cur) => ({ ...cur, [channelId]: true }))
    try {
      const list = await aichannelApi.getModels(channelId)
      setModelsByChannel((cur) => ({ ...cur, [channelId]: list }))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoadingByChannel((cur) => ({ ...cur, [channelId]: false }))
    }
  }

  const openCreateChannel = () => {
    setEditingChannel(null)
    channelForm.resetFields()
    channelForm.setFieldsValue({
      providerKey: '',
      name: '',
      protocolFamily: 'openAIChatCompletions',
      enabled: true,
    })
    setChannelModalOpen(true)
  }

  const openEditChannel = (record: AIChannelItem) => {
    setEditingChannel(record)
    channelForm.setFieldsValue({
      providerKey: record.providerKey ?? '',
      name: record.name ?? '',
      protocolFamily: record.protocolFamily ?? 'openAIChatCompletions',
      baseUrl: record.baseUrl ?? '',
      apiKey: '',
      enabled: record.enabled ?? true,
    })
    setChannelModalOpen(true)
  }

  const applyProviderDefaults = (providerKey: string) => {
    const chosen = catalog.find((p) => p.id === providerKey)
    if (!chosen) return
    channelForm.setFieldsValue({
      name: chosen.name,
      baseUrl: chosen.baseUrl,
      protocolFamily: chosen.protocol,
    })
  }

  const handleProviderSelect = (value: string) => {
    channelForm.setFieldsValue({ providerKey: value })
    applyProviderDefaults(value)
  }

  const handleProviderChange = (value: string) => {
    channelForm.setFieldsValue({ providerKey: value })
    applyProviderDefaults(value)
  }

  const handleChannelOk = async () => {
    const values = await channelForm.validateFields()
    const providerKey = values.providerKey.trim()
    if (!providerKey) {
      feedback.error(t('models.providerKeyRequired'))
      return
    }
    const payload: CreateAIChannelPayload = {
      providerKey,
      name: values.name,
      protocolFamily: values.protocolFamily,
      baseUrl: values.baseUrl ?? '',
      apiKey: values.apiKey ?? '',
      enabled: values.enabled !== false,
    }
    setSubmitting(true)
    try {
      if (editingChannel?.id) {
        await aichannelApi.updateChannel(editingChannel.id, payload)
      } else {
        await aichannelApi.createChannel(payload)
      }
      feedback.success(t('models.saveSuccess'))
      setChannelModalOpen(false)
      void loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDeleteChannel = async (id: string) => {
    try {
      await aichannelApi.deleteChannel(id)
      feedback.success(t('models.deleteSuccess'))
      void loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleSync = async (channel: AIChannelItem) => {
    if (!channel.id) return
    setSyncingChannelId(channel.id)
    try {
      const res = await aichannelApi.syncModel(channel.id)
      const added = res.added ?? 0
      const total = res.total ?? 0
      if (added > 0) {
        feedback.success(t('models.syncSuccess', { count: added }))
      } else {
        feedback.success(t('models.syncNoChange', { count: total }))
      }
      await loadModelsForChannel(channel.id)
      await loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSyncingChannelId(null)
    }
  }

  const openCreateModel = (channelId: string) => {
    setModelChannelId(channelId)
    setEditingModel(null)
    modelForm.resetFields()
    modelForm.setFieldsValue({ enabled: true, modelKind: 'conversation' })
    setModelKind('conversation')
    setModelModalOpen(true)
  }

  const openEditModel = (record: AIModelItem) => {
    setModelChannelId(record.channelId ?? null)
    setEditingModel(record)
    setModelKind(record.modelKind ?? 'conversation')
    modelForm.setFieldsValue({
      modelId: record.modelId ?? '',
      name: record.name ?? '',
      family: record.family ?? '',
      modelKind: record.modelKind ?? 'conversation',
      supportsVision: record.supportsVision ?? undefined,
      supportsAttachments: record.supportsAttachments ?? undefined,
      supportsToolCall: record.supportsToolCall ?? undefined,
      supportsStructuredOutput: record.supportsStructuredOutput ?? undefined,
      supportsReasoning: record.supportsReasoning ?? undefined,
      supportsTemperature: record.supportsTemperature ?? undefined,
      contextWindow: record.contextWindow ?? undefined,
      maxOutput: record.maxOutput ?? undefined,
      enabled: record.enabled ?? true,
    })
    setModelModalOpen(true)
  }

  const handleModelOk = async () => {
    const values = await modelForm.validateFields()
    const channelId = editingModel?.channelId ?? modelChannelId
    if (!channelId) {
      feedback.error(t('models.selectChannelFirst'))
      return
    }
    const payload = {
      channelId,
      meta: metaFromModelForm(values),
      enabled: values.enabled !== false,
    }
    setSubmitting(true)
    try {
      if (editingModel?.id) {
        await aichannelApi.updateModel(editingModel.id, payload)
      } else {
        await aichannelApi.createModel(payload)
      }
      feedback.success(t('models.saveSuccess'))
      setModelModalOpen(false)
      await loadModelsForChannel(channelId)
      await loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDeleteModel = async (record: AIModelItem) => {
    if (!record.id) return
    try {
      await aichannelApi.deleteModel(record.id)
      feedback.success(t('models.deleteSuccess'))
      if (record.channelId) {
        await loadModelsForChannel(record.channelId)
      }
      await loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const channelColumns: TableColumnsType<AIChannelItem> = [
    { title: t('models.colName'), dataIndex: 'name', width: 160 },
    { title: t('models.colProviderKey'), dataIndex: 'providerKey', width: 160, ellipsis: true },
    {
      title: t('models.colProtocol'),
      dataIndex: 'protocolFamily',
      width: 190,
      render: (v: string | null) => (
        <Tag color="geekblue">{protocolLabel(v)}</Tag>
      ),
    },
    { title: t('models.colModelCount'), dataIndex: 'modelCount', width: 90, align: 'center' as const },
    {
      title: t('models.colCreateUser'),
      dataIndex: 'createUserName',
      width: 120,
      render: (v: string | null) => v || '-',
    },
    {
      title: t('models.colCreateTime'),
      dataIndex: 'createTime',
      width: 160,
      render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
    },
    {
      title: t('models.colUpdateUser'),
      dataIndex: 'updateUserName',
      width: 120,
      render: (v: string | null) => v || '-',
    },
    {
      title: t('models.colUpdateTime'),
      dataIndex: 'updateTime',
      width: 160,
      render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
    },
    {
      title: t('models.colEnabled'),
      dataIndex: 'enabled',
      width: 80,
      render: (v: boolean) => (v ? <Tag color="green">{t('models.enabled')}</Tag> : <Tag color="red">{t('models.disabled')}</Tag>),
    },
    {
      title: t('models.colActions'),
      key: 'actions',
      width: 110,
      fixed: 'right' as const,
      render: (_, record) => (
        <Space size={0}>
          <Tooltip title={t('models.edit')}>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('models.edit')}
              onClick={() => openEditChannel(record)}
            />
          </Tooltip>
          <Tooltip title={t('models.sync')}>
            <Button
              type="text"
              size="small"
              icon={<SyncOutlined />}
              aria-label={t('models.sync')}
              loading={syncingChannelId === record.id}
              onClick={() => record.id && handleSync(record)}
            />
          </Tooltip>
          <Popconfirm
            title={t('models.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => record.id && handleDeleteChannel(record.id)}
          >
            <Tooltip title={t('models.delete')}>
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                aria-label={t('models.delete')}
              />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  const providerOptions = catalog.map((p) => ({ value: p.id, label: `${p.name} (${p.id})` }))

  return (
    <Page>
      <DataTable<AIChannelItem>
        rowKey="id"
        columns={channelColumns}
        dataSource={channels}
        loading={channelLoading}
        scroll={{ x: 1620 }}
        pagination={false}
        expandable={{
          expandedRowKeys: expandedKeys,
          onExpand: (expanded, record) => {
            setExpandedKeys(expanded
              ? [...new Set([...expandedKeys, record.id as React.Key])]
              : expandedKeys.filter((k) => k !== record.id))
            if (expanded && record.id && !modelsByChannel[record.id]) {
              void loadModelsForChannel(record.id)
            }
          },
          expandedRowRender: (record) => (
            <ChannelModelsPanel
              channel={record}
              models={modelsByChannel[record.id ?? ''] ?? []}
              loading={loadingByChannel[record.id ?? ''] ?? false}
              syncing={syncingChannelId === record.id}
              onSync={handleSync}
              onAddModel={openCreateModel}
              onEditModel={openEditModel}
              onDeleteModel={handleDeleteModel}
              onRefresh={() => {
                if (record.id) {
                  void loadModelsForChannel(record.id)
                  void loadChannels()
                }
              }}
            />
          ),
        }}
        toolbar={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreateChannel}>
            {t('models.addChannel')}
          </Button>
        }
        onRefresh={loadChannels}
        refreshLoading={channelLoading}
      />

      <Modal
        open={channelModalOpen}
        title={editingChannel ? t('models.editChannel') : t('models.addChannel')}
        onOk={handleChannelOk}
        onCancel={() => setChannelModalOpen(false)}
        okText={t('models.save')}
        cancelText={t('models.cancel')}
        confirmLoading={submitting}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={channelForm} layout="vertical">
          <Form.Item
            name="providerKey"
            label={t('models.colProviderKey')}
            rules={[{ required: true, message: t('models.providerKeyRequired') }]}
          >
            <AutoComplete
              options={providerOptions}
              placeholder={t('models.providerPlaceholder')}
              disabled={Boolean(editingChannel)}
              onSelect={handleProviderSelect}
              onChange={handleProviderChange}
              filterOption={(input, option) => {
                const value = String(option?.value ?? '').toLowerCase()
                const label = (typeof option?.label === 'string' ? option.label : '').toLowerCase()
                return value.includes(input.toLowerCase()) || label.includes(input.toLowerCase())
              }}
            />
          </Form.Item>
          <Form.Item
            name="name"
            label={t('models.colName')}
            rules={[{ required: true, message: t('models.nameRequired') }]}
          >
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item
            name="protocolFamily"
            label={t('models.colProtocol')}
            rules={[{ required: true, message: t('models.protocolRequired') }]}
          >
            <Select options={AI_PROTOCOLS.map((v) => ({ value: v, label: protocolLabel(v) }))} />
          </Form.Item>
          <Form.Item name="baseUrl" label={t('models.baseUrl')}>
            <Input placeholder="https://..." />
          </Form.Item>
          <Form.Item name="apiKey" label={t('models.apiKey')} extra={editingChannel ? t('models.apiKeyKeep') : undefined}>
            <Input.Password />
          </Form.Item>
          <Form.Item name="enabled" label={t('models.enabled')} valuePropName="checked" initialValue>
            <Switch />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={modelModalOpen}
        title={editingModel ? t('models.editModel') : t('models.addModel')}
        onOk={handleModelOk}
        onCancel={() => setModelModalOpen(false)}
        okText={t('models.save')}
        cancelText={t('models.cancel')}
        confirmLoading={submitting}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={modelForm} layout="vertical">
          <Form.Item
            name="modelId"
            label={t('models.colModelId')}
            rules={[{ required: true, message: t('models.modelIdRequired') }]}
          >
            <Input maxLength={200} placeholder="gpt-4o" />
          </Form.Item>
          <Form.Item
            name="name"
            label={t('models.colName')}
            rules={[{ required: true, message: t('models.nameRequired') }]}
          >
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="family" label={t('models.colFamily')}>
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item name="modelKind" label={t('models.colModelKind')}>
            <Select
              options={KIND_OPTIONS.map((k) => ({ value: k.value, label: t(k.labelKey) }))}
              onChange={(v) => setModelKind(v as string)}
            />
          </Form.Item>
          {kindCaps.capabilities.length > 0 && (
            <Space size={24} wrap>
              {kindCaps.capabilities.map((cap) => (
                <Form.Item key={cap} name={cap} label={t(`models.col${cap[0].toUpperCase()}${cap.slice(1)}`)} valuePropName="checked">
                  <Switch />
                </Form.Item>
              ))}
            </Space>
          )}
          <Space size={24}>
            {kindCaps.contextWindow && (
              <Form.Item name="contextWindow" label={t('models.colContextWindow')}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </Form.Item>
            )}
            {kindCaps.maxOutput && (
              <Form.Item name="maxOutput" label={t('models.colMaxOutput')}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </Form.Item>
            )}
          </Space>
          <Form.Item name="enabled" label={t('models.enabled')} valuePropName="checked" initialValue>
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
