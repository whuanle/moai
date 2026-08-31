import { useMemo } from 'react'
import { App } from 'antd'
import { createFeedback, type Feedback } from './feedback'

/** 在组件内获取语义化的 message/notification 反馈。 */
export function useFeedback(): Feedback {
  const { message, notification } = App.useApp()
  return useMemo(() => createFeedback({ message, notification }), [message, notification])
}
