import { create } from 'zustand'

/**
 * 全屏外壳标记：流程应用工作台（设计/调试/配置/运行历史）统一去掉 AppLayout 全局内边距。
 * design 分区本身由 AppLayout 的 URL 规则命中，其余分区依赖此标记（URL 无法区分 Agent/流程应用的同名分区）。
 */
interface ShellState {
  fullscreen: boolean
  setFullscreen: (fullscreen: boolean) => void
}

export const useShellStore = create<ShellState>((set) => ({
  fullscreen: false,
  setFullscreen: (fullscreen) => set({ fullscreen }),
}))
