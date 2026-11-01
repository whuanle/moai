import { getApiClient } from '@/api/kiota'

/** 通知渠道类型（与后端 NotificationChannelType 的 JsonPropertyName 一致） */
export type NotificationChannelTypeValue =
  | 'feishuWebhook'
  | 'dingtalkWebhook'
  | 'wecomWebhook'
  | 'telegramBot'
  | 'webhook'
  | 'smtp'
  | 'inSite'

/** 通知级别 */
export type NotificationLevelValue = 'info' | 'warning' | 'error'

/** 发送状态 */
export type NotificationSendStatusValue = 'pending' | 'success' | 'failed'

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
  error?: string
  durationMs?: number
  createTime?: string | null
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

/** 测试通知渠道：id 为空时使用传入的类型与配置试发（仅管理员） */
export async function testNotificationChannel(payload: { id?: string; type?: NotificationChannelTypeValue; config?: string }): Promise<{ success?: boolean; error?: string; durationMs?: number }> {
  const client = getApiClient()
  const res = await client.api.notification.channel.test.post({ ...payload })
  return (res ?? {}) as { success?: boolean; error?: string; durationMs?: number }
}

/** 广播通知：异步投递全部启用渠道（仅管理员） */
export async function broadcastNotification(payload: { title: string; content: string; level: NotificationLevelValue }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.notification.broadcast.post({ ...payload })
  return (res as unknown as { channelCount?: number })?.channelCount ?? 0
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
