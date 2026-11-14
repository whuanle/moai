import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MoaiWidget, type WidgetRequestContext } from '../widget'

const APP_ID = '0b6ead87-1234-4bcd-9a35-351d1a3b0d61'
const SERVER = 'http://moai.test'

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
}

/** 构造可被粗解析的 JWT（不验签，仅 exp 字段生效） */
function fakeJwt(expSeconds: number): string {
  const b64url = (value: object) => btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${b64url({ alg: 'none', typ: 'JWT' })}.${b64url({ exp: expSeconds })}.sig`
}

type FetchMock = ReturnType<typeof vi.fn>

function installFetch(handler: (url: string, init?: RequestInit) => Promise<Response>): FetchMock {
  const mock = vi.fn(handler as (url: string, init?: RequestInit) => Promise<Response>)
  vi.stubGlobal('fetch', mock)
  return mock
}

function accessPoint(extra: Record<string, unknown> = {}): Record<string, unknown> {
  return { appName: '客服助手', title: '客服', enabled: true, ...extra }
}

beforeEach(() => {
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('MoaiWidget token 拦截器与请求中间件', () => {
  it('配置 tokenProvider 时由业务后端签发 token，组件不再自己换 token', async () => {
    const provider = vi.fn(async () => 'tok-from-backend')
    const fetchMock = installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ authMode: 'token', defaultOpen: true }))
      if (url.includes('/session')) return jsonResponse({ value: 'sess-1' })
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER, tokenProvider: provider })
    await widget.init()

    // 打开面板即取 token（会话懒创建到首次发送）
    expect(provider).toHaveBeenCalledTimes(1)
    const tokenUrls = fetchMock.mock.calls.map((call) => call[0] as string).filter((url) => url.includes('/teamres/token'))
    expect(tokenUrls).toEqual([])

    // 发送时对话请求携带后端签发的 token（经 AG-UI 客户端构造参数）
    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    const textarea = shadow.querySelector<HTMLTextAreaElement>('textarea')!
    textarea.value = '你好'
    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
    await new Promise((r) => setTimeout(r, 0))
    const agent = await lastAgent()
    expect(agent.config.headers.Authorization).toBe('Bearer tok-from-backend')

    const widgetRoot = document.querySelector('.moai-widget-root')!
    expect(widgetRoot).toBeTruthy()
    expect(widgetRoot.shadowRoot!.querySelector('.messages')!.textContent).not.toContain('失败')
  })

  it('onRequest 中间件可改写组件发出的全部 API 请求（access-point / 换 token / 对话路由）', async () => {
    const onRequest = vi.fn((ctx: WidgetRequestContext) => ({
      ...ctx,
      headers: { ...ctx.headers, 'X-Biz-Trace': 'trace-1' },
    }))
    const fetchMock = installFetch(async (url, init) => {
      expect((init?.headers as Record<string, string>)['X-Biz-Trace']).toBe('trace-1')
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ defaultOpen: true }))
      if (url.includes('/teamres/token')) return jsonResponse({ accessToken: 'tok-1', refreshToken: 'r1', expiresIn: 3600, tokenType: 'user' })
      if (url.includes('/session')) return jsonResponse({ value: 'sess-2' })
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER, onRequest })
    await widget.init()

    // 发送一轮，对话路由同样经中间件
    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    const textarea = shadow.querySelector<HTMLTextAreaElement>('textarea')!
    textarea.value = '你好'
    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
    await new Promise((r) => setTimeout(r, 0))
    const agent = await lastAgent()
    expect(agent.config.headers['X-Biz-Trace']).toBe('trace-1')
    agent.resolve({})

    // access-point / 打开面板换 token / 首次发送建会话 / 对话路由，全部先经中间件
    expect(onRequest).toHaveBeenCalledTimes(4)
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })

  it('access token 临近过期时用 refresh_token 换新，新 token 生效期内不重复刷新', async () => {
    const newAccess = fakeJwt(Math.floor(Date.now() / 1000) + 3600)
    const fetchMock = installFetch(async (url) => {
      if (url.includes('/teamres/token/refresh')) return jsonResponse({ accessToken: newAccess, refreshToken: 'refresh-2', expiresIn: 3600, tokenType: 'user' })
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    const internals = widget as unknown as { token?: string; refreshCredential?: string; getToken(): Promise<string | undefined> }
    internals.token = fakeJwt(Math.floor(Date.now() / 1000) - 10)
    internals.refreshCredential = 'refresh-1'

    await expect(internals.getToken()).resolves.toBe(newAccess)
    const refreshCalls = fetchMock.mock.calls.filter((call) => (call[0] as string).includes('/teamres/token/refresh'))
    expect(refreshCalls).toHaveLength(1)

    await expect(internals.getToken()).resolves.toBe(newAccess)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('tokenProvider 模式下临近过期时重新调用提供方获取新 token', async () => {
    const stale = fakeJwt(Math.floor(Date.now() / 1000) - 10)
    const fresh = fakeJwt(Math.floor(Date.now() / 1000) + 3600)
    const provider = vi.fn().mockResolvedValue({ accessToken: fresh })

    installFetch(async (url) => {
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER, tokenProvider: provider })
    const internals = widget as unknown as { token?: string; getToken(): Promise<string | undefined> }
    internals.token = stale

    await expect(internals.getToken()).resolves.toBe(fresh)
    expect(provider).toHaveBeenCalledTimes(1)
  })

  it('内部应用访问点（authMode=token）无 tokenProvider 时直接提示接入方式，不发起匿名换 token', async () => {
    const fetchMock = installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ authMode: 'token', defaultOpen: true }))
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    await widget.init()

    const messages = document.querySelector('.moai-widget-root')!.shadowRoot!.querySelector('.messages')!
    expect(messages.textContent).toContain('tokenProvider')
    // 不应尝试匿名换 token
    const tokenUrls = fetchMock.mock.calls.map((call) => call[0] as string).filter((url) => url.includes('/teamres/token'))
    expect(tokenUrls).toEqual([])
  })
})

/** AG-UI 客户端桩：捕获实例与订阅回调，runAgent 挂起由测试手动驱动（流式/停止用） */
vi.mock('@ag-ui/client', () => {
  const instances: {
    abortRun: ReturnType<typeof vi.fn>
    setMessages: ReturnType<typeof vi.fn>
    subscriber: Record<string, (arg: unknown) => void>
    resolve: (value: unknown) => void
  }[] = []
  class HttpAgent {
    abortRun = vi.fn()
    setMessages = vi.fn()
    subscriber: Record<string, (arg: unknown) => void> = {}
    resolve: (value: unknown) => void = () => {}
    constructor(public config: { url: string; headers: Record<string, string>; threadId: string }) {
      instances.push(this as never)
    }
    runAgent(_params: unknown, subscriber: Record<string, (arg: unknown) => void>) {
      this.subscriber = subscriber
      return new Promise((resolve) => { this.resolve = resolve })
    }
  }
  return { HttpAgent, __instances: instances }
})

async function lastAgent() {
  const mod = (await import('@ag-ui/client')) as unknown as { __instances: { config: { url: string; headers: Record<string, string>; threadId: string }; abortRun: ReturnType<typeof vi.fn>; subscriber: Record<string, (arg: { event: { messageId?: string; delta?: string } }) => void>; resolve: (v: unknown) => void }[] }
  return mod.__instances[mod.__instances.length - 1]
}

function panelOf(widget: MoaiWidget) {
  return (widget as unknown as { shadow: ShadowRoot }).shadow.querySelector<HTMLDivElement>('.panel')!
}

describe('MoaiWidget 显示形式与会话恢复', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('inline 模式渲染进占位容器，无悬浮按钮且面板进文档流', async () => {
    installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ displayMode: 'inline', openingStatement: '你好，欢迎咨询' }))
      if (url.includes('/teamres/token')) return jsonResponse({ accessToken: 'tok-1', refreshToken: 'r1', expiresIn: 3600, tokenType: 'user' })
      throw new Error(`unexpected fetch: ${url}`)
    })
    const host = document.createElement('div')
    host.id = 'chat-box'
    document.body.appendChild(host)

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER, mode: 'inline', target: '#chat-box' })
    await widget.init()

    const root = host.querySelector('.moai-widget-root')!
    expect(root).toBeTruthy()
    expect(root.classList.contains('inline-host')).toBe(true)
    const shadow = root.shadowRoot!
    expect(shadow.querySelector('.launcher')).toBeNull()
    // inline 面板样式直接可见（不依赖 .open），进入占位容器文档流
    const styleText = shadow.querySelector('style')!.textContent ?? ''
    expect(styleText).toContain('.inline-host .panel')
    expect(styleText).toContain('display: flex')
    // 开场白自动展示
    expect(shadow.querySelector('.messages')!.textContent).toContain('你好，欢迎咨询')
  })

  it('放大按钮切换近全屏浮层，ESC 退出', async () => {
    installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint())
      throw new Error(`unexpected fetch: ${url}`)
    })
    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    await widget.init()

    const panel = panelOf(widget)
    panel.querySelector<HTMLButtonElement>('.expand')!.click()
    expect(panel.classList.contains('expanded')).toBe(true)
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    expect(panel.classList.contains('expanded')).toBe(false)
  })

  it('存量 kebab 位置（bottom-left）同样渲染到左侧', async () => {
    installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ position: 'bottom-left' }))
      throw new Error(`unexpected fetch: ${url}`)
    })
    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    await widget.init()

    const styleText = (widget as unknown as { shadow: ShadowRoot }).shadow.querySelector('style')!.textContent ?? ''
    expect(styleText).toContain('left: 24px')
  })

  it('localStorage 有会话 id 时恢复历史消息（markdown 渲染 + 思考块），无会话时展示开场白', async () => {
    localStorage.setItem(`moai-widget-session-${APP_ID}`, '0b6ead87-1234-4bcd-9a35-351d1a3b0d61')
    installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ defaultOpen: true, openingStatement: '开场白文案' }))
      if (url.includes('/teamres/token')) return jsonResponse({ accessToken: 'tok-1', refreshToken: 'r1', expiresIn: 3600, tokenType: 'user' })
      if (url.includes('/messages')) {
        return jsonResponse({ items: [
          { role: 'user', content: '看下这段代码' },
          { role: 'assistant', reasoning: '先分析需求', content: '**加粗** 与 `code`' },
        ] })
      }
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    await widget.init()
    await new Promise((r) => setTimeout(r, 0))

    const messages = (widget as unknown as { shadow: ShadowRoot }).shadow.querySelector('.messages')!
    expect(messages.textContent).toContain('看下这段代码')
    expect(messages.querySelector('.msg-body strong')?.textContent).toBe('加粗')
    expect(messages.querySelector('.msg-body code')?.textContent).toBe('code')
    // 历史消息的思考块存在且默认折叠
    const reasoning = messages.querySelector('.reasoning')!
    expect(reasoning.textContent).toContain('先分析需求')
    expect(reasoning.classList.contains('collapsed')).toBe(true)
  })

  it('新对话按钮清空消息区与持久化会话并重开场白', async () => {
    localStorage.setItem(`moai-widget-session-${APP_ID}`, '0b6ead87-1234-4bcd-9a35-351d1a3b0d61')
    installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ defaultOpen: true, openingStatement: '开场白文案' }))
      if (url.includes('/teamres/token')) return jsonResponse({ accessToken: 'tok-1', refreshToken: 'r1', expiresIn: 3600, tokenType: 'user' })
      if (url.includes('/messages')) return jsonResponse({ items: [{ role: 'user', content: '历史消息' }] })
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    await widget.init()
    await new Promise((r) => setTimeout(r, 0))
    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    expect(shadow.querySelector('.messages')!.textContent).toContain('历史消息')

    panelOf(widget).querySelector<HTMLButtonElement>('.new-chat')!.click()
    expect(shadow.querySelector('.messages')!.textContent).not.toContain('历史消息')
    expect(shadow.querySelector('.messages')!.textContent).toContain('开场白文案')
    expect(localStorage.getItem(`moai-widget-session-${APP_ID}`)).toBeNull()
  })
})

describe('MoaiWidget 流式对话交互', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function mountChatWidget(historyItems: unknown[] = []) {
    if (historyItems.length > 0) {
      localStorage.setItem(`moai-widget-session-${APP_ID}`, '0b6ead87-1234-4bcd-9a35-351d1a3b0d61')
    }
    installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ defaultOpen: true }))
      if (url.includes('/teamres/token')) return jsonResponse({ accessToken: 'tok-1', refreshToken: 'r1', expiresIn: 3600, tokenType: 'user' })
      if (url.includes('/messages')) return jsonResponse({ items: historyItems })
      if (url.includes('/session')) return jsonResponse({ value: 'sess-stream-1' })
      throw new Error(`unexpected fetch: ${url}`)
    })
    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    await widget.init()
    await new Promise((r) => setTimeout(r, 0))
    return widget
  }

  async function typeAndSend(widget: MoaiWidget, text: string) {
    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    const textarea = shadow.querySelector<HTMLTextAreaElement>('textarea')!
    textarea.value = text
    textarea.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
    await new Promise((r) => setTimeout(r, 0))
  }

  it('reasoning 流渲染思考块，正文到达后折叠并转 markdown，完成后出现复制按钮', async () => {
    const widget = await mountChatWidget()
    await typeAndSend(widget, '讲讲 SSRF')

    const agent = await lastAgent()
    agent.subscriber.onReasoningStartEvent({ event: {} })
    agent.subscriber.onReasoningMessageContentEvent({ event: { messageId: 'r1', delta: '先想一下' } })
    await new Promise((r) => setTimeout(r, 0))
    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    let reasoning = shadow.querySelector('.reasoning') as HTMLDivElement
    expect(reasoning.textContent).toContain('先想一下')
    expect(reasoning.classList.contains('collapsed')).toBe(false)

    agent.subscriber.onTextMessageStartEvent({ event: { messageId: 'm1' } })
    agent.subscriber.onTextMessageContentEvent({ event: { messageId: 'm1', delta: '**结论**：注意防护' } })
    await new Promise((r) => setTimeout(r, 0))
    reasoning = shadow.querySelector('.reasoning') as HTMLDivElement
    expect(reasoning.classList.contains('collapsed')).toBe(true)
    // 流式期间纯文本 + 光标
    const body = shadow.querySelector('.msg-body') as HTMLDivElement
    expect(body.querySelector('.cursor')).toBeTruthy()

    agent.subscriber.onRunFinishedEvent({ event: {} })
    agent.resolve({})
    await new Promise((r) => setTimeout(r, 0))
    expect(body.querySelector('strong')?.textContent).toBe('结论')
    expect(body.querySelector('.cursor')).toBeNull()
    const copyBtn = shadow.querySelector<HTMLButtonElement>('.msg-actions button')
    expect(copyBtn?.textContent).toContain('复制')
  })

  it('流式期间发送按钮变停止，点击调 abortRun 保留已生成内容', async () => {
    const widget = await mountChatWidget()
    await typeAndSend(widget, '写一首诗')

    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    const sendBtn = shadow.querySelector<HTMLButtonElement>('.input-row button')!
    expect(sendBtn.textContent).toBe('■')

    const agent = await lastAgent()
    agent.subscriber.onTextMessageStartEvent({ event: { messageId: 'm1' } })
    agent.subscriber.onTextMessageContentEvent({ event: { messageId: 'm1', delta: '床前明月光' } })

    sendBtn.click()
    expect(agent.abortRun).toHaveBeenCalledTimes(1)
    agent.resolve({})
    await new Promise((r) => setTimeout(r, 0))

    expect(sendBtn.textContent).toBe('➤')
    const body = shadow.querySelector('.msg-body') as HTMLDivElement
    expect(body.textContent).toContain('床前明月光')
  })

  it('运行失败时展示重试按钮，点击重发上一条用户消息', async () => {
    const widget = await mountChatWidget()
    await typeAndSend(widget, '帮我总结')

    const agent = await lastAgent()
    agent.subscriber.onTextMessageStartEvent({ event: { messageId: 'm1' } })
    agent.subscriber.onTextMessageContentEvent({ event: { messageId: 'm1', delta: '' } })
    agent.subscriber.onRunErrorEvent({ event: {} })
    agent.resolve({})
    await new Promise((r) => setTimeout(r, 0))

    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    const retryBtn = [...shadow.querySelectorAll<HTMLButtonElement>('.msg-actions button')].find((b) => b.textContent?.includes('重试'))
    expect(retryBtn).toBeTruthy()

    retryBtn!.click()
    await new Promise((r) => setTimeout(r, 0))
    const second = await lastAgent()
    expect(second).not.toBe(agent)
    expect(shadow.querySelectorAll('.msg.user').length).toBe(2)
  })

  it('assistant 内容先转义再渲染 markdown，无 script 注入面', async () => {
    const widget = await mountChatWidget([
      { role: 'assistant', content: '<script>alert(1)</script> 与 [链接](https://example.com)' },
    ])
    const shadow = (widget as unknown as { shadow: ShadowRoot }).shadow
    const body = shadow.querySelector('.msg-body') as HTMLDivElement
    expect(body.querySelector('script')).toBeNull()
    expect(body.innerHTML).toContain('&lt;script&gt;')
    const link = body.querySelector('a')!
    expect(link.getAttribute('href')).toBe('https://example.com')
    expect(link.getAttribute('rel')).toContain('noopener')
  })
})
