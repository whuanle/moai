/**
 * 悬浮对话组件的公开配置（/api/external/app/{appId}/access-point）.
 */
export interface AccessPointConfig {
  appName: string
  avatarUrl?: string | null
  title: string
  subtitle?: string | null
  placeholder?: string | null
  primaryColor?: string | null
  position?: string | null
  launcherText?: string | null
  panelWidth?: number | null
  panelHeight?: number | null
  defaultOpen?: boolean | null
  isAuth?: boolean | null
  enabled?: boolean | null
}

interface TokenResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  tokenType: 'app' | 'user'
  externalId?: number | string | null
  externalUserId?: string | null
}

export interface WidgetOptions {
  appId: string
  server: string
  accessAppKey?: string
  externalUserId?: string
  nickname?: string
}

const TEXT_FALLBACK = {
  inputPlaceholder: '输入消息… / Type a message…',
  needKey: '此应用需要访问密钥 / This app requires an access key',
  sendFailed: '发送失败 / Failed to send',
  initFailed: '初始化失败，请稍后重试 / Initialization failed',
}

const LOG_TAG = '[moai-widget]'

/** 受限嵌入环境（沙箱 iframe 等）访问 localStorage 会抛 SecurityError，降级为内存态（仅当次页面存活） */
const memoryStore = new Map<string, string>()
function storageGet(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return memoryStore.get(key) ?? null
  }
}
function storageSet(key: string, value: string): void {
  try {
    localStorage.setItem(key, value)
  } catch {
    memoryStore.set(key, value)
  }
}

/** crypto.randomUUID 仅在安全上下文可用（宿主页可能是非 localhost 的 http 页面），降级为随机串 */
function randomId(): string {
  const raw = typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`
  return raw.replace(/-/g, '')
}

/** 随机临时外部身份，localStorage 持久化以继承会话 */
function resolveExternalUserId(appId: string, provided?: string): string {
  const key = `moai-widget-uid-${appId}`
  const existing = provided || storageGet(key)
  if (existing) return existing
  const generated = `ext-${randomId().slice(0, 16)}`
  storageSet(key, generated)
  return generated
}

export class MoaiWidget {
  private config?: AccessPointConfig
  private token?: string
  private sessionId?: string
  private root?: HTMLDivElement
  private shadow?: ShadowRoot
  private messagesEl?: HTMLDivElement
  private inputEl?: HTMLTextAreaElement
  private running = false

  constructor(private readonly opts: WidgetOptions) {}

  /** 拉取公开配置；enabled=false 时不渲染任何内容（console.warn 说明原因，便于宿主页排查） */
  async init(): Promise<void> {
    try {
      const res = await fetch(`${this.opts.server}/api/external/app/${this.opts.appId}/access-point`)
      if (!res.ok) {
        console.warn(`${LOG_TAG} 拉取访问点配置失败 status=${res.status}，组件未渲染 / access-point fetch failed, widget not rendered`)
        return
      }
      this.config = (await res.json()) as AccessPointConfig
      if (this.config.enabled === false) {
        console.warn(`${LOG_TAG} 访问点未启用（未发布/已禁用/开关关闭），组件未渲染 / access point disabled, widget not rendered`)
        return
      }
      this.render()
      if (this.config.defaultOpen) {
        void this.open()
      }
    } catch (error) {
      console.error(`${LOG_TAG} 初始化失败，组件未渲染 / initialization failed, widget not rendered`, error)
    }
  }

  private render(): void {
    const cfg = this.config!
    this.root = document.createElement('div')
    this.root.className = 'moai-widget-root'
    this.shadow = this.root.attachShadow({ mode: 'open' })
    const primary = cfg.primaryColor || '#1677ff'
    const side = cfg.position === 'bottomLeft' ? 'left' : 'right'

    this.shadow.innerHTML = `
      <style>
        :host { all: initial; }
        * { box-sizing: border-box; font-family: -apple-system, 'Segoe UI', 'PingFang SC', 'Microsoft YaHei', sans-serif; }
        .launcher {
          position: fixed; bottom: 24px; ${side}: 24px; z-index: 2147483000;
          width: 52px; height: 52px; border-radius: 50%; border: none; cursor: pointer;
          background: var(--moai-primary); color: #fff; font-size: 14px; font-weight: 600;
          box-shadow: 0 6px 16px rgba(0, 0, 0, 0.2); display: flex; align-items: center; justify-content: center;
        }
        .launcher svg { width: 24px; height: 24px; fill: #fff; }
        .panel {
          position: fixed; bottom: 88px; ${side}: 24px; z-index: 2147483000;
          width: var(--panel-w); height: var(--panel-h); max-height: calc(100vh - 120px);
          background: #fff; border-radius: 12px; box-shadow: 0 12px 40px rgba(0, 0, 0, 0.25);
          display: none; flex-direction: column; overflow: hidden;
        }
        .panel.open { display: flex; }
        .header { padding: 14px 16px; background: var(--moai-primary); color: #fff; }
        .header .title { font-size: 15px; font-weight: 600; }
        .header .subtitle { font-size: 12px; opacity: 0.85; margin-top: 2px; white-space: pre-wrap; }
        .messages { flex: 1; overflow-y: auto; padding: 12px; background: #f5f6f8; }
        .msg { max-width: 82%; padding: 8px 12px; border-radius: 10px; margin-bottom: 8px;
               font-size: 13px; line-height: 1.6; white-space: pre-wrap; word-break: break-word; }
        .msg.user { background: var(--moai-primary); color: #fff; margin-left: auto; }
        .msg.assistant { background: #fff; color: #1f2329; border: 1px solid #e5e6eb; }
        .input-row { display: flex; gap: 8px; padding: 10px; border-top: 1px solid #e5e6eb; background: #fff; }
        .input-row textarea {
          flex: 1; resize: none; border: 1px solid #d9d9d9; border-radius: 8px; padding: 8px 10px;
          font-size: 13px; height: 40px; outline: none; font-family: inherit;
        }
        .input-row textarea:focus { border-color: var(--moai-primary); }
        .input-row button {
          border: none; border-radius: 8px; padding: 0 16px; cursor: pointer;
          background: var(--moai-primary); color: #fff; font-size: 13px;
        }
        .input-row button:disabled { opacity: 0.5; cursor: not-allowed; }
        .hint { padding: 24px; text-align: center; color: #86909c; font-size: 13px; }
      </style>
      <button class="launcher" aria-label="chat"></button>
      <div class="panel">
        <div class="header">
          <div class="title"></div>
          <div class="subtitle"></div>
        </div>
        <div class="messages"></div>
        <div class="input-row">
          <textarea></textarea>
          <button type="button">➤</button>
        </div>
      </div>
    `

    const rootEl = this.shadow.host as HTMLElement
    rootEl.style.setProperty('--moai-primary', primary)
    rootEl.style.setProperty('--panel-w', `${cfg.panelWidth ?? 380}px`)
    rootEl.style.setProperty('--panel-h', `${cfg.panelHeight ?? 560}px`)

    const launcher = this.shadow.querySelector<HTMLButtonElement>('.launcher')!
    launcher.innerHTML = cfg.launcherText
      ? `<span style="font-size:13px">${escapeHtml(cfg.launcherText)}</span>`
      : `<svg viewBox="0 0 24 24"><path d="M12 3C6.9 3 3 6.5 3 10.8c0 2.4 1.2 4.5 3.2 5.9-.1.9-.5 2.2-1.5 3.3 1.9-.2 3.4-1 4.3-1.6.9.2 1.9.4 3 .4 5.1 0 9-3.5 9-7.9S17.1 3 12 3z"/></svg>`
    launcher.addEventListener('click', () => void this.toggle())

    const panel = this.shadow.querySelector<HTMLDivElement>('.panel')!
    panel.querySelector<HTMLDivElement>('.title')!.textContent = cfg.title || cfg.appName
    if (cfg.subtitle) panel.querySelector<HTMLDivElement>('.subtitle')!.textContent = cfg.subtitle

    this.messagesEl = panel.querySelector<HTMLDivElement>('.messages')!
    this.inputEl = panel.querySelector<HTMLTextAreaElement>('textarea')!
    this.inputEl.placeholder = cfg.placeholder || TEXT_FALLBACK.inputPlaceholder

    const sendBtn = panel.querySelector<HTMLButtonElement>('.input-row button')!
    sendBtn.addEventListener('click', () => void this.send())
    this.inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault()
        void this.send()
      }
    })

    document.body.appendChild(this.root)
  }

  private async toggle(): Promise<void> {
    const panel = this.shadow!.querySelector<HTMLDivElement>('.panel')!
    panel.classList.toggle('open')
    if (panel.classList.contains('open')) {
      await this.ensureSession()
      this.inputEl?.focus()
    }
  }

  private async open(): Promise<void> {
    const panel = this.shadow!.querySelector<HTMLDivElement>('.panel')!
    if (!panel.classList.contains('open')) {
      await this.toggle()
    }
  }

  private authHeaders(): Record<string, string> {
    return this.token ? { Authorization: `Bearer ${this.token}` } : {}
  }

  private async ensureSession(): Promise<boolean> {
    if (this.token && this.sessionId) return true
    const cfg = this.config!
    const server = this.opts.server
    const externalUserId = resolveExternalUserId(this.opts.appId, this.opts.externalUserId)

    if (cfg.isAuth && !this.opts.accessAppKey) {
      this.appendMessage('assistant', TEXT_FALLBACK.needKey)
      return false
    }

    try {
      const tokenRes = await fetch(`${server}/api/external/token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(
          cfg.isAuth
            ? { accessAppKey: this.opts.accessAppKey, appId: this.opts.appId, externalUserId, nickname: this.opts.nickname }
            : { appId: this.opts.appId, externalUserId, nickname: this.opts.nickname },
        ),
      })
      if (!tokenRes.ok) {
        console.warn(`${LOG_TAG} 换取外部 token 失败 status=${tokenRes.status} / token exchange failed`)
        this.appendMessage('assistant', TEXT_FALLBACK.initFailed)
        return false
      }
      this.token = ((await tokenRes.json()) as TokenResponse).accessToken

      const sessionRes = await fetch(`${server}/api/external/agent/${this.opts.appId}/session`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...this.authHeaders() },
        body: JSON.stringify({}),
      })
      if (!sessionRes.ok) {
        console.warn(`${LOG_TAG} 创建会话失败 status=${sessionRes.status} / create session failed`)
        this.appendMessage('assistant', TEXT_FALLBACK.initFailed)
        return false
      }
      const session = (await sessionRes.json()) as { value: string }
      this.sessionId = session.value
      return true
    } catch (error) {
      console.error(`${LOG_TAG} 初始化网络请求失败（服务不可达或被浏览器拦截，详见宿主页控制台的网络面板） / network request failed`, error)
      this.appendMessage('assistant', TEXT_FALLBACK.initFailed)
      return false
    }
  }

  private appendMessage(role: 'user' | 'assistant', text: string): HTMLDivElement {
    const el = document.createElement('div')
    el.className = `msg ${role}`
    el.textContent = text
    this.messagesEl!.appendChild(el)
    this.messagesEl!.scrollTop = this.messagesEl!.scrollHeight
    return el
  }

  private async send(): Promise<void> {
    if (this.running) return
    const text = this.inputEl!.value.trim()
    if (!text) return
    if (!(await this.ensureSession())) return

    this.running = true
    this.inputEl!.value = ''
    this.appendMessage('user', text)
    const bubble = this.appendMessage('assistant', '')

    try {
      const { HttpAgent } = await import('@ag-ui/client')
      const agent = new HttpAgent({
        url: `${this.opts.server}/api/external/agent/${this.opts.appId}/chat`,
        threadId: this.sessionId!,
        headers: { ...this.authHeaders() },
      })
      const buffers = new Map<string, string>()
      agent.setMessages([{ id: randomId(), role: 'user', content: text }])
      await agent.runAgent(
        {},
        {
          onTextMessageStartEvent: ({ event }) => {
            buffers.set(event.messageId, '')
          },
          onTextMessageContentEvent: ({ event }) => {
            const buf = (buffers.get(event.messageId) ?? '') + event.delta
            buffers.set(event.messageId, buf)
            bubble.textContent = [...buffers.values()].join('')
            this.messagesEl!.scrollTop = this.messagesEl!.scrollHeight
          },
          onRunErrorEvent: () => {
            if (!bubble.textContent) bubble.textContent = TEXT_FALLBACK.sendFailed
          },
        },
      )
      if (!bubble.textContent) bubble.textContent = TEXT_FALLBACK.sendFailed
    } catch {
      if (!bubble.textContent) bubble.textContent = TEXT_FALLBACK.sendFailed
    } finally {
      this.running = false
    }
  }
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}
