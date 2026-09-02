import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { ThemeKey } from '@/design-system/theme'

export type ThemeMode = ThemeKey
export type Locale = 'zh-CN' | 'en-US'

export interface ServerInfo {
  serviceUrl: string
  publicStoreUrl: string
  rsaPublic: string
}

export interface UserInfo {
  accessToken?: string | null
  expiresIn?: string | null
  refreshToken?: string | null
  tokenType?: string | null
  userId?: string | null | undefined
  userName?: string | null
  email?: string | null
  nickName?: string | null
  phone?: string | null
  avatar?: string | null
  isDisable?: boolean | null
  isAdmin?: boolean | null
  isRoot?: boolean | null
  isDeleted?: boolean | null
}

interface AppState {
  themeKey: ThemeKey
  locale: Locale
  serverInfo: ServerInfo | null
  userInfo: UserInfo | null

  setThemeKey: (key: ThemeKey) => void
  toggleTheme: () => void
  setLocale: (locale: Locale) => void
  setServerInfo: (info: ServerInfo) => void
  clearServerInfo: () => void
  setUserInfo: (info: UserInfo) => void
  clearUserInfo: () => void
}

const getInitialTheme = (): ThemeKey => {
  const saved = localStorage.getItem('moai-web-theme')
  if (saved === 'light' || saved === 'dark') return saved
  if (window.matchMedia?.('(prefers-color-scheme: dark)').matches) return 'dark'
  return 'light'
}

const getInitialLocale = (): Locale => {
  const saved = localStorage.getItem('moai-web-locale')
  if (saved === 'zh-CN' || saved === 'en-US') return saved
  return 'zh-CN'
}

export const useAppStore = create<AppState>()(
  persist(
    (set, get) => ({
      themeKey: getInitialTheme(),
      locale: getInitialLocale(),
      serverInfo: null,
      userInfo: null,

      setThemeKey: (key) => {
        localStorage.setItem('moai-web-theme', key)
        set({ themeKey: key })
      },
      toggleTheme: () => {
        const next = get().themeKey === 'dark' ? 'light' : 'dark'
        localStorage.setItem('moai-web-theme', next)
        set({ themeKey: next })
      },
      setLocale: (locale) => {
        localStorage.setItem('moai-web-locale', locale)
        set({ locale })
      },
      setServerInfo: (info) => set({ serverInfo: info }),
      clearServerInfo: () => set({ serverInfo: null }),
      setUserInfo: (info) => set({ userInfo: info }),
      clearUserInfo: () => set({ userInfo: null }),
    }),
    {
      name: 'moai-web-store',
      partialize: (state) => ({
        serverInfo: state.serverInfo,
        userInfo: state.userInfo,
      }),
    },
  ),
)
