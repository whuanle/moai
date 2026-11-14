import { getApiClient } from '@/api/kiota'
import type { MessageItem } from '@/api/client/models'

export type { MessageItem }

export interface MessageListParams {
  pageNo: number
  pageSize: number
  /** 按已读状态过滤，空为全部 */
  isRead?: boolean
}

export interface MessageListResult {
  items: MessageItem[]
  total: number
  pageNo: number
  pageSize: number
}

/** 分页查询我的站内信，按发送时间倒序（items 内 createUserName 为发送人名，系统消息为空） */
export async function getMessages(params: MessageListParams): Promise<MessageListResult> {
  const client = getApiClient()
  const res = await client.api.message.list.get({
    queryParameters: {
      pageNo: params.pageNo,
      pageSize: params.pageSize,
      isRead: params.isRead,
    },
  })
  return {
    items: res?.items ?? [],
    total: res?.total ?? 0,
    pageNo: res?.pageNo ?? params.pageNo,
    pageSize: res?.pageSize ?? params.pageSize,
  }
}

/** 查询我的未读站内信数量 */
export async function getUnreadCount(): Promise<number> {
  const client = getApiClient()
  const res = await client.api.message.unreadCount.get()
  return Number(res?.value ?? 0)
}

/** 标记站内信已读 */
export async function markMessagesRead(messageIds: string[]): Promise<void> {
  const client = getApiClient()
  await client.api.message.read.post({ messageIds })
}

/** 全部标记已读 */
export async function markAllMessagesRead(): Promise<void> {
  const client = getApiClient()
  await client.api.message.readAll.post()
}

/** 删除站内信（软删除） */
export async function deleteMessages(messageIds: string[]): Promise<void> {
  const client = getApiClient()
  await client.api.message.deletePath.post({ messageIds })
}
