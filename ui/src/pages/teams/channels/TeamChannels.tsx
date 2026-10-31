import { useEffect, useState } from 'react'
import {
  Alert,
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
  theme,
  Typography,
} from 'antd'
import type { TableColumnsType } from 'antd'
import {
  CloudServerOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import {
  aichannelApi,
  AI_PROTOCOLS,
  type AIChannelItem,
  type AIModelItem,
  type AIModelMeta,
  type CreateAIChannelPayload,
  type SyncModelCandidate,
} from '@/api/aichannel'
import { getCatalogProviders, type CatalogProvider } from '@/api/models-catalog'
import { AuditCell, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatTokenCount } from '@/utils/format'

const kindColor: Record<string, string> = {
  conversation: 'blue',
  embedding: 'geekblue',
  rerank: 'gold',
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
    case 'rerank':
      return t('models.kindRerank')
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
  { value: 'rerank', labelKey: 'models.kindRerank' },
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
    case 'rerank':
      // 重排序模型无能力开关、无上下文与最大输出.
      return { capabilities: [], contextWindow: false, maxOutput: false }
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
  teamId: number
  channel: AIChannelItem
  models: AIModelItem[]
  loading: boolean
  syncing: boolean
  togglingModelId: string | null
  onSync: (channel: AIChannelItem) => void
  onAddModel: (channelId: string) => void
  onEditModel: (model: AIModelItem) => void
  onDeleteModel: (model: AIModelItem) => void
  onToggleModel: (model: AIModelItem, enabled: boolean) => void
  onRefresh: () => void
}

function ChannelModelsPanel({
  teamId,
  channel,
  models,
  loading,
  syncing,
  togglingModelId,
  onSync,
  onAddModel,
  onEditModel,
  onDeleteModel,
  onToggleModel,
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
      title: t('models.colCreate'),
      key: 'create',
      width: 150,
      render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
    },
    {
      title: t('models.colUpdate'),
      key: 'update',
      width: 150,
      render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} />,
    },
    {
      title: t('models.colEnabled'),
      dataIndex: 'enabled',
      width: 80,
      render: (v: boolean | null | undefined, record) => (
        <Switch
          size="small"
          checked={v === true}
          loading={togglingModelId === record.id}
          onChange={(checked) => onToggleModel(record, checked)}
        />
      ),
    },
    {
      title: t('models.colActions'),
      key: 'actions',
      width: 180,
      render: (_, record) => (
        <Space size={0}>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => onEditModel(record)}>
            {t('models.edit')}
          </Button>
          <Popconfirm
            title={t('models.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void onDeleteModel(record)}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              {t('models.delete')}
            </Button>
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
    runBatch(() => aichannelApi.batchUpdateTeamModel(teamId, selectedKeys.map(String), enabled), 'models.saveSuccess')

  const handleBatchDelete = () =>
    runBatch(() => aichannelApi.batchDeleteTeamModel(teamId, selectedKeys.map(String)), 'models.deleteSuccess')

  return (
    <DataTable<AIModelItem>
      sticky
      rowKey="id"
      columns={columns}
      dataSource={visibleModels}
      loading={loading}
      scroll={{ x: 1560 }}
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
          <Popconfirm
            title={t('models.batchDisableConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleBatchToggle(false)}
          >
            <Button disabled={selectedKeys.length === 0} loading={batchLoading}>
              {t('models.batchDisable')}
            </Button>
          </Popconfirm>
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

/**
 * 团队模型渠道区块：团队维护自己的渠道与模型，仅本团队可用.
 * 与系统模型渠道（管理端）相互独立，模型聚合进团队网关可用列表.
 */
export function TeamChannels({ teamId }: { teamId: number }) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
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

  const [syncModalOpen, setSyncModalOpen] = useState(false)
  const [syncChannel, setSyncChannel] = useState<AIChannelItem | null>(null)
  const [syncCandidates, setSyncCandidates] = useState<SyncModelCandidate[]>([])
  const [syncSelectedIds, setSyncSelectedIds] = useState<React.Key[]>([])
  const [importing, setImporting] = useState(false)
  const [togglingChannelId, setTogglingChannelId] = useState<string | null>(null)
  const [togglingModelId, setTogglingModelId] = useState<string | null>(null)
  const appliedKind = modelKind ?? editingModel?.modelKind ?? 'conversation'
  const kindCaps = kindCapabilitySettings(appliedKind)

  const loadChannels = async () => {
    setChannelLoading(true)
    try {
      setChannels(await aichannelApi.getTeamChannels(teamId))
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
    if (!Number.isFinite(teamId) || teamId <= 0) return
    void loadChannels()
    void loadCatalog()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [teamId])

  const loadModelsForChannel = async (channelId: string) => {
    setLoadingByChannel((cur) => ({ ...cur, [channelId]: true }))
    try {
      const list = await aichannelApi.getTeamModels(teamId, channelId)
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
        await aichannelApi.updateTeamChannel(teamId, editingChannel.id, payload)
      } else {
        await aichannelApi.createTeamChannel(teamId, payload)
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
      await aichannelApi.deleteTeamChannel(teamId, id)
      feedback.success(t('models.deleteSuccess'))
      void loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  // 状态列开关直接启停渠道：按现有字段原样回传，仅翻转 enabled
  const handleToggleChannel = async (record: AIChannelItem, enabled: boolean) => {
    if (!record.id) return
    setTogglingChannelId(record.id)
    try {
      await aichannelApi.updateTeamChannel(teamId, record.id, {
        providerKey: record.providerKey ?? '',
        name: record.name ?? '',
        protocolFamily: record.protocolFamily ?? 'openAIChatCompletions',
        baseUrl: record.baseUrl ?? '',
        apiKey: '',
        description: record.description ?? undefined,
        enabled,
      })
      feedback.success(t('models.saveSuccess'))
      await loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTogglingChannelId(null)
    }
  }

  // 状态列开关直接启停模型：meta 按现有字段原样回传，仅翻转 enabled
  const handleToggleModel = async (record: AIModelItem, enabled: boolean) => {
    if (!record.id || !record.channelId) return
    setTogglingModelId(record.id)
    try {
      await aichannelApi.updateTeamModel(teamId, record.id, {
        channelId: record.channelId,
        meta: metaFromModelForm({
          modelId: record.modelId ?? '',
          name: record.name ?? '',
          family: record.family ?? undefined,
          modelKind: record.modelKind ?? undefined,
          supportsVision: record.supportsVision ?? undefined,
          supportsAttachments: record.supportsAttachments ?? undefined,
          supportsToolCall: record.supportsToolCall ?? undefined,
          supportsStructuredOutput: record.supportsStructuredOutput ?? undefined,
          supportsReasoning: record.supportsReasoning ?? undefined,
          supportsTemperature: record.supportsTemperature ?? undefined,
          contextWindow: record.contextWindow ?? undefined,
          maxOutput: record.maxOutput ?? undefined,
        }),
        enabled,
      })
      feedback.success(t('models.saveSuccess'))
      await loadModelsForChannel(record.channelId)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTogglingModelId(null)
    }
  }

  const handleSync = async (channel: AIChannelItem) => {
    if (!channel.id) return
    setSyncingChannelId(channel.id)
    try {
      const res = await aichannelApi.syncTeamModels(teamId, channel.id)
      const candidates = (res.items ?? []).filter((item) => item.meta != null)
      if (candidates.length === 0) {
        feedback.info(t('models.syncEmpty'))
        return
      }
      // 已识别的排前面，其余按 modelId 排序，便于挑选
      const sorted = [...candidates].sort((a, b) => {
        if (Boolean(a.recognized) !== Boolean(b.recognized)) return a.recognized ? -1 : 1
        return String(a.meta?.modelId ?? '').localeCompare(String(b.meta?.modelId ?? ''))
      })
      setSyncChannel(channel)
      setSyncCandidates(sorted)
      // 默认勾选已识别且未导入过的模型，其余由用户手动挑选
      setSyncSelectedIds(
        sorted
          .filter((item) => item.recognized && !item.exists && item.meta?.modelId != null)
          .map((item) => item.meta?.modelId as React.Key),
      )
      setSyncModalOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSyncingChannelId(null)
    }
  }

  const handleSyncImportOk = async () => {
    const channelId = syncChannel?.id
    if (!channelId || syncSelectedIds.length === 0) return
    const selectedIdSet = new Set(syncSelectedIds.map(String))
    const items: AIModelMeta[] = []
    for (const item of syncCandidates) {
      if (!item.exists && item.meta?.modelId != null && selectedIdSet.has(item.meta.modelId)) {
        items.push(item.meta)
      }
    }
    if (items.length === 0) {
      feedback.warning(t('models.syncSelectRequired'))
      return
    }
    setImporting(true)
    try {
      await aichannelApi.importTeamModels(teamId, { channelId, items })
      feedback.success(t('models.importSuccess', { count: items.length }))
      setSyncModalOpen(false)
      await loadModelsForChannel(channelId)
      await loadChannels()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setImporting(false)
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
        await aichannelApi.updateTeamModel(teamId, editingModel.id, payload)
      } else {
        await aichannelApi.createTeamModel(teamId, payload)
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
      await aichannelApi.deleteTeamModel(teamId, record.id)
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
      title: t('models.colTokens'),
      key: 'tokens',
      width: 150,
      render: (_, record) => (
        <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xxs, minWidth: 0 }}>
          <Typography.Text>{formatTokenCount(Number(record.totalTokens ?? 0))}</Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: token.fontSizeSM }} ellipsis>
            {t('models.tokensDetail', {
              prompt: formatTokenCount(Number(record.promptTokens ?? 0)),
              completion: formatTokenCount(Number(record.completionTokens ?? 0)),
            })}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: t('models.colCreate'),
      key: 'create',
      width: 150,
      render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
    },
    {
      title: t('models.colUpdate'),
      key: 'update',
      width: 150,
      render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} />,
    },
    {
      title: t('models.colEnabled'),
      dataIndex: 'enabled',
      width: 80,
      render: (v: boolean | null | undefined, record) => (
        <Switch
          size="small"
          checked={v === true}
          loading={togglingChannelId === record.id}
          onChange={(checked) => void handleToggleChannel(record, checked)}
        />
      ),
    },
    {
      title: t('models.colActions'),
      key: 'actions',
      width: 240,
      fixed: 'right' as const,
      render: (_, record) => (
        <Space size={0}>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEditChannel(record)}>
            {t('models.edit')}
          </Button>
          <Button
            type="link"
            size="small"
            icon={<SyncOutlined />}
            loading={syncingChannelId === record.id}
            onClick={() => record.id && handleSync(record)}
          >
            {t('models.sync')}
          </Button>
          <Popconfirm
            title={t('models.deleteConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => record.id && handleDeleteChannel(record.id)}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              {t('models.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  const syncColumns: TableColumnsType<SyncModelCandidate> = [
    { title: t('models.colName'), dataIndex: ['meta', 'name'], width: 200, ellipsis: true },
    { title: t('models.colModelId'), dataIndex: ['meta', 'modelId'], width: 240, ellipsis: true },
    {
      title: t('models.colModelKind'),
      dataIndex: 'modelKind',
      width: 110,
      render: (v: string | null | undefined) => (
        <Tag color={kindColor[v ?? ''] ?? 'default'}>{kindLabel(v, t)}</Tag>
      ),
    },
    {
      title: t('models.syncColStatus'),
      key: 'status',
      width: 160,
      render: (_, record) => (
        <Space size={[4, 4]} wrap>
          {record.recognized ? (
            <Tag color="green">{t('models.recognized')}</Tag>
          ) : (
            <Tag>{t('models.unrecognized')}</Tag>
          )}
          {record.exists ? <Tag color="orange">{t('models.syncExists')}</Tag> : null}
        </Space>
      ),
    },
  ]

  const providerOptions = catalog.map((p) => ({ value: p.id, label: `${p.name} (${p.id})` }))

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.lg }}>
      <Alert
        type="info"
        showIcon
        icon={<CloudServerOutlined />}
        message={t('team.channelsHint')}
      />

      <DataTable<AIChannelItem>
        sticky
        rowKey="id"
        columns={channelColumns}
        dataSource={channels}
        loading={channelLoading}
        scroll={{ x: 1400 }}
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
              teamId={teamId}
              channel={record}
              models={modelsByChannel[record.id ?? ''] ?? []}
              loading={loadingByChannel[record.id ?? ''] ?? false}
              syncing={syncingChannelId === record.id}
              togglingModelId={togglingModelId}
              onSync={handleSync}
              onAddModel={openCreateModel}
              onEditModel={openEditModel}
              onDeleteModel={handleDeleteModel}
              onToggleModel={(model, enabled) => void handleToggleModel(model, enabled)}
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

      <Modal
        open={syncModalOpen}
        title={t('models.syncSelectTitle')}
        width={860}
        okText={t('models.syncImport')}
        okButtonProps={{ disabled: syncSelectedIds.length === 0, loading: importing }}
        onOk={handleSyncImportOk}
        onCancel={() => setSyncModalOpen(false)}
        cancelText={t('models.cancel')}
        maskClosable={false}
      >
        <div style={{ marginBottom: spacing.md }}>
          {t('models.syncSelectHint', { total: syncCandidates.length, selected: syncSelectedIds.length })}
        </div>
        <DataTable<SyncModelCandidate>
          rowKey={(record) => record.meta?.modelId ?? ''}
          columns={syncColumns}
          dataSource={syncCandidates}
          scroll={{ y: 420 }}
          pagination={false}
          size="small"
          rowSelection={{
            selectedRowKeys: syncSelectedIds,
            onChange: (keys) => setSyncSelectedIds(keys),
            getCheckboxProps: (record) => ({ disabled: record.exists === true }),
          }}
        />
      </Modal>
    </div>
  )
}
