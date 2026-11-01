import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  BellOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  SendOutlined,
} from '@ant-design/icons'
import { Badge, Button, Form, Input, Modal, Popconfirm, Select, Space, Switch, Tabs, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router'
import { AuditCell, DataTable, Page, QueryBar, feedback } from '@/design-system'
import { controlHeight, spacing, tableCardChromeHeight } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { formatDateTime } from '@/utils/datetime'
import {
  broadcastNotification,
  createNotificationChannel,
  deleteNotificationChannel,
  getNotificationChannels,
  getNotificationLogs,
  testNotificationChannel,
  updateNotificationChannel,
  type NotificationChannelItem,
  type NotificationChannelTypeValue,
  type NotificationLevelValue,
  type NotificationLogItem,
  type NotificationSendStatusValue,
} from '@/api/notification'

const { Text } = Typography

const pageVerticalPadding = spacing.lg * 2
const tabsBarHeight = 46
const queryBarHeight = controlHeight + spacing.md
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const panelHeight = `calc(100vh - ${pageVerticalPadding + tabsBarHeight}px)`
const panelTableScrollY = `calc(100vh - ${pageVerticalPadding + tabsBarHeight + queryBarHeight + tableHeaderHeight + tablePaginationHeight + tableCardChromeHeight}px)`

const CHANNEL_TYPES: NotificationChannelTypeValue[] = [
  'feishuWebhook',
  'dingtalkWebhook',
  'wecomWebhook',
  'telegramBot',
  'webhook',
  'smtp',
  'inSite',
]

/** 渠道类型 → Tag 颜色 */
const TYPE_COLORS: Record<NotificationChannelTypeValue, string> = {
  feishuWebhook: 'blue',
  dingtalkWebhook: 'geekblue',
  wecomWebhook: 'green',
  telegramBot: 'cyan',
  webhook: 'purple',
  smtp: 'orange',
  inSite: 'default',
}

/** 通知中心（仅管理员）：Tab 隔离 渠道管理 / 发送记录 */
export function NotificationCenter() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = searchParams.get('tab') ?? 'channels'

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  const handleTabChange = (key: string) => {
    setSearchParams(key === 'channels' ? {} : { tab: key }, { replace: true })
  }

  return (
    <Page>
      <Tabs
        activeKey={activeTab}
        onChange={handleTabChange}
        style={{ minHeight: panelHeight }}
        items={[
          { key: 'channels', label: t('notification.tabChannels'), children: <ChannelsPanel /> },
          { key: 'logs', label: t('notification.tabLogs'), children: <LogsPanel /> },
        ]}
      />
    </Page>
  )
}

type ChannelFormValues = {
  name: string
  type: NotificationChannelTypeValue
  description: string
  enabled: boolean
  webhookUrl?: string
  secret?: string
  baseUrl?: string
  botToken?: string
  chatId?: string
  parseMode?: string
  url?: string
  headersJson?: string
  bodyTemplate?: string
  recipients?: string[]
  subjectPrefix?: string
  insiteUserIds?: string
  insiteToAdmins?: boolean
}

/** 按渠道类型把表单值组装为配置对象（非法输入已在组装前校验） */
function buildConfig(type: NotificationChannelTypeValue, v: ChannelFormValues): string {
  switch (type) {
    case 'feishuWebhook':
      return JSON.stringify({ webhookUrl: v.webhookUrl?.trim() ?? '', secret: v.secret?.trim() ?? '' })
    case 'dingtalkWebhook':
      return JSON.stringify({ webhookUrl: v.webhookUrl?.trim() ?? '', secret: v.secret?.trim() ?? '' })
    case 'wecomWebhook':
      return JSON.stringify({ webhookUrl: v.webhookUrl?.trim() ?? '' })
    case 'telegramBot':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://api.telegram.org', botToken: v.botToken?.trim() ?? '', chatId: v.chatId?.trim() ?? '', parseMode: v.parseMode ?? '' })
    case 'webhook': {
      let headers: Record<string, string> = {}
      const raw = v.headersJson?.trim()
      if (raw) headers = JSON.parse(raw) as Record<string, string>
      return JSON.stringify({ url: v.url?.trim() ?? '', headers, bodyTemplate: v.bodyTemplate ?? '' })
    }
    case 'smtp':
      return JSON.stringify({ recipients: v.recipients ?? [], subjectPrefix: v.subjectPrefix?.trim() ?? '' })
    case 'inSite': {
      const userIds = (v.insiteUserIds ?? '')
        .split(/[,，\s]+/)
        .filter(Boolean)
        .map(Number)
        .filter((n) => Number.isFinite(n) && n > 0)
      return JSON.stringify({ userIds, toAdmins: v.insiteToAdmins === true })
    }
    default:
      return '{}'
  }
}

/** 渠道管理面板 */
function ChannelsPanel() {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const formType = Form.useWatch('type', form)
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<NotificationChannelItem[]>([])
  const [search, setSearch] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<NotificationChannelItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [testing, setTesting] = useState(false)
  const [testId, setTestId] = useState<string | null>(null)
  const [broadcastOpen, setBroadcastOpen] = useState(false)
  const [broadcastForm] = Form.useForm()
  const [broadcasting, setBroadcasting] = useState(false)

  const load = useCallback(async (keyword = search) => {
    setLoading(true)
    try {
      setItems(await getNotificationChannels(keyword.trim() || undefined))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [search])

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const typeLabel = (type: NotificationChannelTypeValue | undefined) => (type ? t(`notification.type.${type}`) : '-')

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldsValue({ type: 'feishuWebhook', enabled: true, parseMode: '', baseUrl: '' })
    setModalOpen(true)
  }

  const openEdit = (record: NotificationChannelItem) => {
    setEditing(record)
    form.resetFields()
    const values: Partial<ChannelFormValues> = {
      name: record.name,
      type: record.type,
      description: record.description,
      enabled: record.enabled !== false,
    }
    try {
      const config = JSON.parse(record.config ?? '{}') as Record<string, unknown>
      if (record.type === 'smtp') {
        values.recipients = (config.recipients as string[]) ?? []
        values.subjectPrefix = (config.subjectPrefix as string) ?? ''
      } else if (record.type === 'inSite') {
        values.insiteUserIds = ((config.userIds as number[]) ?? []).join(',')
        values.insiteToAdmins = config.toAdmins === true
      } else {
        values.webhookUrl = (config.webhookUrl as string) ?? ''
        values.secret = (config.secret as string) ?? ''
        values.baseUrl = (config.baseUrl as string) ?? ''
        values.botToken = (config.botToken as string) ?? ''
        values.chatId = (config.chatId as string) ?? ''
        values.parseMode = (config.parseMode as string) ?? ''
        values.url = (config.url as string) ?? ''
        values.headersJson = Object.keys((config.headers as Record<string, string>) ?? {}).length > 0 ? JSON.stringify(config.headers, null, 2) : ''
        values.bodyTemplate = (config.bodyTemplate as string) ?? ''
      }
    } catch {
      // 配置 JSON 异常时仅回显基础字段
    }
    form.setFieldsValue(values)
    setModalOpen(true)
  }

  /** 组装配置并做基础前端校验，返回 null 表示校验失败 */
  const buildValidatedConfig = (type: NotificationChannelTypeValue, v: ChannelFormValues): string | null => {
    const requireText = (value: string | undefined, key: string) => {
      if (!value || value.trim().length === 0) {
        feedback.error(t(key))
        return false
      }
      return true
    }
    switch (type) {
      case 'feishuWebhook':
      case 'dingtalkWebhook':
      case 'wecomWebhook':
        if (!requireText(v.webhookUrl, 'notification.error.webhookUrlRequired')) return null
        break
      case 'telegramBot':
        if (!requireText(v.botToken, 'notification.error.botTokenRequired')) return null
        if (!requireText(v.chatId, 'notification.error.chatIdRequired')) return null
        break
      case 'webhook': {
        if (!requireText(v.url, 'notification.error.urlRequired')) return null
        const raw = v.headersJson?.trim()
        if (raw) {
          try {
            const parsed = JSON.parse(raw) as Record<string, unknown>
            if (typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('not object')
          } catch {
            feedback.error(t('notification.error.headersInvalid'))
            return null
          }
        }
        break
      }
      case 'smtp':
        if (!v.recipients || v.recipients.length === 0) {
          feedback.error(t('notification.error.recipientsRequired'))
          return null
        }
        break
      case 'inSite':
        if ((!v.insiteUserIds || v.insiteUserIds.trim().length === 0) && v.insiteToAdmins !== true) {
          feedback.error(t('notification.error.inSiteTargetRequired'))
          return null
        }
        break
      default:
        break
    }
    return buildConfig(type, v)
  }

  const handleSave = async () => {
    const values = (await form.validateFields()) as ChannelFormValues
    const config = buildValidatedConfig(values.type, values)
    if (config === null) return
    setSubmitting(true)
    try {
      const payload = { name: values.name.trim(), type: values.type, config, enabled: values.enabled === true, description: values.description?.trim() ?? '' }
      if (editing) {
        await updateNotificationChannel(editing.id as string, payload)
      } else {
        await createNotificationChannel(payload)
      }
      feedback.success(t('notification.saveSuccess'))
      setModalOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  /** 保存前试发：用当前表单的类型与配置同步发送一条测试通知 */
  const handleTestForm = async () => {
    const values = (await form.validateFields(['type', 'webhookUrl', 'secret', 'baseUrl', 'botToken', 'chatId', 'parseMode', 'url', 'headersJson', 'bodyTemplate', 'recipients', 'subjectPrefix', 'insiteUserIds', 'insiteToAdmins'])) as ChannelFormValues
    const config = buildValidatedConfig(values.type, values)
    if (config === null) return
    setTesting(true)
    try {
      const result = await testNotificationChannel({ type: values.type, config })
      if (result.success) {
        feedback.success(t('notification.testSuccess', { ms: result.durationMs ?? 0 }))
      } else {
        feedback.error(result.error || t('notification.testFailed'))
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTesting(false)
    }
  }

  const handleTestRow = async (record: NotificationChannelItem) => {
    setTestId(record.id ?? null)
    try {
      const result = await testNotificationChannel({ id: record.id })
      if (result.success) {
        feedback.success(t('notification.testSuccess', { ms: result.durationMs ?? 0 }))
      } else {
        feedback.error(result.error || t('notification.testFailed'))
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTestId(null)
    }
  }

  const handleDelete = async (record: NotificationChannelItem) => {
    try {
      await deleteNotificationChannel(record.id as string)
      feedback.success(t('notification.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleBroadcast = async () => {
    const values = await broadcastForm.validateFields()
    setBroadcasting(true)
    try {
      const count = await broadcastNotification({ title: values.title.trim(), content: values.content, level: values.level })
      feedback.success(t('notification.broadcastQueued', { count }))
      setBroadcastOpen(false)
      broadcastForm.resetFields()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBroadcasting(false)
    }
  }

  const columns: TableColumnsType<NotificationChannelItem> = useMemo(
    () => [
      { title: t('notification.colName'), dataIndex: 'name', width: 170, ellipsis: true },
      {
        title: t('notification.colType'),
        dataIndex: 'type',
        width: 120,
        render: (v: NotificationChannelTypeValue) => <Tag color={TYPE_COLORS[v] ?? 'default'}>{typeLabel(v)}</Tag>,
      },
      {
        title: t('notification.colEnabled'),
        dataIndex: 'enabled',
        width: 90,
        render: (v: boolean) => (v ? <Badge status="success" text={t('notification.enabledOn')} /> : <Badge status="default" text={t('notification.enabledOff')} />),
      },
      { title: t('notification.colDescription'), dataIndex: 'description', ellipsis: true, render: (v: string | null) => v || '-' },
      {
        title: t('notification.colCreate'),
        key: 'create',
        width: 150,
        render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
      },
      {
        title: t('notification.colUpdate'),
        key: 'update',
        width: 150,
        render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} />,
      },
      {
        title: t('notification.colActions'),
        key: 'actions',
        width: 170,
        fixed: 'right',
        render: (_, record) => (
          <>
            <Button type="link" size="small" icon={<SendOutlined />} loading={testId === record.id} onClick={() => void handleTestRow(record)}>
              {t('notification.test')}
            </Button>
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
              {t('notification.edit')}
            </Button>
            <Popconfirm title={t('notification.deleteConfirm', { name: record.name ?? '' })} onConfirm={() => void handleDelete(record)}>
              <Button type="link" size="small" icon={<DeleteOutlined />} danger>
                {t('notification.delete')}
              </Button>
            </Popconfirm>
          </>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, testId],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <QueryBar
        onSearch={() => void load()}
        onReset={() => {
          setSearch('')
          void load('')
        }}
        loading={loading}
        extra={
          <Space>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('notification.create')}
            </Button>
            <Button icon={<BellOutlined />} onClick={() => setBroadcastOpen(true)}>
              {t('notification.broadcast')}
            </Button>
          </Space>
        }
      >
        <Input
          placeholder={t('notification.searchName')}
          allowClear
          maxLength={100}
          style={{ width: 200 }}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            void load(e.target.value)
          }}
        />
      </QueryBar>
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<NotificationChannelItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1200, y: panelTableScrollY }}
          pagination={false}
        />
      </div>

      <Modal
        open={modalOpen}
        title={editing ? t('notification.editTitle', { name: editing.name ?? '' }) : t('notification.createTitle')}
        onOk={() => void handleSave()}
        onCancel={() => setModalOpen(false)}
        okText={t('notification.save')}
        cancelText={t('notification.cancel')}
        confirmLoading={submitting}
        width={640}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('notification.formName')}
            rules={[{ required: true, message: t('notification.error.nameRequired') }, { max: 100, message: t('notification.error.nameMax') }]}
          >
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item name="type" label={t('notification.formType')} rules={[{ required: true, message: t('notification.error.typeRequired') }]}>
            <Select
              disabled={editing != null}
              options={CHANNEL_TYPES.map((type) => ({ value: type, label: t(`notification.type.${type}`) }))}
            />
          </Form.Item>

          {(formType === 'feishuWebhook' || formType === 'dingtalkWebhook' || formType === 'wecomWebhook') && (
            <>
              <Form.Item
                name="webhookUrl"
                label={t('notification.form.webhookUrl')}
                rules={[{ required: formType !== undefined, message: t('notification.error.webhookUrlRequired') }]}
                extra={formType === 'feishuWebhook' || formType === 'dingtalkWebhook' ? t('notification.form.webhookUrlExtra') : undefined}
              >
                <Input placeholder="https://..." maxLength={500} />
              </Form.Item>
              {formType !== 'wecomWebhook' && (
                <Form.Item name="secret" label={t('notification.form.secret')} extra={t('notification.form.secretExtra')}>
                  <Input.Password maxLength={200} autoComplete="new-password" />
                </Form.Item>
              )}
            </>
          )}

          {formType === 'telegramBot' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.baseUrl')} extra={t('notification.form.baseUrlExtra')}>
                <Input placeholder="https://api.telegram.org" maxLength={300} />
              </Form.Item>
              <Form.Item name="botToken" label={t('notification.form.botToken')} rules={[{ required: true, message: t('notification.error.botTokenRequired') }]}>
                <Input.Password maxLength={100} autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="chatId" label={t('notification.form.chatId')} rules={[{ required: true, message: t('notification.error.chatIdRequired') }]}>
                <Input placeholder="@channelname 或 数字 id" maxLength={100} />
              </Form.Item>
              <Form.Item name="parseMode" label={t('notification.form.parseMode')}>
                <Select allowClear placeholder={t('notification.form.parseModePlain')} options={[
                  { value: 'MarkdownV2', label: 'MarkdownV2' },
                  { value: 'HTML', label: 'HTML' },
                ]} />
              </Form.Item>
            </>
          )}

          {formType === 'webhook' && (
            <>
              <Form.Item name="url" label={t('notification.form.url')} rules={[{ required: true, message: t('notification.error.urlRequired') }]}>
                <Input placeholder="https://..." maxLength={500} />
              </Form.Item>
              <Form.Item
                name="headersJson"
                label={t('notification.form.headers')}
                extra={t('notification.form.headersExtra')}
                rules={[
                  {
                    validator: (_, value: string) => {
                      if (!value || value.trim().length === 0) return Promise.resolve()
                      try {
                        const parsed = JSON.parse(value) as Record<string, unknown>
                        if (typeof parsed !== 'object' || Array.isArray(parsed)) return Promise.reject(new Error('invalid'))
                        return Promise.resolve()
                      } catch {
                        return Promise.reject(new Error('invalid json'))
                      }
                    },
                    message: t('notification.error.headersInvalid'),
                  },
                ]}
              >
                <Input.TextArea rows={3} placeholder='{"X-Token": "xxx"}' />
              </Form.Item>
              <Form.Item name="bodyTemplate" label={t('notification.form.bodyTemplate')} extra={t('notification.form.bodyTemplateExtra')}>
                <Input.TextArea rows={3} placeholder='{"t":"{title}","c":"{content}","lv":"{level}"}' maxLength={2000} />
              </Form.Item>
            </>
          )}

          {formType === 'smtp' && (
            <>
              <Form.Item name="recipients" label={t('notification.form.recipients')} rules={[{ required: true, message: t('notification.error.recipientsRequired') }]} extra={t('notification.form.recipientsExtra')}>
                <Select mode="tags" open={false} tokenSeparators={[',', '，', ' ']} placeholder="a@example.com" />
              </Form.Item>
              <Form.Item name="subjectPrefix" label={t('notification.form.subjectPrefix')} extra={t('notification.form.subjectPrefixExtra')}>
                <Input maxLength={50} />
              </Form.Item>
            </>
          )}

          {formType === 'inSite' && (
            <>
              <Form.Item name="insiteUserIds" label={t('notification.form.insiteUserIds')} extra={t('notification.form.insiteUserIdsExtra')}>
                <Input placeholder="1001,1002" maxLength={500} />
              </Form.Item>
              <Form.Item name="insiteToAdmins" label={t('notification.form.insiteToAdmins')} valuePropName="checked" extra={t('notification.form.insiteToAdminsExtra')}>
                <Switch />
              </Form.Item>
            </>
          )}

          <Form.Item name="enabled" label={t('notification.form.enabled')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="description" label={t('notification.form.description')}>
            <Input maxLength={500} />
          </Form.Item>
        </Form>

        <Tooltip title={t('notification.testFormTip')}>
          <Button icon={<SendOutlined />} loading={testing} onClick={() => void handleTestForm()}>
            {t('notification.testForm')}
          </Button>
        </Tooltip>
      </Modal>

      <Modal
        open={broadcastOpen}
        title={t('notification.broadcastTitle')}
        onOk={() => void handleBroadcast()}
        onCancel={() => setBroadcastOpen(false)}
        okText={t('notification.broadcastSend')}
        cancelText={t('notification.cancel')}
        confirmLoading={broadcasting}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={broadcastForm} layout="vertical">
          <Form.Item name="title" label={t('notification.form.title')} rules={[{ required: true, message: t('notification.error.titleRequired') }, { max: 200, message: t('notification.error.titleMax') }]}>
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="content" label={t('notification.form.content')} rules={[{ required: true, message: t('notification.error.contentRequired') }, { max: 4000, message: t('notification.error.contentMax') }]}>
            <Input.TextArea rows={4} maxLength={4000} />
          </Form.Item>
          <Form.Item name="level" label={t('notification.form.level')} initialValue="info">
            <Select options={[
              { value: 'info', label: t('notification.level.info') },
              { value: 'warning', label: t('notification.level.warning') },
              { value: 'error', label: t('notification.level.error') },
            ]} />
          </Form.Item>
        </Form>
        <Text type="secondary">{t('notification.broadcastTip')}</Text>
      </Modal>
    </div>
  )
}

/** 发送记录面板 */
function LogsPanel() {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<NotificationLogItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [channelOptions, setChannelOptions] = useState<{ value: string; label: string }[]>([])

  useEffect(() => {
    // 渠道过滤下拉数据源
    getNotificationChannels()
      .then((channels) => setChannelOptions(channels.map((c) => ({ value: c.id as string, label: c.name ?? '' }))))
      .catch(() => {
        // 静默，错误已由全局请求中间件提示
      })
  }, [])

  const load = useCallback(
    async (page = pageNo, size = pageSize) => {
      setLoading(true)
      try {
        const values = form.getFieldsValue()
        const res = await getNotificationLogs({
          pageNo: page,
          pageSize: size,
          channelId: values.channelId || undefined,
          status: values.status || undefined,
        })
        setItems(res.items ?? [])
        setTotalCount(res.totalCount ?? 0)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pageNo, pageSize],
  )

  useEffect(() => {
    void load(1, pageSize)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const renderStatus = (status: NotificationSendStatusValue | undefined) => {
    if (status === 'success') return <Badge status="success" text={t('notification.status.success')} />
    if (status === 'failed') return <Badge status="error" text={t('notification.status.failed')} />
    return <Badge status="processing" text={t('notification.status.pending')} />
  }

  const renderLevel = (level: NotificationLevelValue | undefined) => {
    if (level === 'error') return <Tag color="red">{t('notification.level.error')}</Tag>
    if (level === 'warning') return <Tag color="orange">{t('notification.level.warning')}</Tag>
    return <Tag color="blue">{t('notification.level.info')}</Tag>
  }

  const columns: TableColumnsType<NotificationLogItem> = useMemo(
    () => [
      { title: t('notification.colTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => formatDateTime(v) },
      { title: t('notification.colChannelName'), dataIndex: 'channelName', width: 150, ellipsis: true },
      {
        title: t('notification.colType'),
        dataIndex: 'channelType',
        width: 110,
        render: (v: NotificationChannelTypeValue) => <Tag color={TYPE_COLORS[v] ?? 'default'}>{t(`notification.type.${v}`)}</Tag>,
      },
      { title: t('notification.colLevel'), dataIndex: 'level', width: 90, render: (v: NotificationLevelValue) => renderLevel(v) },
      { title: t('notification.colStatus'), dataIndex: 'status', width: 100, render: (v: NotificationSendStatusValue) => renderStatus(v) },
      { title: t('notification.colTitle'), dataIndex: 'title', width: 240, ellipsis: true },
      {
        title: t('notification.colContent'),
        dataIndex: 'content',
        ellipsis: true,
        render: (v: string | null) => (
          <Tooltip title={v} placement="topLeft">
            <Text type="secondary" style={{ fontSize: 12 }}>{v || '-'}</Text>
          </Tooltip>
        ),
      },
      {
        title: t('notification.colError'),
        dataIndex: 'error',
        width: 200,
        ellipsis: true,
        render: (v: string | null) =>
          v ? (
            <Tooltip title={v} placement="topLeft">
              <Text type="danger" style={{ fontSize: 12 }}>{v}</Text>
            </Tooltip>
          ) : (
            '-'
          ),
      },
      { title: t('notification.colDuration'), dataIndex: 'durationMs', width: 90, render: (v: number | undefined) => (v == null ? '-' : `${v}ms`) },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, pageNo, pageSize],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <QueryBar
        onSearch={() => {
          setPageNo(1)
          void load(1, pageSize)
        }}
        onReset={() => {
          form.resetFields()
          setPageNo(1)
          void load(1, pageSize)
        }}
        loading={loading}
        extra={
          <Button icon={<ReloadOutlined />} onClick={() => void load(pageNo, pageSize)}>
            {t('notification.refresh')}
          </Button>
        }
      >
        <Form.Item name="channelId">
          <Select
            placeholder={t('notification.searchChannel')}
            allowClear
            showSearch
            optionFilterProp="label"
            style={{ width: 200 }}
            options={channelOptions}
          />
        </Form.Item>
        <Form.Item name="status">
          <Select
            placeholder={t('notification.searchStatus')}
            allowClear
            style={{ width: 140 }}
            options={[
              { value: 'pending', label: t('notification.status.pending') },
              { value: 'success', label: t('notification.status.success') },
              { value: 'failed', label: t('notification.status.failed') },
            ]}
          />
        </Form.Item>
      </QueryBar>
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<NotificationLogItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1400, y: panelTableScrollY }}
          pagination={{
            current: pageNo,
            pageSize,
            total: totalCount,
            showSizeChanger: true,
            onChange: (page, size) => {
              setPageNo(page)
              setPageSize(size)
              void load(page, size)
            },
          }}
        />
      </div>
    </div>
  )
}
