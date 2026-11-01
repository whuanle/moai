import { useEffect } from 'react'
import { useAppStore } from '@/store/app'
import { useBadgeStore } from '@/store/badges'
import { getRiskEventUnreadCount } from '@/api/loginsafety'

const POLL_INTERVAL_MS = 60_000

/**
 * 菜单角标数据源轮询：管理员登录后每 60s 拉取一次未读风险事件数写入 badge store。
 * 后续其它菜单需要待处理角标时，在此追加对应数据源即可。
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
      try {
        const count = await getRiskEventUnreadCount()
        if (!disposed) {
          setBadge('logEvents', count)
        }
      } catch {
        // 401/网络错误静默，下一轮再试
      }
    }

    void refresh()
    const timer = window.setInterval(() => void refresh(), POLL_INTERVAL_MS)

    return () => {
      disposed = true
      window.clearInterval(timer)
    }
  }, [isAdmin, setBadge])
}
