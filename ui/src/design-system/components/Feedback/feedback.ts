import type { ReactNode } from 'react'
import type { MessageInstance } from 'antd/es/message/interface'
import type { NotificationInstance } from 'antd/es/notification/interface'
import type { NotificationPlacement } from 'antd/es/notification/interface'
import i18n from '@/i18n'
import { extractErrorMessage, getHttpStatus, isNetworkError } from './error'
import { getMessageInstance, getNotificationInstance } from './instances'

/** message（轻量 toast）的可选配置。 */
export interface FeedbackMessageOptions {
  duration?: number
  key?: string | number
  icon?: ReactNode
  onClose?: () => void
}

/** notification（醒目通知）的可选配置。 */
export interface FeedbackNotificationOptions {
  description?: ReactNode
  duration?: number | null
  key?: string | number
  placement?: NotificationPlacement
  onClick?: () => void
  closable?: boolean
}

/**
 * 消息/通知的统一反馈出口，语义化地区分轻重：
 * - message 通道：普通业务（成功、业务错误、警告、信息、加载）。
 * - notification 通道：消息提示、系统通知、严重错误（500/网络等）。
 */
export interface Feedback {
  success(content: ReactNode, options?: FeedbackMessageOptions): void
  error(content: ReactNode, options?: FeedbackMessageOptions): void
  warning(content: ReactNode, options?: FeedbackMessageOptions): void
  info(content: ReactNode, options?: FeedbackMessageOptions): void
  loading(content: ReactNode, options?: FeedbackMessageOptions): void

  notify(content: ReactNode, options?: FeedbackNotificationOptions): void
  notifySuccess(content: ReactNode, options?: FeedbackNotificationOptions): void
  notifyWarning(content: ReactNode, options?: FeedbackNotificationOptions): void
  notifyError(content: ReactNode, options?: FeedbackNotificationOptions): void

  /** 统一 API 错误路由：服务器 500/网络异常 → notification，业务 4xx → message。 */
  handleError(err: unknown): void
}

export interface FeedbackConnector {
  message: MessageInstance
  notification: NotificationInstance
}

type MessageAccessor = () => MessageInstance | undefined
type NotificationAccessor = () => NotificationInstance | undefined
type MessageTypeKey = 'success' | 'error' | 'warning' | 'info' | 'loading'
type NotificationTypeKey = 'success' | 'error' | 'warning' | 'info'

function warnUnregistered(): void {
  console.warn('[feedback] antd App 实例尚未注册，请确认 <FeedbackBridge /> 已挂载到 <App /> 内。')
}

function openMessage(
  instance: MessageInstance,
  type: MessageTypeKey,
  content: ReactNode,
  options?: FeedbackMessageOptions,
): void {
  if (options?.key !== undefined) {
    instance.open({
      type,
      content,
      duration: options.duration,
      onClose: options.onClose,
      key: options.key,
      icon: options.icon,
    })
    return
  }
  instance[type](content, options?.duration, options?.onClose)
}

/** 根据访问器构建反馈对象，未注册时安全降级为 no-op。 */
export function buildFeedback(
  getMessage: MessageAccessor,
  getNotification: NotificationAccessor,
): Feedback {
  const openToast = (type: MessageTypeKey, content: ReactNode, options?: FeedbackMessageOptions): void => {
    const instance = getMessage()
    if (!instance) {
      warnUnregistered()
      return
    }
    openMessage(instance, type, content, options)
  }
  const openNotify = (
    type: NotificationTypeKey,
    content: ReactNode,
    options?: FeedbackNotificationOptions,
  ): void => {
    const instance = getNotification()
    if (!instance) {
      warnUnregistered()
      return
    }
    instance[type]({ message: content, ...options })
  }

  return {
    success: (content, options) => openToast('success', content, options),
    error: (content, options) => openToast('error', content, options),
    warning: (content, options) => openToast('warning', content, options),
    info: (content, options) => openToast('info', content, options),
    loading: (content, options) => openToast('loading', content, options),

    notify: (content, options) => openNotify('info', content, options),
    notifySuccess: (content, options) => openNotify('success', content, options),
    notifyWarning: (content, options) => openNotify('warning', content, options),
    notifyError: (content, options) => openNotify('error', content, options),

    handleError: (err) => {
      const message = getMessage()
      const notification = getNotification()
      if (!message || !notification) {
        warnUnregistered()
        return
      }
      handleApiError({ message, notification }, err)
    },
  }
}

/** 组件内使用：基于 antd App.useApp() 的语义化反馈。 */
export function createFeedback(connector: FeedbackConnector): Feedback {
  return buildFeedback(() => connector.message, () => connector.notification)
}

/** 全局反馈对象：可在组件外调用（API 层、store、工具），基于已注册的 App 实例。 */
export const feedback: Feedback = buildFeedback(getMessageInstance, getNotificationInstance)

export function handleApiError(connector: FeedbackConnector, err: unknown): void {
  const status = getHttpStatus(err)
  const detail = extractErrorMessage(err)

  if (status === undefined) {
    if (isNetworkError(err)) {
      connector.notification.error({
        message: i18n.t('feedback.networkError'),
        description: i18n.t('feedback.networkErrorDesc'),
      })
    } else {
      connector.notification.error({ message: i18n.t('feedback.unknownError') })
    }
    return
  }

  if (status >= 500) {
    connector.notification.error({
      message: i18n.t('feedback.serverError'),
      description: detail ?? i18n.t('feedback.serverErrorDesc'),
    })
    return
  }

  connector.message.error(detail ?? i18n.t('feedback.businessError'))
}
