/**
 * 悬浮对话组件的公开配置（/api/teamres/app/{appId}/access-point）.
 */
export interface AccessPointConfig {
  appName: string
  avatarUrl?: string | null
  title: string
  subtitle?: string | null
  placeholder?: string | null
  primaryColor?: string | null
  position?: string | null
  displayMode?: string | null
  launcherText?: string | null
  panelWidth?: number | null
  panelHeight?: number | null
  defaultOpen?: boolean | null
  openingStatement?: string | null
  /** open 匿名开放（外部应用）/ token 需访问用户 token（内部应用访问点，须接入 tokenProvider） */
  authMode?: string | null
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

/** tokenProvider 可返回 accessToken 字符串，或附带 refresh_token 的对象 */
export interface WidgetToken {
  accessToken: string
  refreshToken?: string
}

/**
 * 请求中间件的改写上下文：组件发出的所有 API 请求（换 token / 建会话 / 对话）
 * 在发出前先经 onRequest 改写，url / headers / body 均可替换（如代理到业务系统自己的后端）.
 */
export interface WidgetRequestContext {
  url: string
  method: string
  headers: Record<string, string>
  body?: unknown
}

export type WidgetTokenProvider = () => Promise<string | WidgetToken | null | undefined>
export type WidgetRequestInterceptor = (ctx: WidgetRequestContext) => WidgetRequestContext | Promise<WidgetRequestContext>

/** 显示形式：floating 悬浮组件（默认）/ inline 内嵌小窗（渲染进宿主页占位容器） */
export type WidgetDisplayMode = 'floating' | 'inline'

export interface WidgetOptions {
  appId: string
  server: string
  externalUserId?: string
  nickname?: string
  /** 显示形式覆盖：默认取访问点服务端配置 displayMode（floating/inline） */
  mode?: WidgetDisplayMode
  /** inline 模式的挂载目标：CSS 选择器或元素本身；缺省回落 body 并 console.warn */
  target?: string | HTMLElement
  /**
   * token 拦截器（内部应用访问点必须接入）：向业务系统自己的后端要访问用户 token（后端持
   * 系统级接入凭据调 POST /api/subsys/system/app-tokens 颁发），组件不再自己换 token。
   * 返回 accessToken 字符串或 { accessToken, refreshToken }；每次调用应返回当前可用凭证.
   */
  tokenProvider?: WidgetTokenProvider
  /** 请求中间件：所有 API 请求发出前经此改写，可加头或整条代理到业务后端 */
  onRequest?: WidgetRequestInterceptor
}

/** 历史消息项（/api/teamres/session/{id}/messages，AppMessageItem 契约的裁剪视图） */
interface HistoryMessage {
  role: string
  content: string
  reasoning?: string | null
}

const TEXT_FALLBACK = {
  inputPlaceholder: '输入消息… / Type a message…',
  needToken: '此应用需持访问用户 token 访问：请由业务后端颁发 token 并经 tokenProvider 注入 / This app requires an access user token issued by your backend via tokenProvider',
  tokenFailed: '获取访问凭证失败 / Failed to obtain access token',
  sendFailed: '发送失败 / Failed to send',
  initFailed: '初始化失败，请稍后重试 / Initialization failed',
  thinking: '正在思考… / Thinking…',
  thoughtFor: (seconds: string) => `已思考 ${seconds}s / Thought for ${seconds}s`,
  reasoningLabel: '思考过程 / Reasoning',
  newChat: '新对话 / New chat',
  stop: '停止生成 / Stop',
  copy: '复制 / Copy',
  copied: '已复制 / Copied',
  retry: '重试 / Retry',
  stopped: '已停止 / Stopped',
  backToBottom: '回到最新 / Back to latest',
}

const LOG_TAG = '[moai-widget]'

/** access token 剩余寿命不足该值时提前刷新（毫秒） */
const TOKEN_REFRESH_AHEAD_MS = 60_000

/** 受限嵌入环境（沙箱 iframe 等）访问 localStorage 会抛 SecurityError，降级为内存态（仅当次页面存活） */
const memoryStore = new Map<string, string>()
function storageGet(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return memoryStore.get(key) ?? null
  }
}
function storageSet(key: string, value: string | null): void {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch {
    if (value === null) memoryStore.delete(key)
    else memoryStore.set(key, value)
  }
}

/** 会话 id 持久化：刷新后恢复最近一次会话（含历史消息）；值必须为 Guid，防路径注入 */
const SESSION_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
function storedSessionId(appId: string): string | null {
  const stored = storageGet(`moai-widget-session-${appId}`)
  return stored && SESSION_ID_PATTERN.test(stored) ? stored : null
}
function storeSessionId(appId: string, sessionId: string | null): void {
  storageSet(`moai-widget-session-${appId}`, sessionId)
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

/** 粗解析 JWT exp（不验签，仅用于临近过期提前刷新）；非 JWT 或无 exp 返回 undefined */
function tokenExpiresAt(token: string): number | undefined {
  const parts = token.split('.')
  if (parts.length !== 3) return undefined
  try {
    const payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')))
    return typeof payload.exp === 'number' ? payload.exp * 1000 : undefined
  } catch {
    return undefined
  }
}

export class MoaiWidget {
  private config?: AccessPointConfig
  private token?: string
  private refreshCredential?: string
  private sessionId?: string
  private root?: HTMLDivElement
  private shadow?: ShadowRoot
  private messagesEl?: HTMLDivElement
  private inputEl?: HTMLTextAreaElement
  private sendBtn?: HTMLButtonElement
  private panelEl?: HTMLDivElement
  private scrollChipEl?: HTMLButtonElement
  private running = false
  private refreshing?: Promise<string | undefined>
  private expanded = false
  private stickToBottom = true
  private lastUserText?: string
  /** 当前流式运行的 agent（仅用 abortRun 停止生成） */
  private activeAgent?: { abortRun: () => void }
  private sessionReady = false

  constructor(private readonly opts: WidgetOptions) {}

  /** 拉取公开配置；enabled=false 时不渲染任何内容（console.warn 说明原因，便于宿主页排查） */
  async init(): Promise<void> {
    try {
      const res = await this.runRequest({
        url: `${this.opts.server}/api/teamres/app/${this.opts.appId}/access-point`,
        method: 'GET',
        headers: {},
      })
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
      const inline = this.displayMode() === 'inline'
      if (this.config.defaultOpen || inline) {
        await this.open()
      }
    } catch (error) {
      console.error(`${LOG_TAG} 初始化失败，组件未渲染 / initialization failed, widget not rendered`, error)
    }
  }

  /** 生效显示形式：显式覆盖优先，否则取服务端配置，默认悬浮 */
  private displayMode(): WidgetDisplayMode {
    return this.opts.mode ?? (this.config?.displayMode === 'inline' ? 'inline' : 'floating')
  }

  /** 组件发出的所有 API 请求统一走此管线：先经 onRequest 中间件改写，再发出 */
  private async runRequest(ctx: WidgetRequestContext): Promise<Response> {
    const next = this.opts.onRequest ? await this.opts.onRequest({ ...ctx, headers: { ...ctx.headers } }) : ctx
    return fetch(next.url, {
      method: next.method,
      headers: next.headers,
      body: next.body === undefined ? undefined : JSON.stringify(next.body),
    })
  }

  /** 经 tokenProvider 获取凭证；返回 null 表示提供方未产出可用 token */
  private async acquireViaProvider(): Promise<WidgetToken | null> {
    if (!this.opts.tokenProvider) return null
    try {
      const result = await this.opts.tokenProvider()
      if (!result) return null
      const normalized = typeof result === 'string' ? { accessToken: result } : result
      return normalized.accessToken ? { accessToken: normalized.accessToken, refreshToken: normalized.refreshToken } : null
    } catch (error) {
      console.error(`${LOG_TAG} tokenProvider 执行失败 / tokenProvider failed`, error)
      return null
    }
  }

  /** 取当前可用 access token：临近过期时先刷新（tokenProvider 重新获取，否则用 refresh_token 换新） */
  private async getToken(): Promise<string | undefined> {
    if (!this.token) return undefined
    const expiresAt = tokenExpiresAt(this.token)
    if (expiresAt === undefined || expiresAt - Date.now() > TOKEN_REFRESH_AHEAD_MS) return this.token
    this.refreshing ??= this.refreshAccessToken().finally(() => {
      this.refreshing = undefined
    })
    return this.refreshing
  }

  /** 刷新失败时保留旧 token 返回，让请求自行失败并可重试 */
  private async refreshAccessToken(): Promise<string | undefined> {
    if (this.opts.tokenProvider) {
      const refreshed = await this.acquireViaProvider()
      if (refreshed) {
        this.token = refreshed.accessToken
        this.refreshCredential = refreshed.refreshToken
        return this.token
      }
      return this.token
    }

    if (!this.refreshCredential) return this.token
    try {
      const res = await this.runRequest({
        url: `${this.opts.server}/api/teamres/token/refresh`,
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: { refreshToken: this.refreshCredential },
      })
      if (!res.ok) {
        console.warn(`${LOG_TAG} 刷新外部 token 失败 status=${res.status} / token refresh failed`)
        return this.token
      }
      const data = (await res.json()) as TokenResponse
      this.token = data.accessToken
      this.refreshCredential = data.refreshToken
      return this.token
    } catch (error) {
      console.warn(`${LOG_TAG} 刷新外部 token 请求失败 / token refresh request failed`, error)
      return this.token
    }
  }

  private render(): void {
    const cfg = this.config!
    const mode = this.displayMode()
    this.root = document.createElement('div')
    this.root.className = 'moai-widget-root'
    this.shadow = this.root.attachShadow({ mode: 'open' })
    const primary = cfg.primaryColor || '#1677ff'
    const position = cfg.position ?? 'bottomRight'
    // 兼容存量 kebab（bottom-left）与新 camel（bottomLeft）两种拼写
    const side = position === 'bottomLeft' || position === 'bottom-left' ? 'left' : 'right'

    this.shadow.innerHTML = `
      <style>
        :host { all: initial;${mode === 'inline' ? ' display: block;' : ''} }
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
        .panel.expanded { top: 16px; bottom: 16px; left: 16px; right: 16px; width: auto; height: auto; max-height: none; }
        /* inline 内嵌小窗：面板进入宿主页文档流（占位容器决定位置），无悬浮按钮，直接可见 */
        .inline-host .panel {
          position: relative; bottom: auto; top: auto; ${side}: auto;
          width: 100%; height: var(--panel-h); max-height: none;
          display: flex;
          box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12); border: 1px solid #e5e6eb;
        }
        .inline-host .panel.expanded {
          position: fixed; top: 16px; bottom: 16px; left: 16px; right: 16px;
          width: auto; height: auto; z-index: 2147483000;
        }
        .inline-host .launcher { display: none; }
        .header { padding: 12px 14px; background: var(--moai-primary); color: #fff; display: flex; align-items: center; gap: 8px; }
        .header .titles { flex: 1; min-width: 0; }
        .header .title { font-size: 15px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .header .subtitle { font-size: 12px; opacity: 0.85; margin-top: 2px; white-space: pre-wrap; }
        .header .avatar { width: 34px; height: 34px; border-radius: 50%; object-fit: cover; flex: none; display: none; }
        .icon-btn {
          flex: none; width: 28px; height: 28px; border: none; border-radius: 6px; cursor: pointer;
          background: rgba(255, 255, 255, 0.18); color: #fff; display: flex; align-items: center; justify-content: center;
        }
        .icon-btn:hover { background: rgba(255, 255, 255, 0.32); }
        .icon-btn svg { width: 16px; height: 16px; fill: #fff; }
        .messages-wrap { position: relative; flex: 1; min-height: 0; display: flex; }
        .messages { flex: 1; overflow-y: auto; padding: 12px; background: #f5f6f8; }
        .msg { max-width: 86%; padding: 8px 12px; border-radius: 10px; margin-bottom: 8px;
               font-size: 13px; line-height: 1.6; word-break: break-word; }
        .msg.user { background: var(--moai-primary); color: #fff; margin-left: auto; white-space: pre-wrap; }
        .msg.assistant { background: #fff; color: #1f2329; border: 1px solid #e5e6eb; }
        .msg.assistant .msg-body > :first-child { margin-top: 0; }
        .msg.assistant .msg-body > :last-child { margin-bottom: 0; }
        .msg-body p { margin: 0 0 6px; }
        .msg-body h1, .msg-body h2, .msg-body h3, .msg-body h4 { margin: 8px 0 4px; font-size: 14px; font-weight: 600; }
        .msg-body h1 { font-size: 16px; }
        .msg-body ul, .msg-body ol { margin: 4px 0; padding-left: 18px; }
        .msg-body blockquote { margin: 4px 0; padding: 2px 8px; border-left: 3px solid var(--moai-primary); color: #4e5969; background: rgba(128, 128, 128, 0.06); }
        .msg-body pre { margin: 6px 0; padding: 8px 10px; background: #1f2329; color: #e5e6eb; border-radius: 6px; overflow-x: auto; font-size: 12px; }
        .msg-body code { padding: 1px 5px; background: rgba(128, 128, 128, 0.14); border-radius: 4px; font-size: 12px; font-family: Consolas, Menlo, monospace; }
        .msg-body pre code { padding: 0; background: none; }
        .msg-body a { color: var(--moai-primary); }
        .msg-body hr { border: none; border-top: 1px solid #e5e6eb; margin: 8px 0; }
        /* 思考过程：可折叠块，流式期间实时追加 */
        .reasoning { margin-bottom: 6px; border: 1px dashed #e5e6eb; border-radius: 8px; overflow: hidden; background: rgba(128, 128, 128, 0.04); }
        .reasoning-header {
          display: flex; align-items: center; gap: 4px; padding: 4px 8px; cursor: pointer; user-select: none;
          font-size: 12px; color: #4e5969;
        }
        .reasoning-header svg { width: 12px; height: 12px; fill: #86909c; transition: transform 0.15s; }
        .reasoning.collapsed .reasoning-header svg { transform: rotate(-90deg); }
        .reasoning-body { display: block; padding: 4px 10px 8px; font-size: 12px; color: #86909c; white-space: pre-wrap; max-height: 200px; overflow-y: auto; }
        .reasoning.collapsed .reasoning-body { display: none; }
        .reasoning-header .label.moai-thinking::after { content: '…'; animation: moai-ellipsis 1.2s infinite; }
        @keyframes moai-ellipsis { 0% { content: ''; } 50% { content: '.'; } 100% { content: '..'; } }
        /* 打字指示：正文未到前的三点气泡 */
        .msg.typing { display: flex; gap: 4px; align-items: center; padding: 12px 14px; }
        .msg.typing i { width: 6px; height: 6px; border-radius: 50%; background: #86909c; animation: moai-dot 1.2s infinite; }
        .msg.typing i:nth-child(2) { animation-delay: 0.2s; }
        .msg.typing i:nth-child(3) { animation-delay: 0.4s; }
        @keyframes moai-dot { 0%, 60%, 100% { opacity: 0.25; } 30% { opacity: 1; } }
        /* 流式光标 */
        .cursor { display: inline-block; width: 2px; height: 14px; margin-left: 2px; vertical-align: -2px; background: var(--moai-primary); animation: moai-blink 0.8s step-end infinite; }
        @keyframes moai-blink { 50% { opacity: 0; } }
        /* 消息操作条（完成后出现）：复制 / 重试 */
        .msg-actions { display: none; gap: 10px; margin-top: 6px; }
        .msg-actions button { border: none; background: none; padding: 0; font-size: 12px; color: #86909c; cursor: pointer; }
        .msg-actions button:hover { color: var(--moai-primary); }
        .msg.done .msg-actions { display: flex; }
        .input-row { display: flex; gap: 8px; padding: 10px; border-top: 1px solid #e5e6eb; background: #fff; }
        .input-row textarea {
          flex: 1; resize: none; border: 1px solid #d9d9d9; border-radius: 8px; padding: 8px 10px;
          font-size: 13px; height: 40px; outline: none; font-family: inherit;
        }
        .input-row textarea:focus { border-color: var(--moai-primary); }
        .input-row button {
          border: none; border-radius: 8px; padding: 0 16px; cursor: pointer; min-width: 64px;
          background: var(--moai-primary); color: #fff; font-size: 13px;
        }
        .input-row button:disabled { opacity: 0.5; cursor: not-allowed; }
        .input-row button.stopping { background: #f53f3f; }
        .hint { padding: 24px; text-align: center; color: #86909c; font-size: 13px; }
        /* 回到最新浮标 */
        .scroll-chip {
          display: none; position: absolute; bottom: 10px; left: 50%; transform: translateX(-50%);
          border: none; border-radius: 12px; padding: 4px 12px; cursor: pointer;
          background: #fff; color: var(--moai-primary); font-size: 12px;
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15); z-index: 10;
        }
        .scroll-chip.show { display: block; }
      </style>
      ${mode === 'floating' ? '<button class="launcher" aria-label="chat"></button>' : ''}
      <div class="panel">
        <div class="header">
          <img class="avatar" alt="" />
          <div class="titles">
            <div class="title"></div>
            <div class="subtitle"></div>
          </div>
          <button class="icon-btn new-chat" title="${escapeHtml(TEXT_FALLBACK.newChat)}">
            <svg viewBox="0 0 24 24"><path d="M11 5h2v6h6v2h-6v6h-2v-6H5v-2h6z"/></svg>
          </button>
          <button class="icon-btn expand" title="⤢">
            <svg viewBox="0 0 24 24"><path d="M4 4h7v2H7.4l4.3 4.3-1.4 1.4L6 7.4V11H4V4zm16 16h-7v-2h3.6l-4.3-4.3 1.4-1.4 4.3 4.3V13h2v7z"/></svg>
          </button>
        </div>
        <div class="messages-wrap">
          <div class="messages"></div>
          <button class="scroll-chip">↓ ${escapeHtml(TEXT_FALLBACK.backToBottom)}</button>
        </div>
        <div class="input-row">
          <textarea></textarea>
          <button type="button">➤</button>
        </div>
      </div>
    `

    if (mode === 'inline') this.root.classList.add('inline-host')

    const rootEl = this.shadow.host as HTMLElement
    rootEl.style.setProperty('--moai-primary', primary)
    rootEl.style.setProperty('--panel-w', `${cfg.panelWidth ?? 380}px`)
    rootEl.style.setProperty('--panel-h', `${cfg.panelHeight ?? 560}px`)

    if (mode === 'floating') {
      const launcher = this.shadow.querySelector<HTMLButtonElement>('.launcher')!
      launcher.innerHTML = cfg.launcherText
        ? `<span style="font-size:13px">${escapeHtml(cfg.launcherText)}</span>`
        : `<svg viewBox="0 0 24 24"><path d="M12 3C6.9 3 3 6.5 3 10.8c0 2.4 1.2 4.5 3.2 5.9-.1.9-.5 2.2-1.5 3.3 1.9-.2 3.4-1 4.3-1.6.9.2 1.9.4 3 .4 5.1 0 9-3.5 9-7.9S17.1 3 12 3z"/></svg>`
      launcher.addEventListener('click', () => void this.toggle())
    }

    this.panelEl = this.shadow.querySelector<HTMLDivElement>('.panel')!
    this.panelEl.querySelector<HTMLDivElement>('.title')!.textContent = cfg.title || cfg.appName
    if (cfg.subtitle) this.panelEl.querySelector<HTMLDivElement>('.subtitle')!.textContent = cfg.subtitle
    const avatarImg = this.panelEl.querySelector<HTMLImageElement>('.avatar')!
    if (cfg.avatarUrl) {
      avatarImg.src = cfg.avatarUrl
      avatarImg.style.display = 'block'
    }

    this.messagesEl = this.panelEl.querySelector<HTMLDivElement>('.messages')!
    this.inputEl = this.panelEl.querySelector<HTMLTextAreaElement>('textarea')!
    this.inputEl.placeholder = cfg.placeholder || TEXT_FALLBACK.inputPlaceholder
    this.sendBtn = this.panelEl.querySelector<HTMLButtonElement>('.input-row button')!
    this.scrollChipEl = this.panelEl.querySelector<HTMLButtonElement>('.scroll-chip')!

    this.sendBtn.addEventListener('click', () => {
      if (this.running) this.stopGenerating()
      else void this.send()
    })
    this.inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault()
        if (!this.running) void this.send()
      }
    })
    this.panelEl.querySelector<HTMLButtonElement>('.expand')!.addEventListener('click', () => this.toggleExpanded())
    this.panelEl.querySelector<HTMLButtonElement>('.new-chat')!.addEventListener('click', () => this.newConversation())
    this.messagesEl.addEventListener('scroll', () => {
      const el = this.messagesEl!
      this.stickToBottom = el.scrollTop + el.clientHeight >= el.scrollHeight - 30
      this.scrollChipEl!.classList.toggle('show', !this.stickToBottom)
    })
    this.scrollChipEl.addEventListener('click', () => {
      this.stickToBottom = true
      this.scrollChipEl!.classList.remove('show')
      this.scrollToBottom()
    })
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && this.expanded) this.toggleExpanded(false)
    })

    // 挂载：inline 进宿主页占位容器，floating 进 body
    let mountParent: HTMLElement = document.body
    if (mode === 'inline') {
      const target = typeof this.opts.target === 'string'
        ? document.querySelector<HTMLElement>(this.opts.target)
        : this.opts.target
      if (target) {
        mountParent = target
      } else {
        console.warn(`${LOG_TAG} inline 模式未找到挂载目标（target=${String(this.opts.target)}），回落 body / inline target not found, fallback to body`)
      }
    }
    mountParent.appendChild(this.root)
  }

  /** 放大/还原面板（两种显示形式都支持；放大后为近全屏浮层） */
  private toggleExpanded(expand?: boolean): void {
    if (!this.panelEl) return
    this.expanded = expand ?? !this.expanded
    this.panelEl.classList.toggle('expanded', this.expanded)
    this.scrollToBottom()
  }

  /** 新对话：丢弃持久化会话与消息区，重开场白；下次发送时再懒建会话 */
  private newConversation(): void {
    this.sessionId = undefined
    this.sessionReady = false
    storeSessionId(this.opts.appId, null)
    this.messagesEl!.innerHTML = ''
    this.showOpeningStatement()
    this.stickToBottom = true
    this.scrollToBottom()
  }

  private showOpeningStatement(): void {
    const opening = this.config?.openingStatement
    if (opening) this.appendAssistantDone(opening)
  }

  private scrollToBottom(): void {
    if (!this.stickToBottom) return
    const el = this.messagesEl
    if (el) el.scrollTop = el.scrollHeight
  }

  private async toggle(): Promise<void> {
    const panel = this.shadow!.querySelector<HTMLDivElement>('.panel')!
    panel.classList.toggle('open')
    if (panel.classList.contains('open')) {
      await this.ensureConversation()
      this.inputEl?.focus()
    }
  }

  private async open(): Promise<void> {
    const panel = this.shadow!.querySelector<HTMLDivElement>('.panel')!
    if (!panel.classList.contains('open')) {
      await this.toggle()
    }
  }

  /** 渲染一次性提示（错误等），沿用 .msg 容器保证既有断言兼容 */
  private appendMessage(role: 'user' | 'assistant', text: string): HTMLDivElement {
    const el = document.createElement('div')
    el.className = `msg ${role}`
    el.textContent = text
    this.messagesEl!.appendChild(el)
    this.scrollToBottom()
    return el
  }

  /** 历史恢复/开场白用：直接落一条完成的 assistant 消息（markdown 渲染 + 可展开思考块） */
  private appendAssistantDone(text: string, reasoning?: string | null): void {
    const wrap = this.createAssistantMessage()
    if (reasoning) wrap.restoreReasoning(reasoning)
    wrap.setContent(text)
    wrap.done(false)
    this.scrollToBottom()
  }

  /** 构造一条流式 assistant 消息：思考块 + 正文 + 操作条（复制/重试），流式各回调写入 */
  private createAssistantMessage() {
    const root = document.createElement('div')
    root.className = 'msg assistant'

    const reasoning = document.createElement('div')
    reasoning.className = 'reasoning collapsed'
    reasoning.style.display = 'none'
    const reasoningHeader = document.createElement('div')
    reasoningHeader.className = 'reasoning-header'
    // 初始不填充文案（display:none 的隐藏文本也会进入 textContent），首次展示时再写入
    reasoningHeader.innerHTML = '<svg viewBox="0 0 24 24"><path d="M7 10l5 5 5-5z"/></svg><span class="label"></span>'
    const reasoningBody = document.createElement('div')
    reasoningBody.className = 'reasoning-body'
    reasoning.append(reasoningHeader, reasoningBody)
    reasoningHeader.addEventListener('click', () => reasoning.classList.toggle('collapsed'))

    const body = document.createElement('div')
    body.className = 'msg-body'
    const actions = document.createElement('div')
    actions.className = 'msg-actions'
    root.append(reasoning, body, actions)
    this.messagesEl!.appendChild(root)

    let reasoningText = ''
    let reasoningStart = 0
    let text = ''
    let cursor: HTMLSpanElement | null = null

    const ensureCursor = () => {
      if (!cursor) {
        cursor = document.createElement('span')
        cursor.className = 'cursor'
        body.appendChild(cursor)
      }
    }
    const removeCursor = () => {
      cursor?.remove()
      cursor = null
    }
    const setThinkingLabel = (thinking: boolean) => {
      const label = reasoningHeader.querySelector('.label')!
      if (thinking) {
        label.classList.add('moai-thinking')
        label.textContent = TEXT_FALLBACK.thinking
      } else {
        label.classList.remove('moai-thinking')
        label.textContent = TEXT_FALLBACK.thoughtFor(((Date.now() - reasoningStart) / 1000).toFixed(1))
      }
    }

    // 失败重试：移除失败气泡并重发上一条用户消息（箭头函数捕获组件实例，避免 this 别名）
    const retryLastSend = () => {
      root.remove()
      const previous = this.lastUserText
      if (previous) void this.send(previous)
    }
    return {
      root,
      setReasoning(delta: string) {
        if (reasoning.style.display === 'none') {
          reasoning.style.display = 'block'
          reasoning.classList.remove('collapsed')
          reasoningStart = Date.now()
          setThinkingLabel(true)
        }
        reasoningText += delta
        reasoningBody.textContent = reasoningText
      },
      /** 历史消息恢复：整段落思考内容 + 默认折叠 + 中性标签 */
      restoreReasoning(text: string) {
        if (!text) return
        reasoning.style.display = 'block'
        reasoningText = text
        reasoningBody.textContent = text
        reasoning.classList.add('collapsed')
        const label = reasoningHeader.querySelector('.label')!
        label.classList.remove('moai-thinking')
        label.textContent = TEXT_FALLBACK.reasoningLabel
      },
      finishReasoning() {
        if (reasoning.style.display === 'none') return
        reasoning.classList.add('collapsed')
        setThinkingLabel(false)
      },
      setContent(textValue: string) {
        text = textValue
        removeCursor()
        body.innerHTML = renderMarkdown(text)
      },
      /** 流式期间：纯文本展示 + 闪烁光标（完成后经 setContent 转 markdown） */
      setStreamText(value: string) {
        text = value
        body.textContent = text
        ensureCursor()
      },
      done(withCopy = true) {
        removeCursor()
        body.innerHTML = renderMarkdown(text)
        root.classList.add('done')
        if (!withCopy) return
        const copyBtn = document.createElement('button')
        copyBtn.type = 'button'
        copyBtn.textContent = TEXT_FALLBACK.copy
        copyBtn.addEventListener('click', () => {
          void navigator.clipboard.writeText(text).then(() => {
            copyBtn.textContent = TEXT_FALLBACK.copied
            setTimeout(() => { copyBtn.textContent = TEXT_FALLBACK.copy }, 1500)
          })
        })
        actions.appendChild(copyBtn)
      },
      fail(retry: boolean) {
        removeCursor()
        if (root.classList.contains('done')) return
        if (!text) body.textContent = TEXT_FALLBACK.sendFailed
        root.classList.add('done')
        if (!retry) return
        const retryBtn = document.createElement('button')
        retryBtn.type = 'button'
        retryBtn.textContent = TEXT_FALLBACK.retry
        retryBtn.addEventListener('click', retryLastSend)
        actions.appendChild(retryBtn)
      },
    }
  }

  /**
   * 打开会话上下文：先确保外部 token（需授权应用的接入错误尽早暴露在面板里），
   * 有持久化会话则拉历史消息渲染，否则仅展示开场白；会话本身懒创建（首次发送才建）.
   */
  private async ensureConversation(): Promise<void> {
    if (this.sessionReady) return
    this.sessionReady = true
    this.showOpeningStatement()

    if (!(await this.ensureToken())) return
    const stored = storedSessionId(this.opts.appId)
    if (!stored) return
    try {
      const res = await this.runRequest({
        url: `${this.opts.server}/api/teamres/session/${stored}/messages`,
        method: 'GET',
        headers: await this.authHeaders(),
      })
      if (!res.ok) {
        // 会话已不存在（外部身份被清理等）：丢弃持久化，保持当前开场白态
        storeSessionId(this.opts.appId, null)
        return
      }
      const data = (await res.json()) as { items?: HistoryMessage[] }
      const history = (data.items ?? []).filter(m => (m.role === 'user' || m.role === 'assistant') && (m.content || m.reasoning))
      if (history.length === 0) return
      this.messagesEl!.innerHTML = ''
      this.sessionId = stored
      for (const m of history) {
        if (m.role === 'user') this.appendMessage('user', m.content)
        else this.appendAssistantDone(m.content, m.reasoning)
      }
      this.stickToBottom = true
      this.scrollToBottom()
    } catch (error) {
      console.warn(`${LOG_TAG} 恢复历史会话失败 / restore session history failed`, error)
    }
  }

  private async ensureSession(): Promise<boolean> {
    if (this.sessionId) return true
    if (!(await this.ensureToken())) return false

    try {
      const res = await this.runRequest({
        url: `${this.opts.server}/api/teamres/agent/${this.opts.appId}/session`,
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(await this.authHeaders()) },
        body: {},
      })
      if (!res.ok) {
        console.warn(`${LOG_TAG} 创建会话失败 status=${res.status} / create session failed`)
        this.appendMessage('assistant', TEXT_FALLBACK.initFailed)
        return false
      }
      const session = (await res.json()) as { value: string }
      this.sessionId = session.value
      storeSessionId(this.opts.appId, this.sessionId)
      return true
    } catch (error) {
      console.error(`${LOG_TAG} 初始化网络请求失败（服务不可达或被浏览器拦截，详见宿主页控制台的网络面板） / network request failed`, error)
      this.appendMessage('assistant', TEXT_FALLBACK.initFailed)
      return false
    }
  }

  /**
   * 确保持有访问凭证：tokenProvider 优先（业务后端签发，key 不落前端；内部应用访问点必须接入）；
   * 否则组件匿名换 token（is_auth 已退役，外部应用一律匿名开放）。
   * 内部应用（authMode=token）无 tokenProvider 时直接提示，不发起匿名请求.
   */
  private async ensureToken(): Promise<boolean> {
    if (this.token) return true
    const cfg = this.config!

    if (this.opts.tokenProvider) {
      const token = await this.acquireViaProvider()
      if (!token) {
        this.appendMessage('assistant', TEXT_FALLBACK.tokenFailed)
        return false
      }
      this.token = token.accessToken
      this.refreshCredential = token.refreshToken
      return true
    }

    if (cfg.authMode === 'token') {
      this.appendMessage('assistant', TEXT_FALLBACK.needToken)
      return false
    }

    const externalUserId = resolveExternalUserId(this.opts.appId, this.opts.externalUserId)
    try {
      const res = await this.runRequest({
        url: `${this.opts.server}/api/teamres/token`,
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: { appId: this.opts.appId, externalUserId, nickname: this.opts.nickname },
      })
      if (!res.ok) {
        console.warn(`${LOG_TAG} 换取外部 token 失败 status=${res.status} / token exchange failed`)
        this.appendMessage('assistant', TEXT_FALLBACK.initFailed)
        return false
      }
      const data = (await res.json()) as TokenResponse
      this.token = data.accessToken
      this.refreshCredential = data.refreshToken
      return true
    } catch (error) {
      console.error(`${LOG_TAG} 换取外部 token 网络请求失败 / token exchange request failed`, error)
      this.appendMessage('assistant', TEXT_FALLBACK.initFailed)
      return false
    }
  }

  private async authHeaders(): Promise<Record<string, string>> {
    const token = await this.getToken()
    return token ? { Authorization: `Bearer ${token}` } : {}
  }

  private setRunning(running: boolean): void {
    this.running = running
    if (!this.sendBtn) return
    this.sendBtn.textContent = running ? '■' : '➤'
    this.sendBtn.classList.toggle('stopping', running)
    this.sendBtn.title = running ? TEXT_FALLBACK.stop : ''
  }

  /** 停止生成：中止当前 SSE 流，保留已生成的部分内容 */
  private stopGenerating(): void {
    this.activeAgent?.abortRun()
  }

  private async send(presetText?: string): Promise<void> {
    if (this.running) return
    const text = (presetText ?? this.inputEl!.value).trim()
    if (!text) return
    if (!(await this.ensureSession())) return

    this.lastUserText = text
    this.setRunning(true)
    this.inputEl!.value = ''
    this.appendMessage('user', text)

    // 打字指示：首个 reasoning / 正文事件到达前展示
    const typing = document.createElement('div')
    typing.className = 'msg assistant typing'
    typing.innerHTML = '<i></i><i></i><i></i>'
    this.messagesEl!.appendChild(typing)
    this.scrollToBottom()
    const bubble = this.createAssistantMessage()
    const finishTyping = () => typing.remove()

    try {
      // 对话请求同样先经 onRequest 中间件改写（可代理/加头），再交给 AG-UI 客户端
      const routed = this.opts.onRequest
        ? await this.opts.onRequest({
          url: `${this.opts.server}/api/teamres/agent/${this.opts.appId}/chat`,
          method: 'POST',
          headers: await this.authHeaders(),
        })
        : { url: `${this.opts.server}/api/teamres/agent/${this.opts.appId}/chat`, headers: await this.authHeaders() }

      const { HttpAgent } = await import('@ag-ui/client')
      const agent = new HttpAgent({
        url: routed.url,
        threadId: this.sessionId!,
        headers: routed.headers,
      })
      this.activeAgent = agent
      const buffers = new Map<string, string>()
      const reasoningBuffers = new Map<string, string>()
      let firstTextArrived = false
      agent.setMessages([{ id: randomId(), role: 'user', content: text }])
      await agent.runAgent(
        {},
        {
          onReasoningStartEvent: () => finishTyping(),
          onReasoningMessageContentEvent: ({ event }) => {
            finishTyping()
            reasoningBuffers.set(event.messageId, (reasoningBuffers.get(event.messageId) ?? '') + event.delta)
            bubble.setReasoning(event.delta)
            this.scrollToBottom()
          },
          onTextMessageStartEvent: ({ event }) => {
            finishTyping()
            buffers.set(event.messageId, '')
          },
          onTextMessageContentEvent: ({ event }) => {
            if (!firstTextArrived) {
              firstTextArrived = true
              bubble.finishReasoning()
            }
            buffers.set(event.messageId, (buffers.get(event.messageId) ?? '') + event.delta)
            // 多段 TEXT_MESSAGE（分段生成）以空行分隔拼接，流式期间纯文本 + 光标
            bubble.setStreamText([...buffers.values()].join('\n\n'))
            this.scrollToBottom()
          },
          onRunFinishedEvent: () => {
            finishTyping()
            bubble.finishReasoning()
            bubble.done()
          },
          onRunErrorEvent: () => {
            finishTyping()
            bubble.fail(true)
          },
        },
      )
      finishTyping()
      // abortRun 后 runAgent 返回（未走 onRunError/onRunFinished）：保留已生成内容
      if (!bubble.root.classList.contains('done')) {
        bubble.finishReasoning()
        bubble.done()
      }
    } catch {
      finishTyping()
      bubble.fail(true)
    } finally {
      this.activeAgent = undefined
      this.setRunning(false)
      this.scrollToBottom()
    }
  }
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}

/**
 * 轻量 markdown 渲染（零依赖，专为组件单文件产物设计，不引入 react-markdown）。
 * 输入先整体转义再生成白名单标签，无注入面：围栏代码块 / 行内代码 / 加粗 / 斜体 /
 * 标题 / 无序有序列表 / 引用 / 分割线 / 链接（仅 http(s)，新窗口 + noopener）.
 */
function renderMarkdown(text: string): string {
  const inline = (src: string): string => src
    .replace(/`([^`\n]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\*([^*\n]+)\*/g, '<em>$1</em>')
    .replace(/\[([^\]\n]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>')

  const lines = escapeHtml(text).split('\n')
  const out: string[] = []
  let inCode = false
  let listType: 'ul' | 'ol' | null = null
  let inQuote = false
  let paragraph: string[] = []

  const flushParagraph = () => {
    if (paragraph.length > 0) {
      out.push(`<p>${paragraph.map(inline).join('<br>')}</p>`)
      paragraph = []
    }
  }
  const flushList = () => {
    if (listType) {
      out.push(`</${listType}>`)
      listType = null
    }
  }
  const flushQuote = () => {
    if (inQuote) {
      out.push('</blockquote>')
      inQuote = false
    }
  }
  const flushAll = () => {
    flushParagraph()
    flushList()
    flushQuote()
  }

  for (const line of lines) {
    if (/^```/.test(line)) {
      if (inCode) {
        out.push('</code></pre>')
        inCode = false
      } else {
        flushAll()
        out.push('<pre><code>')
        inCode = true
      }
      continue
    }
    if (inCode) {
      out.push(line)
      continue
    }

    const heading = line.match(/^(#{1,4})\s+(.*)$/)
    if (heading) {
      flushAll()
      const level = heading[1].length
      out.push(`<h${level}>${inline(heading[2])}</h${level}>`)
      continue
    }
    if (/^(-{3,}|\*{3,})\s*$/.test(line)) {
      flushAll()
      out.push('<hr>')
      continue
    }
    const ul = line.match(/^[-*]\s+(.*)$/)
    if (ul) {
      flushParagraph()
      flushQuote()
      if (listType !== 'ul') {
        flushList()
        out.push('<ul>')
        listType = 'ul'
      }
      out.push(`<li>${inline(ul[1])}</li>`)
      continue
    }
    const ol = line.match(/^\d+[.)]\s+(.*)$/)
    if (ol) {
      flushParagraph()
      flushQuote()
      if (listType !== 'ol') {
        flushList()
        out.push('<ol>')
        listType = 'ol'
      }
      out.push(`<li>${inline(ol[1])}</li>`)
      continue
    }
    const quote = line.match(/^&gt;\s?(.*)$/)
    if (quote) {
      flushParagraph()
      flushList()
      if (!inQuote) {
        out.push('<blockquote>')
        inQuote = true
      }
      out.push(`<p>${inline(quote[1])}</p>`)
      continue
    }
    if (line.trim() === '') {
      flushAll()
      continue
    }
    flushList()
    flushQuote()
    paragraph.push(line)
  }
  if (inCode) out.push('</code></pre>')
  flushAll()
  return out.join('\n')
}
