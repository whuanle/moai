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
  it('配置 tokenProvider 时由业务后端签发 token，组件不再自己换 token 也不发送 key', async () => {
    const provider = vi.fn(async () => 'tok-from-backend')
    const fetchMock = installFetch(async (url, init) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ isAuth: true, defaultOpen: true }))
      if (url.includes('/session')) {
        expect(init?.headers).toMatchObject({ Authorization: 'Bearer tok-from-backend' })
        return jsonResponse({ value: 'sess-1' })
      }
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER, accessAppKey: 'moai-ac-should-not-be-used', tokenProvider: provider })
    await widget.init()

    expect(provider).toHaveBeenCalledTimes(1)
    const tokenUrls = fetchMock.mock.calls.map((call) => call[0] as string).filter((url) => url.includes('/external/token'))
    expect(tokenUrls).toEqual([])

    const widgetRoot = document.querySelector('.moai-widget-root')!
    expect(widgetRoot).toBeTruthy()
    expect(widgetRoot.shadowRoot!.querySelector('.messages')!.textContent).not.toContain('失败')
  })

  it('onRequest 中间件可改写组件发出的全部 API 请求（access-point 与建会话）', async () => {
    const onRequest = vi.fn((ctx: WidgetRequestContext) => ({
      ...ctx,
      headers: { ...ctx.headers, 'X-Biz-Trace': 'trace-1' },
    }))
    const fetchMock = installFetch(async (url, init) => {
      expect((init?.headers as Record<string, string>)['X-Biz-Trace']).toBe('trace-1')
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ isAuth: true, defaultOpen: true }))
      if (url.includes('/session')) return jsonResponse({ value: 'sess-2' })
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER, tokenProvider: async () => 'tok-1', onRequest })
    await widget.init()

    // access-point 一次 + 建会话一次，均先经中间件
    expect(onRequest).toHaveBeenCalledTimes(2)
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('access token 临近过期时用 refresh_token 换新，新 token 生效期内不重复刷新', async () => {
    const newAccess = fakeJwt(Math.floor(Date.now() / 1000) + 3600)
    const fetchMock = installFetch(async (url) => {
      if (url.includes('/external/token/refresh')) return jsonResponse({ accessToken: newAccess, refreshToken: 'refresh-2', expiresIn: 3600, tokenType: 'user' })
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    const internals = widget as unknown as { token?: string; refreshCredential?: string; getToken(): Promise<string | undefined> }
    internals.token = fakeJwt(Math.floor(Date.now() / 1000) - 10)
    internals.refreshCredential = 'refresh-1'

    await expect(internals.getToken()).resolves.toBe(newAccess)
    const refreshCalls = fetchMock.mock.calls.filter((call) => (call[0] as string).includes('/external/token/refresh'))
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

  it('需授权应用无 key 且无 tokenProvider 时换 token 失败并提示接入方式', async () => {
    installFetch(async (url) => {
      if (url.includes('/access-point')) return jsonResponse(accessPoint({ isAuth: true, defaultOpen: true }))
      if (url.includes('/external/token')) return jsonResponse({ message: '该应用需要通过应用接入 key 授权访问.' }, 403)
      throw new Error(`unexpected fetch: ${url}`)
    })

    const widget = new MoaiWidget({ appId: APP_ID, server: SERVER })
    await widget.init()

    const messages = document.querySelector('.moai-widget-root')!.shadowRoot!.querySelector('.messages')!
    expect(messages.textContent).toContain('tokenProvider')
  })
})
