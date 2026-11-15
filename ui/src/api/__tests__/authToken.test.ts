import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAppStore } from '@/store/app'

const { getAnonymousClientMock } = vi.hoisted(() => ({
  getAnonymousClientMock: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({
  getAnonymousClient: getAnonymousClientMock,
  getApiClient: vi.fn(),
}))

import { checkToken } from '@/api/auth'

const refreshPost = vi.fn()

function makeJwt(expOffsetSeconds: number): string {
  const now = Math.floor(Date.now() / 1000)
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_')
  return `${encode({ alg: 'none', typ: 'JWT' })}.${encode({ exp: now + expOffsetSeconds, iat: now })}.sig`
}

function setUserInfo(overrides: Record<string, unknown>) {
  useAppStore.setState({ userInfo: overrides })
}

describe('checkToken（access token 静默续期）', () => {
  beforeEach(() => {
    useAppStore.setState({ userInfo: null })
    refreshPost.mockReset()
    getAnonymousClientMock.mockReset()
    getAnonymousClientMock.mockReturnValue({ api: { auth: { refresh_token: { post: refreshPost } } } })
  })

  it('剩余有效期超过提前量时直接放行，不请求续期', async () => {
    setUserInfo({ accessToken: makeJwt(1800), refreshToken: makeJwt(86400) })

    await expect(checkToken()).resolves.toBe(true)
    expect(refreshPost).not.toHaveBeenCalled()
  })

  it('剩余不足 5 分钟时提前静默续期，并保留本地资料', async () => {
    setUserInfo({
      accessToken: makeJwt(120),
      refreshToken: makeJwt(86400),
      isAdmin: true,
      nickName: '管理员',
    })
    const nextAccessToken = makeJwt(1800)
    refreshPost.mockResolvedValue({
      accessToken: nextAccessToken,
      refreshToken: makeJwt(86400),
      userId: '1',
      userName: 'admin',
    })

    await expect(checkToken()).resolves.toBe(true)

    const userInfo = useAppStore.getState().userInfo
    expect(refreshPost).toHaveBeenCalledTimes(1)
    expect(userInfo?.accessToken).toBe(nextAccessToken)
    expect(userInfo?.isAdmin).toBe(true)
    expect(userInfo?.nickName).toBe('管理员')
  })

  it('并发复检复用同一次续期请求', async () => {
    setUserInfo({ accessToken: makeJwt(120), refreshToken: makeJwt(86400) })
    refreshPost.mockResolvedValue({ accessToken: makeJwt(1800), refreshToken: makeJwt(86400) })

    const [a, b] = await Promise.all([checkToken(), checkToken()])

    expect(a).toBe(true)
    expect(b).toBe(true)
    expect(refreshPost).toHaveBeenCalledTimes(1)
  })

  it('续期请求异常但 access token 未真正过期时沿用会话，不登出', async () => {
    setUserInfo({ accessToken: makeJwt(120), refreshToken: makeJwt(86400) })
    refreshPost.mockRejectedValue(new Error('502 Bad Gateway'))

    await expect(checkToken()).resolves.toBe(true)
    expect(useAppStore.getState().userInfo?.accessToken).toBeTruthy()
  })

  it('续期请求异常且 access token 已过期时判定失败（需要重新登录）', async () => {
    setUserInfo({ accessToken: makeJwt(-10), refreshToken: makeJwt(86400) })
    refreshPost.mockRejectedValue(new Error('502 Bad Gateway'))

    await expect(checkToken()).resolves.toBe(false)
  })

  it('refresh token 已过期时直接判定失败，不请求后端', async () => {
    setUserInfo({ accessToken: makeJwt(-10), refreshToken: makeJwt(-1) })

    await expect(checkToken()).resolves.toBe(false)
    expect(refreshPost).not.toHaveBeenCalled()
  })

  it('续期响应缺少 accessToken（后端 401 被拒）时判定失败', async () => {
    setUserInfo({ accessToken: makeJwt(120), refreshToken: makeJwt(86400) })
    refreshPost.mockResolvedValue({})

    await expect(checkToken()).resolves.toBe(false)
  })
})
