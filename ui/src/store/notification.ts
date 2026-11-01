import { create } from 'zustand'
import { getUnreadCount } from '@/api/message'

interface NotificationState {
  /** 未读站内信数量（登录后拉取，message.new 事件与已读/删除操作后刷新） */
  unreadCount: number
  refresh: () => Promise<void>
  setUnreadCount: (count: number) => void
}

export const useNotificationStore = create<NotificationState>((set) => ({
  unreadCount: 0,
  refresh: async () => {
    try {
      const count = await getUnreadCount()
      set({ unreadCount: count })
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  },
  setUnreadCount: (count) => set({ unreadCount: count }),
}))
