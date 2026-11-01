import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { CSSProperties } from 'react'
import { feedback, themePresets } from '@/design-system'
import { getMessages } from '@/api/message'
import { Env } from '@/config/env'
import { useAppStore } from '@/store/app'
import { useNotificationStore } from '@/store/notification'

/** /hub/moai onEvent 事件负载：只推事件不推数据，data 仅标识类字段 */
export interface EventPushPayload {
  event: string
  data?: Record<string, string>
  time?: string
}

/** 事件名（对齐后端 MoAI.Message.Models.EventNames） */
export const EVENT_MESSAGE_NEW = 'message.new'

type EventHandler = (payload: EventPushPayload) => void

const handlers = new Set<EventHandler>()

let connection: HubConnection | null = null
let starting: Promise<void> | null = null
let retryTimer: ReturnType<typeof setTimeout> | null = null
let navigateRef: ((path: string) => void) | null = null
let toastTimer: ReturnType<typeof setTimeout> | null = null

/** 注册业务事件处理器（如消息页在线刷新），返回取消函数 */
export function onEventPushEvent(handler: EventHandler): () => void {
  handlers.add(handler)
  return () => handlers.delete(handler)
}

/** 供 toast 点击跳转使用：由组件层注册 SPA navigate，未注册时退化为整页跳转 */
export function setEventPushNavigate(navigate: ((path: string) => void) | null): void {
  navigateRef = navigate
}

function goToMessages(): void {
  if (navigateRef) navigateRef('/account/messages')
  else window.location.href = '/account/messages'
}

// 主题 key 异常时的兜底调色板（经典亮色）
const themePalettesFallback = { accent: '#6D5AE6', bgElevated: '#FFFFFF' }

/** 通知卡片样式：当前主题强调色渐变底 + 描边，避免白底卡片浮在浅色页面上不显眼 */
function buildNotifyStyle(): CSSProperties {
  const palette = themePresets[useAppStore.getState().themeKey]?.palette ?? themePalettesFallback
  const accent = palette.accent
  return {
    background: `linear-gradient(135deg, ${accent}1F 0%, ${palette.bgElevated} 45%)`,
    border: `1px solid ${accent}59`,
    boxShadow: `0 6px 16px 0 ${accent}26, 0 3px 6px -4px rgba(0, 0, 0, 0.12)`,
    borderRadius: 8,
  }
}

/** message.new → 刷新角标 + 右上角 Notification（带标题，悬浮不自动关闭，刷新页面自然消失） */
function handleEvent(payload: EventPushPayload): void {
  for (const handler of handlers) {
    try {
      handler(payload)
    } catch {
      // 业务处理器异常不阻断其它处理器
    }
  }

  if (payload.event !== EVENT_MESSAGE_NEW) return

  useNotificationStore.getState().refresh()

  // 合并窗口：短时间多条事件只弹一条，展示最新一条的标题
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => {
    toastTimer = null
    void (async () => {
      try {
        const res = await getMessages({ pageNo: 1, pageSize: 1 })
        const latest = res.items[0]
        if (!latest) return
        feedback.notify(latest.title ?? '', {
          description: latest.content && latest.content.length > 120 ? `${latest.content.slice(0, 120)}…` : latest.content ?? undefined,
          duration: 0,
          key: `message-new-${latest.id ?? Date.now()}`,
          onClick: goToMessages,
          style: buildNotifyStyle(),
        })
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    })()
  }, 800)
}

async function createConnection(): Promise<HubConnection> {
  const conn = new HubConnectionBuilder()
    .withUrl(`${Env.serverUrl}/hub/moai`, {
      // 实时读取 store：token 刷新/换发后重连自动带新 token
      accessTokenFactory: () => useAppStore.getState().userInfo?.accessToken ?? '',
      // 平台用 token 鉴权（不依赖 cookie）：必须关闭 withCredentials，
      // 否则浏览器以 credentials 模式发起请求，与 AllowAnyOrigin 的 CORS 策略冲突直接 Failed to fetch
      withCredentials: false,
    })
    .withAutomaticReconnect([0, 3000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build()

  conn.on('onEvent', handleEvent)
  conn.onreconnected(() => {
    // 断线期间错过的状态统一补拉
    void useNotificationStore.getState().refresh()
  })
  return conn
}

/** 建立 /hub/moai 连接（幂等）；失败静默并在 15s 后自动重试（首连失败不归 withAutomaticReconnect 管） */
export async function startEventPush(): Promise<void> {
  if (connection || starting) return
  if (!useAppStore.getState().userInfo?.accessToken) return
  starting = (async () => {
    try {
      const conn = await createConnection()
      await conn.start()
      connection = conn
    } catch {
      // 后端暂不可达时静默（站内信仍有拉取兜底），定时重试直到登录态失效
      if (useAppStore.getState().userInfo?.accessToken && !retryTimer) {
        retryTimer = setTimeout(() => {
          retryTimer = null
          void startEventPush()
        }, 15000)
      }
    } finally {
      starting = null
    }
  })()
  await starting
}

/** 当前连接状态（诊断用）：未建立连接时返回 null */
export function getEventPushState(): string | null {
  return connection?.state ?? null
}

/** 断开连接（登出/token 失效时由 useEventPush 调用） */
export async function stopEventPush(): Promise<void> {
  if (retryTimer) {
    clearTimeout(retryTimer)
    retryTimer = null
  }
  const conn = connection
  connection = null
  if (!conn) return
  try {
    await conn.stop()
  } catch {
    // 停止失败无需处理
  }
}
