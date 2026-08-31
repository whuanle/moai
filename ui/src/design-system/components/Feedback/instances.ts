import type { MessageInstance } from 'antd/es/message/interface'
import type { NotificationInstance } from 'antd/es/notification/interface'

let messageInstance: MessageInstance | undefined
let notificationInstance: NotificationInstance | undefined

/** 在 App 上下文就绪后，将 antd 的 message/notification 实例注册到全局，供非组件代码（如 API 层）复用。 */
export function registerFeedbackInstances(
  message: MessageInstance,
  notification: NotificationInstance,
): void {
  messageInstance = message
  notificationInstance = notification
}

export function getMessageInstance(): MessageInstance | undefined {
  return messageInstance
}

export function getNotificationInstance(): NotificationInstance | undefined {
  return notificationInstance
}
