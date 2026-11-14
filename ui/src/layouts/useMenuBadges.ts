import { useEffect } from 'react'
import { useAppStore } from '@/store/app'
import { useBadgeStore } from '@/store/badges'
import { getRiskEventUnreadCount } from '@/api/loginsafety'
import { getPublicationPendingCount } from '@/api/publication'
import { getNotificationFailedCount } from '@/api/notification'

const POLL_INTERVAL_MS = 60_000

/**
 * 菜单角标数据源轮询：管理员登录后每 60s 拉取一次各菜单待处理数量写入 badge store。
 * 数据源并行拉取、互不阻塞，单个失败只跳过该角标等下一轮；key 与 AppSider NavItem.key 对齐。
 */
export function useMenuBadges() {
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const setBadge = useBadgeStore((state) => state.setBadge)

  useEffect(() => {
    if (!isAdmin) {
      return
    }

    let disposed = false

    const refresh = async () => {
      const sources: Array<[key: string, loader: () => Promise<number>]> = [
        ['logEvents', getRiskEventUnreadCount],
        ['publications', getPublicationPendingCount],
        ['notificationCenter', getNotificationFailedCount],
      ]

      await Promise.all(
        sources.map(async ([key, loader]) => {
          try {
            const count = await loader()
            if (!disposed) {
              setBadge(key, count)
            }
          } catch {
            // 401/网络错误静默，下一轮再试
          }
        }),
      )
    }

    void refresh()
    const timer = window.setInterval(() => void refresh(), POLL_INTERVAL_MS)

    return () => {
      disposed = true
      window.clearInterval(timer)
    }
  }, [isAdmin, setBadge])
}
