import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAppStore } from '@/store/app'

// 真实引入 kiota（含 401 续期重试中间件）与 auth（注册续期实现）；仅替换全局 fetch
import { getApiClient } from '@/api/kiota'
import '@/api/auth'

function makeJwt(expOffsetSeconds: number): string {
  const now = Math.floor(Date.now() / 1000)
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_')
  return `${encode({ alg: 'none', typ: 'JWT' })}.${encode({ exp: now + expOffsetSeconds, iat: now })}.sig`
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

describe('请求层 401 静默续期重试', () => {
  const requests: Array<{ url: string; authorization: string | null }> = []
  let serverInfoCalls = 0

  const nextAccessToken = makeJwt(1800)

  beforeEach(() => {
    requests.length = 0
    serverInfoCalls = 0
    useAppStore.setState({
      userInfo: {
        accessToken: makeJwt(-30),
        refreshToken: makeJwt(86400),
        userId: '1',
        userName: 'admin',
      },
    })

    vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      requests.push({ url, authorization: new Headers(init?.headers).get('Authorization') })

      if (url.includes('refresh_token')) {
        return jsonResponse({
          accessToken: nextAccessToken,
          refreshToken: makeJwt(86400),
          userId: '1',
          userName: 'admin',
        })
      }
      if (url.includes('/api/common/serverinfo')) {
        serverInfoCalls += 1
        if (serverInfoCalls === 1) return new Response(null, { status: 401 })
        return jsonResponse({ serviceUrl: 'https://moai.example.com' })
      }
      return jsonResponse({})
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('业务请求 401 → 先静默续期，再用新 token 重试并返回成功结果', async () => {
    const res = await getApiClient().api.common.serverinfo.get()

    expect(res?.serviceUrl).toBe('https://moai.example.com')
    const business = requests.filter((r) => r.url.includes('/api/common/serverinfo'))
    expect(business).toHaveLength(2)
    expect(business[1].authorization).toBe(`Bearer ${nextAccessToken}`)
    expect(requests.some((r) => r.url.includes('refresh_token'))).toBe(true)
    expect(useAppStore.getState().userInfo?.accessToken).toBe(nextAccessToken)
  })
})
