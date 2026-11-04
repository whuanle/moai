import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  BellOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  SendOutlined,
} from '@ant-design/icons'
import { Badge, Button, Checkbox, Form, Input, InputNumber, Modal, Popconfirm, Radio, Select, Space, Switch, Tabs, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router'
import { AuditCell, DataTable, Page, QueryBar, feedback } from '@/design-system'
import { controlHeight, spacing, tableCardChromeHeight } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import {
  broadcastNotification,
  createNotificationChannel,
  createNotificationTemplate,
  deleteNotificationChannel,
  deleteNotificationTemplate,
  getNotificationChannels,
  getNotificationLogs,
  getNotificationTemplates,
  testNotificationChannel,
  testNotificationTemplate,
  updateNotificationChannel,
  updateNotificationTemplate,
  type NotificationChannelItem,
  type NotificationChannelTypeValue,
  type NotificationContentTypeValue,
  type NotificationLevelValue,
  type NotificationLogItem,
  type NotificationSendStatusValue,
  type NotificationTemplateItem,
  type NotificationTemplateTestResult,
  type NotificationTemplateVariableItem,
} from '@/api/notification'

const { Text } = Typography

const pageVerticalPadding = spacing.lg * 2
// Tab 栏自身 46px（padding 12+12 + 22px 文字行高）+ antd 顶部 Tabs 导航默认 margin-bottom 16px（horizontalMargin），
// 少算这 16px 会让整页恒定高出 16px 出现页面滚动条
const tabsBarHeight = 46 + 16
const queryBarHeight = controlHeight + spacing.md
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const panelHeight = `calc(100vh - ${pageVerticalPadding + tabsBarHeight}px)`
const panelTableScrollY = `calc(100vh - ${pageVerticalPadding + tabsBarHeight + queryBarHeight + tableHeaderHeight + tablePaginationHeight + tableCardChromeHeight}px)`
// 弹窗内容限高（默认 top 100px + 标题/底部按钮与上下留白约 160px），表单过长时仅 body 内部滚动，标题与按钮保持可见
const modalBodyStyle = { maxHeight: 'calc(100vh - 260px)', overflowY: 'auto' } as const

// 可创建的渠道类型：站内推送不作为渠道配置（广播弹窗可直接勾选「发送站内信」，无需配置）
const CHANNEL_TYPES: NotificationChannelTypeValue[] = [
  'feishuWebhook',
  'dingtalkWebhook',
  'wecomWebhook',
  'feishuAppBot',
  'dingtalkAppBot',
  'wecomAppBot',
  'telegramBot',
  'slack',
  'discord',
  'microsoftTeams',
  'webhook',
  'smtp',
  'gotify',
  'ntfy',
  'bark',
  'serverChan',
  'pushPlus',
  'aliyunSms',
  'tencentSms',
]

/** 应用机器人渠道：投递目标按用户第三方账号绑定反查，可对个人推送 */
const APP_BOT_TYPES: NotificationChannelTypeValue[] = ['feishuAppBot', 'dingtalkAppBot', 'wecomAppBot']

/** 渠道类型 → Tag 颜色 */
const TYPE_COLORS: Record<NotificationChannelTypeValue, string> = {
  feishuWebhook: 'blue',
  dingtalkWebhook: 'geekblue',
  wecomWebhook: 'green',
  feishuAppBot: 'blue',
  dingtalkAppBot: 'geekblue',
  wecomAppBot: 'green',
  telegramBot: 'cyan',
  slack: 'magenta',
  discord: 'volcano',
  microsoftTeams: 'gold',
  webhook: 'purple',
  smtp: 'orange',
  gotify: 'lime',
  ntfy: 'red',
  bark: 'magenta',
  serverChan: 'volcano',
  pushPlus: 'purple',
  aliyunSms: 'geekblue',
  tencentSms: 'cyan',
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
          { key: 'templates', label: t('notification.tabTemplates'), children: <TemplatesPanel /> },
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
  gotifyToken?: string
  ntfyTopic?: string
  barkDeviceKey?: string
  serverChanSendKey?: string
  pushPlusToken?: string
  url?: string
  headersJson?: string
  bodyTemplate?: string
  smtpHost?: string
  smtpPort?: number
  smtpSecurity?: string
  smtpUsername?: string
  smtpPassword?: string
  smtpFromAddress?: string
  smtpFromName?: string
  insiteUserIds?: string
  insiteToAdmins?: boolean
  appId?: string
  appKey?: string
  corpId?: string
  appSecret?: string
  corpSecret?: string
  agentId?: string
  smsAccessKeyId?: string
  smsAccessKeySecret?: string
  smsSecretId?: string
  smsSecretKey?: string
  smsSdkAppId?: string
  smsSignName?: string
  smsEndpoint?: string
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
    case 'feishuAppBot':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://open.feishu.cn', appId: v.appId?.trim() ?? '', appSecret: v.appSecret?.trim() ?? '' })
    case 'dingtalkAppBot':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://oapi.dingtalk.com', appKey: v.appKey?.trim() ?? '', appSecret: v.appSecret?.trim() ?? '', agentId: v.agentId?.trim() ?? '' })
    case 'wecomAppBot':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://qyapi.weixin.qq.com', corpId: v.corpId?.trim() ?? '', corpSecret: v.corpSecret?.trim() ?? '', agentId: v.agentId?.trim() ?? '' })
    case 'slack':
    case 'discord':
    case 'microsoftTeams':
      return JSON.stringify({ webhookUrl: v.webhookUrl?.trim() ?? '' })
    case 'gotify':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() ?? '', token: v.gotifyToken?.trim() ?? '' })
    case 'ntfy':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://ntfy.sh', topic: v.ntfyTopic?.trim() ?? '' })
    case 'bark':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://api.day.app', deviceKey: v.barkDeviceKey?.trim() ?? '' })
    case 'serverChan':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://sctapi.ftqq.com', sendKey: v.serverChanSendKey?.trim() ?? '' })
    case 'pushPlus':
      return JSON.stringify({ baseUrl: v.baseUrl?.trim() || 'https://www.pushplus.plus', token: v.pushPlusToken?.trim() ?? '' })
    case 'webhook': {
      let headers: Record<string, string> = {}
      const raw = v.headersJson?.trim()
      if (raw) headers = JSON.parse(raw) as Record<string, string>
      return JSON.stringify({ url: v.url?.trim() ?? '', headers, bodyTemplate: v.bodyTemplate ?? '' })
    }
    case 'smtp':
      return JSON.stringify({
        host: v.smtpHost?.trim() ?? '',
        port: typeof v.smtpPort === 'number' && v.smtpPort >= 1 && v.smtpPort <= 65535 ? v.smtpPort : 465,
        security: v.smtpSecurity ?? 'ssl',
        username: v.smtpUsername?.trim() ?? '',
        password: v.smtpPassword ?? '',
        fromAddress: v.smtpFromAddress?.trim() ?? '',
        fromName: v.smtpFromName?.trim() ?? '',
      })
    case 'inSite': {
      const userIds = (v.insiteUserIds ?? '')
        .split(/[,，\s]+/)
        .filter(Boolean)
        .map(Number)
        .filter((n) => Number.isFinite(n) && n > 0)
      return JSON.stringify({ userIds, toAdmins: v.insiteToAdmins === true })
    }
    case 'aliyunSms':
      return JSON.stringify({
        accessKeyId: v.smsAccessKeyId?.trim() ?? '',
        accessKeySecret: v.smsAccessKeySecret?.trim() ?? '',
        signName: v.smsSignName?.trim() ?? '',
        endpoint: v.smsEndpoint?.trim() || 'https://dysmsapi.aliyuncs.com',
      })
    case 'tencentSms':
      return JSON.stringify({
        secretId: v.smsSecretId?.trim() ?? '',
        secretKey: v.smsSecretKey?.trim() ?? '',
        smsSdkAppId: v.smsSdkAppId?.trim() ?? '',
        signName: v.smsSignName?.trim() ?? '',
        endpoint: v.smsEndpoint?.trim() || 'https://sms.tencentcloudapi.com',
      })
    default:
      return '{}'
  }
}

/** 渠道管理面板 */
function ChannelsPanel() {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const [searchForm] = Form.useForm()
  const formType = Form.useWatch('type', form)
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<NotificationChannelItem[]>([])
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<NotificationChannelItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [testing, setTesting] = useState(false)
  const [testId, setTestId] = useState<string | null>(null)
  // 试发收件人：SMTP/短信渠道发送时动态指定（邮箱/手机号按渠道类型）
  const [testRecipients, setTestRecipients] = useState<string[]>([])
  // 行内「测试」弹窗：SMTP/短信渠道需先填写收件人再试发
  const [rowTestTarget, setRowTestTarget] = useState<NotificationChannelItem | null>(null)
  const [rowTestForm] = Form.useForm()
  const [rowTestSubmitting, setRowTestSubmitting] = useState(false)
  const [broadcastOpen, setBroadcastOpen] = useState(false)
  const [broadcastForm] = Form.useForm()
  const [broadcasting, setBroadcasting] = useState(false)
  const [channelOptions, setChannelOptions] = useState<{ value: string; label: string; type?: NotificationChannelTypeValue }[]>([])
  const broadcastUseTemplate = Form.useWatch('useTemplate', broadcastForm)
  const broadcastTemplateId = Form.useWatch('templateId', broadcastForm)
  const broadcastChannelIds = Form.useWatch('channelIds', broadcastForm) as string[] | undefined
  const [broadcastTemplates, setBroadcastTemplates] = useState<NotificationTemplateItem[]>([])
  const broadcastVariables: NotificationTemplateVariableItem[] = useMemo(() => {
    const template = broadcastTemplates.find((tpl) => tpl.id === broadcastTemplateId)
    try {
      return JSON.parse(template?.variables ?? '[]') as NotificationTemplateVariableItem[]
    } catch {
      return []
    }
  }, [broadcastTemplates, broadcastTemplateId])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const keyword = (searchForm.getFieldValue('keyword') as string | undefined)?.trim() || undefined
      setItems(await getNotificationChannels(keyword))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [searchForm])

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const typeLabel = (type: NotificationChannelTypeValue | undefined) => (type ? t(`notification.type.${type}`) : '-')

  /** 渠道是否需要动态收件人邮箱（SMTP）或手机号（短信） */
  const recipientKindOf = (type: NotificationChannelTypeValue | undefined): 'email' | 'phone' | null => {
    if (type === 'smtp') return 'email'
    if (type === 'aliyunSms' || type === 'tencentSms') return 'phone'
    return null
  }

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldsValue({ type: 'feishuWebhook', enabled: true, parseMode: '', baseUrl: '' })
    setTestRecipients([])
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
        const smtpPort = Number(config.port)
        values.smtpHost = (config.host as string) ?? ''
        values.smtpPort = Number.isFinite(smtpPort) && smtpPort >= 1 && smtpPort <= 65535 ? smtpPort : 465
        values.smtpSecurity = (config.security as string) ?? 'ssl'
        values.smtpUsername = (config.username as string) ?? ''
        values.smtpPassword = (config.password as string) ?? ''
        values.smtpFromAddress = (config.fromAddress as string) ?? ''
        values.smtpFromName = (config.fromName as string) ?? ''
      } else if (record.type === 'inSite') {
        values.insiteUserIds = ((config.userIds as number[]) ?? []).join(',')
        values.insiteToAdmins = config.toAdmins === true
      } else if (record.type === 'aliyunSms' || record.type === 'tencentSms') {
        values.smsAccessKeyId = (config.accessKeyId as string) ?? ''
        values.smsAccessKeySecret = (config.accessKeySecret as string) ?? ''
        values.smsSecretId = (config.secretId as string) ?? ''
        values.smsSecretKey = (config.secretKey as string) ?? ''
        values.smsSdkAppId = (config.smsSdkAppId as string) ?? ''
        values.smsSignName = (config.signName as string) ?? ''
        values.smsEndpoint = (config.endpoint as string) ?? ''
      } else {
        values.webhookUrl = (config.webhookUrl as string) ?? ''
        values.secret = (config.secret as string) ?? ''
        values.baseUrl = (config.baseUrl as string) ?? ''
        values.botToken = (config.botToken as string) ?? ''
        values.chatId = (config.chatId as string) ?? ''
        values.parseMode = (config.parseMode as string) ?? ''
        values.gotifyToken = (config.token as string) ?? ''
        values.ntfyTopic = (config.topic as string) ?? ''
        values.barkDeviceKey = (config.deviceKey as string) ?? ''
        values.serverChanSendKey = (config.sendKey as string) ?? ''
        values.pushPlusToken = (config.token as string) ?? ''
        values.url = (config.url as string) ?? ''
        values.headersJson = Object.keys((config.headers as Record<string, string>) ?? {}).length > 0 ? JSON.stringify(config.headers, null, 2) : ''
        values.bodyTemplate = (config.bodyTemplate as string) ?? ''
        values.appId = (config.appId as string) ?? ''
        values.appKey = (config.appKey as string) ?? ''
        values.corpId = (config.corpId as string) ?? ''
        values.appSecret = (config.appSecret as string) ?? ''
        values.corpSecret = (config.corpSecret as string) ?? ''
        values.agentId = (config.agentId as string) ?? ''
      }
    } catch {
      // 配置 JSON 异常时仅回显基础字段
    }
    form.setFieldsValue(values)
    setTestRecipients([])
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
      case 'slack':
      case 'discord':
      case 'microsoftTeams':
        if (!requireText(v.webhookUrl, 'notification.error.webhookUrlRequired')) return null
        break
      case 'telegramBot':
        if (!requireText(v.botToken, 'notification.error.botTokenRequired')) return null
        if (!requireText(v.chatId, 'notification.error.chatIdRequired')) return null
        break
      case 'feishuAppBot':
        if (!requireText(v.appId, 'notification.error.appIdRequired')) return null
        if (!requireText(v.appSecret, 'notification.error.appSecretRequired')) return null
        break
      case 'dingtalkAppBot':
        if (!requireText(v.appKey, 'notification.error.appKeyRequired')) return null
        if (!requireText(v.appSecret, 'notification.error.appSecretRequired')) return null
        if (!requireText(v.agentId, 'notification.error.agentIdRequired')) return null
        break
      case 'wecomAppBot':
        if (!requireText(v.corpId, 'notification.error.corpIdRequired')) return null
        if (!requireText(v.corpSecret, 'notification.error.corpSecretRequired')) return null
        if (!requireText(v.agentId, 'notification.error.agentIdRequired')) return null
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
      case 'smtp': {
        if (!requireText(v.smtpHost, 'notification.error.smtpHostRequired')) return null
        const smtpPort = v.smtpPort
        if (!Number.isInteger(smtpPort) || (smtpPort as number) < 1 || (smtpPort as number) > 65535) {
          feedback.error(t('notification.error.smtpPortInvalid'))
          return null
        }
        const smtpFrom = v.smtpFromAddress?.trim()
        if (smtpFrom && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(smtpFrom)) {
          feedback.error(t('notification.error.smtpFromAddressInvalid'))
          return null
        }
        break
      }
      case 'gotify':
        if (!requireText(v.baseUrl, 'notification.error.baseUrlRequired')) return null
        if (!requireText(v.gotifyToken, 'notification.error.tokenRequired')) return null
        break
      case 'ntfy':
        if (!requireText(v.ntfyTopic, 'notification.error.topicRequired')) return null
        break
      case 'bark':
        if (!requireText(v.barkDeviceKey, 'notification.error.deviceKeyRequired')) return null
        break
      case 'serverChan':
        if (!requireText(v.serverChanSendKey, 'notification.error.sendKeyRequired')) return null
        break
      case 'pushPlus':
        if (!requireText(v.pushPlusToken, 'notification.error.tokenRequired')) return null
        break
      case 'inSite':
        if ((!v.insiteUserIds || v.insiteUserIds.trim().length === 0) && v.insiteToAdmins !== true) {
          feedback.error(t('notification.error.inSiteTargetRequired'))
          return null
        }
        break
      case 'aliyunSms':
        if (!requireText(v.smsAccessKeyId, 'notification.error.accessKeyIdRequired')) return null
        if (!requireText(v.smsAccessKeySecret, 'notification.error.accessKeySecretRequired')) return null
        if (!requireText(v.smsSignName, 'notification.error.signNameRequired')) return null
        break
      case 'tencentSms':
        if (!requireText(v.smsSecretId, 'notification.error.secretIdRequired')) return null
        if (!requireText(v.smsSecretKey, 'notification.error.secretKeyRequired')) return null
        if (!requireText(v.smsSdkAppId, 'notification.error.smsSdkAppIdRequired')) return null
        if (!requireText(v.smsSignName, 'notification.error.signNameRequired')) return null
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

  /** 保存前试发：用当前表单的类型与配置同步发送一条测试通知（SMTP/短信渠道需填写试发收件人） */
  const handleTestForm = async () => {
    const values = (await form.validateFields(['type', 'webhookUrl', 'secret', 'baseUrl', 'botToken', 'chatId', 'parseMode', 'gotifyToken', 'ntfyTopic', 'barkDeviceKey', 'serverChanSendKey', 'pushPlusToken', 'url', 'headersJson', 'bodyTemplate', 'smtpHost', 'smtpPort', 'smtpSecurity', 'smtpUsername', 'smtpPassword', 'smtpFromAddress', 'smtpFromName', 'insiteUserIds', 'insiteToAdmins', 'appId', 'appKey', 'corpId', 'appSecret', 'corpSecret', 'agentId', 'smsAccessKeyId', 'smsAccessKeySecret', 'smsSecretId', 'smsSecretKey', 'smsSdkAppId', 'smsSignName', 'smsEndpoint'])) as ChannelFormValues
    const config = buildValidatedConfig(values.type, values)
    if (config === null) return
    const kind = recipientKindOf(values.type)
    if (kind !== null && testRecipients.length === 0) {
      feedback.error(t(kind === 'email' ? 'notification.error.testRecipientsRequired' : 'notification.error.testRecipientsRequiredSms'))
      return
    }
    setTesting(true)
    try {
      const result = await testNotificationChannel({ type: values.type, config, recipients: kind !== null ? testRecipients : undefined })
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
    // SMTP/短信渠道收件人发送时动态指定：先弹窗填写收件人再试发
    if (recipientKindOf(record.type) !== null) {
      rowTestForm.resetFields()
      setRowTestTarget(record)
      return
    }
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

  /** 行内测试弹窗提交：带收件人向已保存渠道试发 */
  const handleRowTest = async () => {
    if (!rowTestTarget) return
    const values = await rowTestForm.validateFields()
    const recipients = (values.recipients ?? []) as string[]
    setRowTestSubmitting(true)
    try {
      const result = await testNotificationChannel({ id: rowTestTarget.id, recipients })
      if (result.success) {
        feedback.success(t('notification.testSuccess', { ms: result.durationMs ?? 0 }))
        setRowTestTarget(null)
      } else {
        feedback.error(result.error || t('notification.testFailed'))
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setRowTestSubmitting(false)
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
    const channelIds: string[] = values.channelIds ?? []
    const sendInSite = values.sendInSite === true
    if (channelIds.length === 0 && !sendInSite) {
      feedback.error(t('notification.error.broadcastTargetRequired'))
      return
    }
    // 动态收件人：所选渠道含 SMTP 需邮箱收件人、含短信渠道需手机号收件人
    const selectedTypes = channelOptions.filter((option) => channelIds.includes(option.value)).map((option) => option.type)
    const needEmail = selectedTypes.includes('smtp')
    const needPhone = selectedTypes.includes('aliyunSms') || selectedTypes.includes('tencentSms')
    const emailRecipients = (values.emailRecipients ?? []) as string[]
    const phoneNumbers = (values.phoneNumbers ?? []) as string[]
    if (needEmail && emailRecipients.length === 0) {
      feedback.error(t('notification.error.broadcastEmailRecipientsRequired'))
      return
    }
    if (needPhone && phoneNumbers.length === 0) {
      feedback.error(t('notification.error.broadcastPhoneRecipientsRequired'))
      return
    }
    const templateId = values.useTemplate === true ? (values.templateId as string | undefined) : undefined
    let templateVariables: Record<string, string> | undefined
    if (templateId) {
      templateVariables = {}
      for (const variable of broadcastVariables) {
        templateVariables[variable.name] = (values[`var_${variable.name}`] ?? '') as string
      }
    }
    setBroadcasting(true)
    try {
      const count = await broadcastNotification({
        title: (values.title ?? '').trim(),
        content: values.content ?? '',
        level: values.level,
        templateId,
        templateVariables,
        channelIds: channelIds.length > 0 ? channelIds : undefined,
        emailRecipients: needEmail ? emailRecipients : undefined,
        phoneNumbers: needPhone ? phoneNumbers : undefined,
        sendInSite,
      })
      if (sendInSite && channelIds.length > 0) {
        feedback.success(`${t('notification.broadcastQueued', { count })}；${t('notification.broadcastInSiteSent')}`)
      } else if (sendInSite) {
        feedback.success(t('notification.broadcastInSiteSent'))
      } else {
        feedback.success(t('notification.broadcastQueued', { count }))
      }
      setBroadcastOpen(false)
      broadcastForm.resetFields()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBroadcasting(false)
    }
  }

  /** 打开广播弹窗并加载启用渠道与模板选项 */
  const openBroadcast = () => {
    broadcastForm.resetFields()
    getNotificationChannels()
      .then((channels) => setChannelOptions(channels.filter((c) => c.enabled !== false).map((c) => ({ value: c.id as string, label: c.name ?? '', type: c.type }))))
      .catch(() => {
        // 静默，错误已由全局请求中间件提示
      })
    getNotificationTemplates()
      .then((templates) => setBroadcastTemplates(templates))
      .catch(() => {
        // 静默，错误已由全局请求中间件提示
      })
    setBroadcastOpen(true)
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
        // 3 个「图标+文字」link 按钮单行宽度（参考 UsersPanel 约 85px/个），Space size={0} 默认 nowrap 防换行
        width: 260,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
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
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, testId],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <QueryBar
        form={searchForm}
        onSearch={() => void load()}
        onReset={() => {
          searchForm.resetFields()
          void load()
        }}
        loading={loading}
        extra={
          <Space>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('notification.create')}
            </Button>
            <Button icon={<BellOutlined />} onClick={openBroadcast}>
              {t('notification.broadcast')}
            </Button>
          </Space>
        }
      >
        <Form.Item name="keyword">
          <Input placeholder={t('notification.searchName')} allowClear maxLength={100} style={{ width: 200 }} />
        </Form.Item>
      </QueryBar>
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<NotificationChannelItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1360, y: panelTableScrollY }}
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
        styles={{ body: modalBodyStyle }}
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
              options={(editing?.type === 'inSite' ? [...CHANNEL_TYPES, 'inSite' as NotificationChannelTypeValue] : CHANNEL_TYPES).map((type) => ({ value: type, label: t(`notification.type.${type}`) }))}
            />
          </Form.Item>

          {(formType === 'feishuWebhook' || formType === 'dingtalkWebhook' || formType === 'wecomWebhook' || formType === 'slack' || formType === 'discord' || formType === 'microsoftTeams') && (
            <>
              <Form.Item
                name="webhookUrl"
                label={t('notification.form.webhookUrl')}
                rules={[{ required: formType !== undefined, message: t('notification.error.webhookUrlRequired') }]}
                extra={formType === 'feishuWebhook' ? t('notification.form.webhookUrlExtraFeishu') : formType === 'dingtalkWebhook' ? t('notification.form.webhookUrlExtraDingTalk') : undefined}
              >
                <Input placeholder="https://..." maxLength={500} />
              </Form.Item>
              {(formType === 'feishuWebhook' || formType === 'dingtalkWebhook') && (
                <Form.Item name="secret" label={t('notification.form.secret')} extra={formType === 'feishuWebhook' ? t('notification.form.secretExtraFeishu') : t('notification.form.secretExtraDingTalk')}>
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

          {formType != null && APP_BOT_TYPES.includes(formType) && (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
              {t('notification.form.appBotTargetExtra')}
            </Typography.Paragraph>
          )}

          {formType === 'feishuAppBot' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.baseUrl')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://open.feishu.cn" maxLength={300} />
              </Form.Item>
              <Form.Item name="appId" label={t('notification.form.appId')} rules={[{ required: true, message: t('notification.error.appIdRequired') }]}>
                <Input maxLength={100} autoComplete="off" />
              </Form.Item>
              <Form.Item name="appSecret" label={t('notification.form.appSecret')} rules={[{ required: true, message: t('notification.error.appSecretRequired') }]}>
                <Input.Password maxLength={200} autoComplete="new-password" />
              </Form.Item>
            </>
          )}

          {formType === 'dingtalkAppBot' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.baseUrl')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://oapi.dingtalk.com" maxLength={300} />
              </Form.Item>
              <Form.Item name="appKey" label={t('notification.form.appKey')} rules={[{ required: true, message: t('notification.error.appKeyRequired') }]}>
                <Input maxLength={100} autoComplete="off" />
              </Form.Item>
              <Form.Item name="appSecret" label={t('notification.form.appSecret')} rules={[{ required: true, message: t('notification.error.appSecretRequired') }]}>
                <Input.Password maxLength={200} autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="agentId" label={t('notification.form.agentId')} rules={[{ required: true, message: t('notification.error.agentIdRequired') }]}>
                <Input maxLength={100} autoComplete="off" />
              </Form.Item>
            </>
          )}

          {formType === 'wecomAppBot' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.baseUrl')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://qyapi.weixin.qq.com" maxLength={300} />
              </Form.Item>
              <Form.Item name="corpId" label={t('notification.form.corpId')} rules={[{ required: true, message: t('notification.error.corpIdRequired') }]}>
                <Input maxLength={100} autoComplete="off" />
              </Form.Item>
              <Form.Item name="corpSecret" label={t('notification.form.corpSecret')} rules={[{ required: true, message: t('notification.error.corpSecretRequired') }]}>
                <Input.Password maxLength={200} autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="agentId" label={t('notification.form.agentId')} rules={[{ required: true, message: t('notification.error.agentIdRequired') }]}>
                <Input maxLength={100} autoComplete="off" />
              </Form.Item>
            </>
          )}

          {formType === 'gotify' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.gotifyUrl')} rules={[{ required: true, message: t('notification.error.baseUrlRequired') }]}>
                <Input placeholder="https://gotify.example.com" maxLength={300} />
              </Form.Item>
              <Form.Item name="gotifyToken" label={t('notification.form.gotifyToken')} rules={[{ required: true, message: t('notification.error.tokenRequired') }]}>
                <Input.Password maxLength={200} autoComplete="new-password" />
              </Form.Item>
            </>
          )}

          {formType === 'ntfy' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.ntfyUrl')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://ntfy.sh" maxLength={300} />
              </Form.Item>
              <Form.Item name="ntfyTopic" label={t('notification.form.ntfyTopic')} rules={[{ required: true, message: t('notification.error.topicRequired') }]}>
                <Input maxLength={100} />
              </Form.Item>
            </>
          )}

          {formType === 'bark' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.barkUrl')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://api.day.app" maxLength={300} />
              </Form.Item>
              <Form.Item name="barkDeviceKey" label={t('notification.form.barkDeviceKey')} rules={[{ required: true, message: t('notification.error.deviceKeyRequired') }]}>
                <Input.Password maxLength={200} autoComplete="new-password" />
              </Form.Item>
            </>
          )}

          {formType === 'serverChan' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.serverChanUrl')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://sctapi.ftqq.com" maxLength={300} />
              </Form.Item>
              <Form.Item name="serverChanSendKey" label={t('notification.form.serverChanKey')} rules={[{ required: true, message: t('notification.error.sendKeyRequired') }]}>
                <Input.Password maxLength={200} autoComplete="new-password" />
              </Form.Item>
            </>
          )}

          {formType === 'pushPlus' && (
            <>
              <Form.Item name="baseUrl" label={t('notification.form.pushPlusUrl')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://www.pushplus.plus" maxLength={300} />
              </Form.Item>
              <Form.Item name="pushPlusToken" label={t('notification.form.pushPlusToken')} rules={[{ required: true, message: t('notification.error.tokenRequired') }]}>
                <Input.Password maxLength={200} autoComplete="new-password" />
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
              <Form.Item name="smtpHost" label={t('notification.form.smtpHost')} rules={[{ required: true, message: t('notification.error.smtpHostRequired') }]} extra={t('notification.form.smtpConnectionExtra')}>
                <Input placeholder={t('notification.form.smtpHostPlaceholder')} maxLength={255} />
              </Form.Item>
              <Form.Item name="smtpPort" label={t('notification.form.smtpPort')} initialValue={465} rules={[{ required: true, message: t('notification.error.smtpPortInvalid') }]}>
                <InputNumber min={1} max={65535} precision={0} style={{ width: 160 }} />
              </Form.Item>
              <Form.Item name="smtpSecurity" label={t('notification.form.smtpSecurity')} initialValue="ssl">
                <Select
                  options={[
                    { value: 'ssl', label: t('notification.form.smtpSecuritySsl') },
                    { value: 'starttls', label: t('notification.form.smtpSecurityStarttls') },
                    { value: 'none', label: t('notification.form.smtpSecurityNone') },
                  ]}
                />
              </Form.Item>
              <Form.Item name="smtpUsername" label={t('notification.form.smtpUsername')}>
                <Input placeholder={t('notification.form.smtpUsernamePlaceholder')} maxLength={320} autoComplete="off" />
              </Form.Item>
              <Form.Item name="smtpPassword" label={t('notification.form.smtpPassword')}>
                <Input.Password placeholder={t('notification.form.smtpPasswordPlaceholder')} maxLength={200} autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="smtpFromAddress" label={t('notification.form.smtpFromAddress')}>
                <Input placeholder={t('notification.form.smtpFromAddressPlaceholder')} maxLength={320} />
              </Form.Item>
              <Form.Item name="smtpFromName" label={t('notification.form.smtpFromName')}>
                <Input placeholder={t('notification.form.smtpFromNamePlaceholder')} maxLength={100} />
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

          {formType === 'aliyunSms' && (
            <>
              <Form.Item name="smsAccessKeyId" label={t('notification.form.accessKeyId')} rules={[{ required: true, message: t('notification.error.accessKeyIdRequired') }]}>
                <Input maxLength={128} autoComplete="off" />
              </Form.Item>
              <Form.Item name="smsAccessKeySecret" label={t('notification.form.accessKeySecret')} rules={[{ required: true, message: t('notification.error.accessKeySecretRequired') }]}>
                <Input.Password maxLength={256} autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="smsSignName" label={t('notification.form.smsSignName')} rules={[{ required: true, message: t('notification.error.signNameRequired') }]} extra={t('notification.form.smsSignNameExtra')}>
                <Input maxLength={64} />
              </Form.Item>
              <Form.Item name="smsEndpoint" label={t('notification.form.smsEndpoint')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://dysmsapi.aliyuncs.com" maxLength={300} />
              </Form.Item>
            </>
          )}

          {formType === 'tencentSms' && (
            <>
              <Form.Item name="smsSecretId" label={t('notification.form.secretId')} rules={[{ required: true, message: t('notification.error.secretIdRequired') }]}>
                <Input maxLength={128} autoComplete="off" />
              </Form.Item>
              <Form.Item name="smsSecretKey" label={t('notification.form.secretKey')} rules={[{ required: true, message: t('notification.error.secretKeyRequired') }]}>
                <Input.Password maxLength={256} autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="smsSdkAppId" label={t('notification.form.smsSdkAppId')} rules={[{ required: true, message: t('notification.error.smsSdkAppIdRequired') }]}>
                <Input maxLength={64} autoComplete="off" />
              </Form.Item>
              <Form.Item name="smsSignName" label={t('notification.form.smsSignName')} rules={[{ required: true, message: t('notification.error.signNameRequired') }]} extra={t('notification.form.smsSignNameExtra')}>
                <Input maxLength={64} />
              </Form.Item>
              <Form.Item name="smsEndpoint" label={t('notification.form.smsEndpoint')} extra={t('notification.form.selfHostExtra')}>
                <Input placeholder="https://sms.tencentcloudapi.com" maxLength={300} />
              </Form.Item>
            </>
          )}

          <Form.Item name="enabled" label={t('notification.form.enabled')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="description" label={t('notification.form.description')}>
            <Input maxLength={500} />
          </Form.Item>

          {(formType === 'smtp' || formType === 'aliyunSms' || formType === 'tencentSms') && (
            // 试发收件人与试发按钮同处一个 Form.Item 控制区，保证按钮与输入框垂直对齐（位于 Form 内才能继承 vertical 布局）
            <Form.Item label={formType === 'smtp' ? t('notification.form.testRecipients') : t('notification.form.testPhoneNumbers')} required style={{ marginBottom: 0 }} help={t(formType === 'smtp' ? 'notification.form.testRecipientsExtra' : 'notification.form.testPhoneNumbersExtra')}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <Select
                  mode="tags"
                  open={false}
                  tokenSeparators={[',', '，', ' ']}
                  value={testRecipients}
                  onChange={(value) => setTestRecipients(value)}
                  placeholder={formType === 'smtp' ? 'a@example.com' : '13800138000'}
                  style={{ flex: 1, minWidth: 0 }}
                />
                <Tooltip title={t('notification.testFormTip')}>
                  <Button icon={<SendOutlined />} loading={testing} onClick={() => void handleTestForm()}>
                    {t('notification.testForm')}
                  </Button>
                </Tooltip>
              </div>
            </Form.Item>
          )}
        </Form>
      </Modal>

      <Modal
        open={rowTestTarget != null}
        title={t('notification.rowTestTitle', { name: rowTestTarget?.name ?? '' })}
        onOk={() => void handleRowTest()}
        onCancel={() => setRowTestTarget(null)}
        okText={t('notification.test')}
        cancelText={t('notification.cancel')}
        confirmLoading={rowTestSubmitting}
        width={560}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={rowTestForm} layout="vertical">
          <Form.Item
            name="recipients"
            label={rowTestTarget?.type === 'smtp' ? t('notification.form.testRecipients') : t('notification.form.testPhoneNumbers')}
            rules={[{ required: true, message: t(rowTestTarget?.type === 'smtp' ? 'notification.error.testRecipientsRequired' : 'notification.error.testRecipientsRequiredSms') }]}
            extra={t(rowTestTarget?.type === 'smtp' ? 'notification.form.testRecipientsRowExtra' : 'notification.form.testPhoneNumbersRowExtra')}
          >
            <Select mode="tags" open={false} tokenSeparators={[',', '，', ' ']} placeholder={rowTestTarget?.type === 'smtp' ? 'a@example.com' : '13800138000'} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={broadcastOpen}
        title={t('notification.broadcastTitle')}
        onOk={() => void handleBroadcast()}
        onCancel={() => setBroadcastOpen(false)}
        okText={t('notification.broadcastSend')}
        cancelText={t('notification.cancel')}
        confirmLoading={broadcasting}
        styles={{ body: modalBodyStyle }}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={broadcastForm} layout="vertical">
          <Form.Item name="useTemplate" valuePropName="checked" extra={t('notification.form.useTemplateExtra')}>
            <Checkbox>{t('notification.form.useTemplate')}</Checkbox>
          </Form.Item>
          {broadcastUseTemplate && (
            <>
              <Form.Item name="templateId" label={t('notification.form.template')} rules={[{ required: true, message: t('notification.error.templateRequired') }]}>
                <Select showSearch optionFilterProp="label" placeholder={t('notification.form.templatePlaceholder')} options={broadcastTemplates.map((tpl) => ({ value: tpl.id as string, label: tpl.name ?? '' }))} />
              </Form.Item>
              {broadcastVariables.map((variable) => (
                <Form.Item
                  key={variable.name}
                  name={`var_${variable.name}`}
                  label={variable.name}
                  rules={[{ required: true, message: t('notification.error.templateVarRequired') }]}
                  extra={variable.description}
                >
                  <Input placeholder={variable.example} maxLength={500} />
                </Form.Item>
              ))}
            </>
          )}
          <Form.Item name="title" label={t('notification.form.title')} rules={[{ required: broadcastUseTemplate !== true, message: t('notification.error.titleRequired') }, { max: 200, message: t('notification.error.titleMax') }]} extra={broadcastUseTemplate ? t('notification.form.titleTemplateExtra') : undefined}>
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="content" label={t('notification.form.content')} rules={[{ required: broadcastUseTemplate !== true, message: t('notification.error.contentRequired') }, { max: 4000, message: t('notification.error.contentMax') }]} extra={broadcastUseTemplate ? t('notification.form.contentTemplateExtra') : undefined}>
            <Input.TextArea rows={4} maxLength={4000} />
          </Form.Item>
          <Form.Item name="level" label={t('notification.form.level')} initialValue="info">
            <Select options={[
              { value: 'info', label: t('notification.level.info') },
              { value: 'warning', label: t('notification.level.warning') },
              { value: 'error', label: t('notification.level.error') },
            ]} />
          </Form.Item>
          <Form.Item name="channelIds" label={t('notification.form.broadcastChannels')} extra={t('notification.form.broadcastChannelsExtra')}>
            <Select
              mode="multiple"
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('notification.form.broadcastChannelsPlaceholder')}
              options={channelOptions}
            />
          </Form.Item>
          {(() => {
            const selectedTypes = channelOptions.filter((option) => (broadcastChannelIds ?? []).includes(option.value)).map((option) => option.type)
            return (
              <>
                {selectedTypes.includes('smtp') && (
                  <Form.Item
                    name="emailRecipients"
                    label={t('notification.form.broadcastEmailRecipients')}
                    rules={[{ required: true, message: t('notification.error.broadcastEmailRecipientsRequired') }]}
                    extra={t('notification.form.broadcastEmailRecipientsExtra')}
                  >
                    <Select mode="tags" open={false} tokenSeparators={[',', '，', ' ']} placeholder="a@example.com" />
                  </Form.Item>
                )}
                {(selectedTypes.includes('aliyunSms') || selectedTypes.includes('tencentSms')) && (
                  <Form.Item
                    name="phoneNumbers"
                    label={t('notification.form.broadcastPhoneNumbers')}
                    rules={[{ required: true, message: t('notification.error.broadcastPhoneRecipientsRequired') }]}
                    extra={t('notification.form.broadcastPhoneNumbersExtra')}
                  >
                    <Select mode="tags" open={false} tokenSeparators={[',', '，', ' ']} placeholder="13800138000" />
                  </Form.Item>
                )}
              </>
            )
          })()}
          <Form.Item name="sendInSite" valuePropName="checked" extra={t('notification.form.sendInSiteExtra')}>
            <Checkbox>{t('notification.form.sendInSite')}</Checkbox>
          </Form.Item>
        </Form>
        <Text type="secondary">{t('notification.broadcastTip')}</Text>
      </Modal>
    </div>
  )
}

/** 解析模板变量定义 JSON（编辑/测试弹窗与表格列共用） */
function parseTemplateVariables(variablesJson: string | undefined): NotificationTemplateVariableItem[] {
  try {
    return JSON.parse(variablesJson ?? '[]') as NotificationTemplateVariableItem[]
  } catch {
    return []
  }
}

/** 变量名规则（与后端一致）：字母开头，仅字母/数字/下划线 */
const TEMPLATE_VARIABLE_NAME_RE = /^[A-Za-z][A-Za-z0-9_]*$/

/** 通知模板面板 */
function TemplatesPanel() {
  const { t } = useTranslation()
  const [searchForm] = Form.useForm()
  const [form] = Form.useForm()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<NotificationTemplateItem[]>([])
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<NotificationTemplateItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const formContentType = Form.useWatch('contentType', form)

  const [testOpen, setTestOpen] = useState(false)
  const [testing, setTesting] = useState(false)
  const [testForm] = Form.useForm()
  const [testTarget, setTestTarget] = useState<NotificationTemplateItem | null>(null)
  const [testResult, setTestResult] = useState<NotificationTemplateTestResult | null>(null)
  const [testChannelOptions, setTestChannelOptions] = useState<{ value: string; label: string; type?: NotificationChannelTypeValue }[]>([])
  const testChannelId = Form.useWatch('channelId', testForm)
  const testVariables = useMemo(() => parseTemplateVariables(testTarget?.variables), [testTarget])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const keyword = (searchForm.getFieldValue('keyword') as string | undefined)?.trim() || undefined
      setItems(await getNotificationTemplates(keyword))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [searchForm])

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldsValue({ contentType: 'text', variables: [] })
    setModalOpen(true)
  }

  const openEdit = (record: NotificationTemplateItem) => {
    setEditing(record)
    form.resetFields()
    form.setFieldsValue({
      name: record.name,
      title: record.title,
      content: record.content,
      contentType: record.contentType ?? 'text',
      variables: parseTemplateVariables(record.variables),
      aliyunTemplateCode: record.aliyunTemplateCode,
      tencentTemplateId: record.tencentTemplateId,
      description: record.description,
    })
    setModalOpen(true)
  }

  const handleSave = async () => {
    const values = (await form.validateFields()) as { name: string; title?: string; content: string; contentType?: NotificationContentTypeValue; variables?: { name: string; description?: string; example?: string }[]; aliyunTemplateCode?: string; tencentTemplateId?: string; description?: string }
    setSubmitting(true)
    try {
      const payload = {
        name: values.name.trim(),
        title: values.title?.trim() ?? '',
        content: values.content,
        contentType: values.contentType ?? 'text',
        variables: JSON.stringify((values.variables ?? []).map((v) => ({ name: v.name.trim(), description: v.description?.trim() ?? '', example: v.example?.trim() ?? '' }))),
        aliyunTemplateCode: values.aliyunTemplateCode?.trim() ?? '',
        tencentTemplateId: values.tencentTemplateId?.trim() ?? '',
        description: values.description?.trim() ?? '',
      }
      if (editing) {
        await updateNotificationTemplate(editing.id as string, payload)
      } else {
        await createNotificationTemplate(payload)
      }
      feedback.success(t('notification.template.saveSuccess'))
      setModalOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (record: NotificationTemplateItem) => {
    try {
      await deleteNotificationTemplate(record.id as string)
      feedback.success(t('notification.template.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const openTest = (record: NotificationTemplateItem) => {
    setTestTarget(record)
    setTestResult(null)
    testForm.resetFields()
    getNotificationChannels()
      .then((channels) => setTestChannelOptions(channels.filter((c) => c.enabled !== false).map((c) => ({ value: c.id as string, label: c.name ?? '', type: c.type }))))
      .catch(() => {
        // 静默，错误已由全局请求中间件提示
      })
    setTestOpen(true)
  }

  const handleTest = async () => {
    if (!testTarget?.id) return
    const values = await testForm.validateFields()
    const variables: Record<string, string> = {}
    for (const variable of testVariables) {
      variables[variable.name] = (values[`var_${variable.name}`] ?? '') as string
    }
    // 试发渠道为 SMTP/短信时需动态收件人（邮箱/手机号）
    const testChannelType = testChannelOptions.find((option) => option.value === values.channelId)?.type
    const kind = testChannelType === 'smtp' ? 'email' : testChannelType === 'aliyunSms' || testChannelType === 'tencentSms' ? 'phone' : null
    const recipients = kind !== null ? ((values.recipients ?? []) as string[]) : undefined
    if (kind !== null && recipients?.length === 0) {
      feedback.error(t(kind === 'email' ? 'notification.error.testRecipientsRequired' : 'notification.error.testRecipientsRequiredSms'))
      return
    }
    setTesting(true)
    try {
      const result = await testNotificationTemplate({
        templateId: testTarget.id,
        variables,
        channelId: values.channelId || undefined,
        recipients,
      })
      setTestResult(result)
      if (values.channelId) {
        if (result.success) {
          feedback.success(t('notification.testSuccess', { ms: result.durationMs ?? 0 }))
        } else {
          feedback.error(result.error || t('notification.testFailed'))
        }
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTesting(false)
    }
  }

  const columns: TableColumnsType<NotificationTemplateItem> = useMemo(
    () => [
      {
        title: t('notification.colName'),
        dataIndex: 'name',
        width: 210,
        // 省略号只作用于名称文本：列宽不足时收缩名称，内置 Tag 保持完整可见
        render: (_, record) => (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, minWidth: 0 }}>
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', minWidth: 0 }}>{record.name}</span>
            {record.isBuiltIn === true && (
              <Tooltip title={t('notification.template.builtinTip')}>
                <Tag color="purple" style={{ flex: 'none', marginInlineEnd: 0 }}>{t('notification.template.builtinTag')}</Tag>
              </Tooltip>
            )}
          </div>
        ),
      },
      {
        title: t('notification.template.colVariables'),
        dataIndex: 'variables',
        width: 240,
        ellipsis: true,
        render: (_, record) => {
          const variables = parseTemplateVariables(record.variables)
          if (variables.length === 0) return <Text type="secondary">{t('notification.template.noVariables')}</Text>
          return (
            <Tooltip title={variables.map((v) => `{{${v.name}}} ${v.description ?? ''}`.trim()).join('\n')} placement="topLeft">
              <span>
                {variables.map((v) => (
                  <Tag key={v.name} style={{ marginBottom: 2 }}>{`{{${v.name}}}`}</Tag>
                ))}
              </span>
            </Tooltip>
          )
        },
      },
      {
        title: t('notification.template.colContentType'),
        dataIndex: 'contentType',
        width: 90,
        render: (v: NotificationContentTypeValue) => (v === 'html' ? <Tag color="orange">HTML</Tag> : <Tag color="default">{t('notification.template.contentTypeText')}</Tag>),
      },
      {
        title: t('notification.template.colPlatformMapping'),
        key: 'mapping',
        width: 200,
        ellipsis: true,
        render: (_, record) => {
          if (!record.aliyunTemplateCode && !record.tencentTemplateId) return '-'
          return (
            <Space size={4} wrap>
              {record.aliyunTemplateCode && <Tag color="geekblue">{`阿里云: ${record.aliyunTemplateCode}`}</Tag>}
              {record.tencentTemplateId && <Tag color="cyan">{`腾讯云: ${record.tencentTemplateId}`}</Tag>}
            </Space>
          )
        },
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
        width: 260,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Button type="link" size="small" icon={<SendOutlined />} onClick={() => openTest(record)}>
              {t('notification.template.test')}
            </Button>
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
              {t('notification.edit')}
            </Button>
            {record.isBuiltIn !== true && (
              <Popconfirm title={t('notification.template.deleteConfirm', { name: record.name ?? '' })} onConfirm={() => void handleDelete(record)}>
                <Button type="link" size="small" icon={<DeleteOutlined />} danger>
                  {t('notification.delete')}
                </Button>
              </Popconfirm>
            )}
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <QueryBar
        form={searchForm}
        onSearch={() => void load()}
        onReset={() => {
          searchForm.resetFields()
          void load()
        }}
        loading={loading}
        extra={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('notification.template.create')}
          </Button>
        }
      >
        <Form.Item name="keyword">
          <Input placeholder={t('notification.searchName')} allowClear maxLength={100} style={{ width: 200 }} />
        </Form.Item>
      </QueryBar>
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<NotificationTemplateItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1400, y: panelTableScrollY }}
          pagination={false}
        />
      </div>

      <Modal
        open={modalOpen}
        title={editing ? t('notification.template.editTitle', { name: editing.name ?? '' }) : t('notification.template.createTitle')}
        onOk={() => void handleSave()}
        onCancel={() => setModalOpen(false)}
        okText={t('notification.save')}
        cancelText={t('notification.cancel')}
        confirmLoading={submitting}
        width={720}
        styles={{ body: modalBodyStyle }}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('notification.template.formName')}
            rules={[{ required: true, message: t('notification.error.nameRequired') }, { max: 100, message: t('notification.error.nameMax') }]}
          >
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item name="contentType" label={t('notification.template.formContentType')} initialValue="text" extra={t('notification.template.formContentTypeExtra')}>
            <Radio.Group
              options={[
                { value: 'text', label: t('notification.template.contentTypeText') },
                { value: 'html', label: 'HTML' },
              ]}
            />
          </Form.Item>
          <Form.Item name="title" label={t('notification.template.formTitle')} rules={[{ max: 256, message: t('notification.template.error.titleMax') }]} extra={t('notification.template.formTitleExtra')}>
            <Input maxLength={256} placeholder={t('notification.template.formTitlePlaceholder')} />
          </Form.Item>
          <Form.Item name="content" label={t('notification.template.formContent')} rules={[{ required: true, message: t('notification.template.error.contentRequired') }]} extra={t('notification.template.formContentExtra')}>
            <Input.TextArea rows={formContentType === 'html' ? 10 : 5} maxLength={10000} showCount placeholder={formContentType === 'html' ? t('notification.template.formContentHtmlPlaceholder') : t('notification.template.formContentPlaceholder')} />
          </Form.Item>

          <Form.Item label={t('notification.template.formVariables')} extra={t('notification.template.formVariablesExtra')} style={{ marginBottom: 0 }}>
            <Form.List name="variables">
              {(fields, { add, remove }) => (
                <>
                  {fields.map(({ key, name, ...restField }) => (
                    <Space key={key} style={{ display: 'flex', marginBottom: 4 }} align="baseline">
                      <Form.Item
                        {...restField}
                        name={[name, 'name']}
                        rules={[
                          { required: true, message: t('notification.template.error.varNameRequired') },
                          { pattern: TEMPLATE_VARIABLE_NAME_RE, message: t('notification.template.error.varNameInvalid') },
                        ]}
                        style={{ marginBottom: 0 }}
                      >
                        <Input placeholder={t('notification.template.varName')} maxLength={50} style={{ width: 150 }} />
                      </Form.Item>
                      <Form.Item {...restField} name={[name, 'description']} style={{ marginBottom: 0 }}>
                        <Input placeholder={t('notification.template.varDescription')} maxLength={100} style={{ width: 220 }} />
                      </Form.Item>
                      <Form.Item {...restField} name={[name, 'example']} style={{ marginBottom: 0 }}>
                        <Input placeholder={t('notification.template.varExample')} maxLength={200} style={{ width: 160 }} />
                      </Form.Item>
                      <Button type="link" size="small" danger icon={<DeleteOutlined />} onClick={() => remove(name)}>
                        {t('notification.delete')}
                      </Button>
                    </Space>
                  ))}
                  <Button type="dashed" block icon={<PlusOutlined />} onClick={() => add({ name: '', description: '', example: '' })}>
                    {t('notification.template.addVariable')}
                  </Button>
                </>
              )}
            </Form.List>
          </Form.Item>

          <Form.Item name="aliyunTemplateCode" label={t('notification.template.formAliyunCode')} rules={[{ max: 128, message: t('notification.template.error.codeMax') }]} extra={t('notification.template.formAliyunCodeExtra')} style={{ marginTop: 16 }}>
            <Input maxLength={128} placeholder="SMS_123456789" autoComplete="off" />
          </Form.Item>
          <Form.Item name="tencentTemplateId" label={t('notification.template.formTencentId')} rules={[{ max: 128, message: t('notification.template.error.codeMax') }]} extra={t('notification.template.formTencentIdExtra')}>
            <Input maxLength={128} placeholder="2012345" autoComplete="off" />
          </Form.Item>
          <Form.Item name="description" label={t('notification.form.description')}>
            <Input maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={testOpen}
        title={t('notification.template.testTitle', { name: testTarget?.name ?? '' })}
        onOk={() => void handleTest()}
        onCancel={() => setTestOpen(false)}
        okText={t('notification.template.testRun')}
        cancelText={t('notification.cancel')}
        confirmLoading={testing}
        width={640}
        styles={{ body: modalBodyStyle }}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={testForm} layout="vertical">
          <Form.Item name="channelId" label={t('notification.template.testChannel')} extra={t('notification.template.testChannelExtra')}>
            <Select allowClear showSearch optionFilterProp="label" placeholder={t('notification.template.testChannelPlaceholder')} options={testChannelOptions} />
          </Form.Item>
          {(() => {
            const selectedType = testChannelOptions.find((option) => option.value === testChannelId)?.type
            if (selectedType !== 'smtp' && selectedType !== 'aliyunSms' && selectedType !== 'tencentSms') return null
            return (
              <Form.Item
                name="recipients"
                label={selectedType === 'smtp' ? t('notification.form.testRecipients') : t('notification.form.testPhoneNumbers')}
                rules={[{ required: true, message: t(selectedType === 'smtp' ? 'notification.error.testRecipientsRequired' : 'notification.error.testRecipientsRequiredSms') }]}
                extra={t(selectedType === 'smtp' ? 'notification.form.testRecipientsRowExtra' : 'notification.form.testPhoneNumbersRowExtra')}
              >
                <Select mode="tags" open={false} tokenSeparators={[',', '，', ' ']} placeholder={selectedType === 'smtp' ? 'a@example.com' : '13800138000'} />
              </Form.Item>
            )
          })()}
          {testVariables.map((variable) => (
            <Form.Item
              key={variable.name}
              name={`var_${variable.name}`}
              label={`{{${variable.name}}}`}
              rules={[{ required: true, message: t('notification.error.templateVarRequired') }]}
              extra={variable.description}
            >
              <Input placeholder={variable.example} maxLength={500} />
            </Form.Item>
          ))}
        </Form>
        {testResult && (
          <div style={{ marginTop: 8 }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 4 }}>
              {t('notification.template.renderedTitle')}：{testResult.renderedTitle || '-'}
            </Typography.Paragraph>
            <div style={{ background: 'rgba(128,128,128,0.08)', borderRadius: 8, padding: '8px 12px', whiteSpace: 'pre-wrap', wordBreak: 'break-all', fontSize: 12, maxHeight: 220, overflow: 'auto' }}>
              {testResult.renderedContent || '-'}
            </div>
            {testChannelId && (
              <Typography.Paragraph style={{ marginBottom: 0, marginTop: 8 }} type={testResult.success ? 'success' : 'danger'}>
                {testResult.success ? t('notification.template.testSendSuccess', { ms: testResult.durationMs ?? 0 }) : `${t('notification.template.testSendFailed')}: ${testResult.error ?? ''}`}
              </Typography.Paragraph>
            )}
          </div>
        )}
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
      { title: t('notification.colCreate'), key: 'create', width: 150, render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} /> },
      { title: t('notification.colUpdate'), key: 'update', width: 150, render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} /> },
      { title: t('notification.colChannelName'), dataIndex: 'channelName', width: 150, ellipsis: true },
      {
        title: t('notification.colType'),
        dataIndex: 'channelType',
        width: 110,
        render: (v: NotificationChannelTypeValue) => <Tag color={TYPE_COLORS[v] ?? 'default'}>{t(`notification.type.${v}`)}</Tag>,
      },
      { title: t('notification.colLevel'), dataIndex: 'level', width: 90, render: (v: NotificationLevelValue) => renderLevel(v) },
      { title: t('notification.colStatus'), dataIndex: 'status', width: 100, render: (v: NotificationSendStatusValue) => renderStatus(v) },
      {
        title: t('notification.colTargetUsers'),
        dataIndex: 'targetUserNames',
        width: 160,
        ellipsis: true,
        render: (v: string | null) =>
          v ? (
            <Tooltip title={v} placement="topLeft">
              <Text style={{ fontSize: 12 }}>{v}</Text>
            </Tooltip>
          ) : (
            '-'
          ),
      },
      {
        title: t('notification.colRecipients'),
        dataIndex: 'recipients',
        width: 180,
        ellipsis: true,
        render: (v: string | null) =>
          v ? (
            <Tooltip title={v} placement="topLeft">
              <Text style={{ fontSize: 12 }}>{v}</Text>
            </Tooltip>
          ) : (
            '-'
          ),
      },
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
        form={form}
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
          scroll={{ x: 1920, y: panelTableScrollY }}
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
