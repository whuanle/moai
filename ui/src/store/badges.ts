import { create } from 'zustand'

/**
 * 侧边栏菜单角标（待处理数量）全局状态。
 * key 与 AppSider NavItem.key 对齐（如 logEvents），后续菜单需要角标时在此注册数据源即可。
 */
interface BadgeState {
  counts: Record<string, number>
  setBadge: (key: string, count: number) => void
  clearBadge: (key: string) => void
}

export const useBadgeStore = create<BadgeState>()((set) => ({
  counts: {},
  setBadge: (key, count) =>
    set((state) => (state.counts[key] === count ? state : { counts: { ...state.counts, [key]: count } })),
  clearBadge: (key) =>
    set((state) => {
      if (!(key in state.counts)) return state
      const next = { ...state.counts }
      delete next[key]
      return { counts: next }
    }),
}))
