import { useEffect } from 'react'
import { useAppStore } from '@/store/app'
import { useNotificationStore } from '@/store/notification'
import { setEventPushNavigate, startEventPush, stopEventPush } from '@/realtime/eventPush'

/**
 * 站内信/事件推送连接生命周期：登录后建立 /hub/moai 连接，登出（token 清空）自动断开；
 * 未读角标初始拉一次，后续由 message.new 事件驱动刷新。
 */
export function useEventPush(navigate: (path: string) => void): void {
  const accessToken = useAppStore((state) => state.userInfo?.accessToken)

  useEffect(() => {
    setEventPushNavigate(navigate)
    if (!accessToken) {
      void stopEventPush()
      useNotificationStore.getState().setUnreadCount(0)
      return
    }
    void startEventPush()
    void useNotificationStore.getState().refresh()
    return () => {
      setEventPushNavigate(null)
    }
  }, [accessToken, navigate])

  // 卸载时断开（AppLayout 只在登出/整页跳转时卸载，stop 幂等）
  useEffect(() => {
    return () => {
      void stopEventPush()
    }
  }, [])
}
