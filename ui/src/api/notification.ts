import { getApiClient } from '@/api/kiota'

/** 通知渠道类型（与后端 NotificationChannelType 的 JsonPropertyName 一致） */
export type NotificationChannelTypeValue =
  | 'feishuWebhook'
  | 'dingtalkWebhook'
  | 'wecomWebhook'
  | 'feishuAppBot'
  | 'dingtalkAppBot'
  | 'wecomAppBot'
  | 'telegramBot'
  | 'webhook'
  | 'smtp'
  | 'inSite'
  | 'slack'
  | 'discord'
  | 'microsoftTeams'
  | 'gotify'
  | 'ntfy'
  | 'bark'
  | 'serverChan'
  | 'pushPlus'
  | 'aliyunSms'
  | 'tencentSms'

/** 通知级别 */
export type NotificationLevelValue = 'info' | 'warning' | 'error'

/** 发送状态 */
export type NotificationSendStatusValue = 'pending' | 'success' | 'failed'

/** 通知内容类型 */
export type NotificationContentTypeValue = 'text' | 'html'

/** 通知模板变量定义 */
export interface NotificationTemplateVariableItem {
  /** 变量名（字母开头，仅字母/数字/下划线） */
  name: string
  /** 变量说明 */
  description?: string
  /** 示例值（便于复制到外部平台提审时对照） */
  example?: string
}

/** 通知模板 */
export interface NotificationTemplateItem {
  id?: string
  /** 是否平台内置模板（内置模板不可删除、可编辑） */
  isBuiltIn?: boolean
  name?: string
  /** 通知主题模板（邮件主题用，短信无主题） */
  title?: string
  /** 通知正文模板（纯文本或 HTML，含 {变量} 占位符） */
  content?: string
  contentType?: NotificationContentTypeValue
  /** 变量定义 JSON（[{"name","description","example"}]） */
  variables?: string
  /** 阿里云短信模板 Code（平台审核通过后回填） */
  aliyunTemplateCode?: string
  /** 腾讯云短信模板 Id（平台审核通过后回填） */
  tencentTemplateId?: string
  description?: string
  createUserId?: number
  createUserName?: string
  createTime?: string | null
  updateUserId?: number
  updateUserName?: string
  updateTime?: string | null
}

export interface NotificationTemplateSavePayload {
  name: string
  /** 通知主题模板 */
  title: string
  /** 通知正文模板 */
  content: string
  contentType: NotificationContentTypeValue
  /** 变量定义 JSON 字符串 */
  variables: string
  /** 阿里云短信模板 Code */
  aliyunTemplateCode?: string
  /** 腾讯云短信模板 Id */
  tencentTemplateId?: string
  description: string
}

/** 通知模板测试/预览结果 */
export interface NotificationTemplateTestResult {
  renderedTitle?: string
  renderedContent?: string
  success?: boolean
  error?: string
  durationMs?: number
}

/** 通知渠道 */
export interface NotificationChannelItem {
  id?: string
  name?: string
  type?: NotificationChannelTypeValue
  /** 渠道配置 JSON（结构与 type 对应） */
  config?: string
  enabled?: boolean
  description?: string
  createUserId?: number
  createUserName?: string
  createTime?: string | null
  updateUserId?: number
  updateUserName?: string
  updateTime?: string | null
}

/** 通知发送记录 */
export interface NotificationLogItem {
  id?: string
  channelId?: string
  channelName?: string
  channelType?: NotificationChannelTypeValue
  level?: NotificationLevelValue
  title?: string
  content?: string
  status?: NotificationSendStatusValue
  /** 关联用户 id 快照（逗号分隔，定向个人推送时记录收件人；空为广播/全员） */
  targetUserIds?: string
  /** 实际收件人快照（逗号分隔；SMTP 渠道记邮箱、短信渠道记手机号；固定地址渠道留空） */
  recipients?: string | null
  /** 关联用户名（逗号分隔，按快照 id 回填） */
  targetUserNames?: string
  error?: string
  durationMs?: number
  createTime?: string | null
  createUserId?: number
  createUserName?: string
  updateUserId?: number
  updateUserName?: string
  updateTime?: string | null
}

interface PagedResponse<T> {
  totalCount?: number
  items?: T[] | null
}

export interface NotificationChannelSavePayload {
  name: string
  type: NotificationChannelTypeValue
  /** 渠道配置 JSON 字符串 */
  config: string
  enabled: boolean
  description: string
}

/** 查询通知渠道列表（仅管理员） */
export async function getNotificationChannels(search?: string): Promise<NotificationChannelItem[]> {
  const client = getApiClient()
  const res = await client.api.notification.channel.list.get({ queryParameters: { search: search || undefined } })
  return (res as unknown as { items?: NotificationChannelItem[] | null })?.items ?? []
}

/** 创建通知渠道（仅管理员） */
export async function createNotificationChannel(payload: NotificationChannelSavePayload): Promise<string | undefined> {
  const client = getApiClient()
  const res = await client.api.notification.channel.create.post({ ...payload })
  return (res as unknown as { id?: string })?.id
}

/** 更新通知渠道（仅管理员，类型不可变更） */
export async function updateNotificationChannel(id: string, payload: NotificationChannelSavePayload): Promise<void> {
  const client = getApiClient()
  await client.api.notification.channel.byId(id).put({ ...payload, id })
}

/** 删除通知渠道（仅管理员，历史发送记录保留） */
export async function deleteNotificationChannel(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.notification.channel.byId(id).delete()
}

/** 测试通知渠道：id 为空时使用传入的类型与配置试发（仅管理员）；SMTP/短信渠道须带动态收件人（邮箱/手机号） */
export async function testNotificationChannel(payload: { id?: string; type?: NotificationChannelTypeValue; config?: string; recipients?: string[] }): Promise<{ success?: boolean; error?: string; durationMs?: number }> {
  const client = getApiClient()
  const res = await client.api.notification.channel.test.post({ ...payload })
  return (res ?? {}) as { success?: boolean; error?: string; durationMs?: number }
}

/** 广播通知（仅管理员）：投递目标为「渠道（指定集合或全部启用）」与「站内信」的组合，至少选择其一；使用模板时按变量值渲染 */
export async function broadcastNotification(payload: {
  title: string
  content: string
  level: NotificationLevelValue
  /** 使用的通知模板 id（可选，使用模板时标题/正文由模板插值生成） */
  templateId?: string
  /** 模板变量值表 */
  templateVariables?: Record<string, string>
  /** 指定投递的启用渠道 id 集合 */
  channelIds?: string[]
  /** 是否投递全部启用渠道 */
  allChannels?: boolean
  /** 收件人邮箱列表（所选渠道含 SMTP 邮件渠道时必填） */
  emailRecipients?: string[]
  /** 收件人手机号列表（所选渠道含短信渠道时必填） */
  phoneNumbers?: string[]
  /** 是否同时发送站内信给全部启用用户 */
  sendInSite?: boolean
}): Promise<number> {
  const client = getApiClient()
  const res = await client.api.notification.broadcast.post({ ...payload })
  return (res as unknown as { channelCount?: number })?.channelCount ?? 0
}

/** 查询通知模板列表（仅管理员） */
export async function getNotificationTemplates(search?: string): Promise<NotificationTemplateItem[]> {
  const client = getApiClient()
  const res = await client.api.notification.template.list.get({ queryParameters: { search: search || undefined } })
  return (res as unknown as { items?: NotificationTemplateItem[] | null })?.items ?? []
}

/** 创建通知模板（仅管理员） */
export async function createNotificationTemplate(payload: NotificationTemplateSavePayload): Promise<string | undefined> {
  const client = getApiClient()
  const res = await client.api.notification.template.create.post({ ...payload })
  return (res as unknown as { id?: string })?.id
}

/** 更新通知模板（仅管理员，整体覆盖） */
export async function updateNotificationTemplate(id: string, payload: NotificationTemplateSavePayload): Promise<void> {
  const client = getApiClient()
  await client.api.notification.template.byId(id).put({ ...payload, id })
}

/** 删除通知模板（仅管理员，软删除，历史发送记录保留） */
export async function deleteNotificationTemplate(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.notification.template.byId(id).delete()
}

/** 测试通知模板：渲染预览；channelId 传入时同步真实试发（仅管理员）；试发渠道为 SMTP/短信时须带动态收件人 */
export async function testNotificationTemplate(payload: { templateId: string; variables?: Record<string, string>; channelId?: string; recipients?: string[] }): Promise<NotificationTemplateTestResult> {
  const client = getApiClient()
  const res = await client.api.notification.template.test.post({ ...payload })
  return (res ?? {}) as NotificationTemplateTestResult
}

/** 分页查询通知发送记录（仅管理员） */
export async function getNotificationLogs(query: { pageNo: number; pageSize: number; channelId?: string; status?: NotificationSendStatusValue }): Promise<PagedResponse<NotificationLogItem>> {
  const client = getApiClient()
  const res = await client.api.notification.log.list.get({
    queryParameters: {
      pageNo: query.pageNo,
      pageSize: query.pageSize,
      channelId: query.channelId || undefined,
      status: query.status || undefined,
    },
  })
  return res as unknown as PagedResponse<NotificationLogItem>
}

/** 查询发送失败的记录数量（仅管理员，菜单「通知中心」角标数据源） */
export async function getNotificationFailedCount(): Promise<number> {
  const client = getApiClient()
  const res = await client.api.notification.log.failedCount.get()
  return res?.count ?? 0
}
