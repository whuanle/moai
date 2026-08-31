import { useEffect } from 'react'
import { App } from 'antd'
import { registerFeedbackInstances } from './instances'

/** 挂载在 <App /> 内，把 antd 的 message/notification 实例注册到全局，供 API 层等非组件代码复用。 */
export function FeedbackBridge() {
  const { message, notification } = App.useApp()

  useEffect(() => {
    registerFeedbackInstances(message, notification)
  }, [message, notification])

  return null
}
