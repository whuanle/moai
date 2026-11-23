import { describe, expect, it, beforeEach } from 'vitest'
import {
  clearPendingRedirect,
  consumePendingRedirect,
  isSafeRedirectTarget,
  PENDING_REDIRECT_STORAGE_KEY,
  parseOAuthState,
  stashPendingRedirect,
  toBindAuthorizeUrl,
} from '../oauth'

describe('isSafeRedirectTarget（回跳目标安全校验）', () => {
  it('仅放行本站相对路径', () => {
    expect(isSafeRedirectTarget('/sso/authorize?client=report')).toBe(true)
    expect(isSafeRedirectTarget('/apps')).toBe(true)
  })

  it('拒绝绝对地址与协议相对地址（防开放重定向）', () => {
    expect(isSafeRedirectTarget('https://evil.local')).toBe(false)
    expect(isSafeRedirectTarget('//evil.local')).toBe(false)
    expect(isSafeRedirectTarget('')).toBe(false)
    expect(isSafeRedirectTarget(null)).toBe(false)
    expect(isSafeRedirectTarget(undefined)).toBe(false)
  })
})

describe('stashPendingRedirect / consumePendingRedirect（登录后回跳暂存）', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('暂存后消费返回原目标，且读取即清除', () => {
    stashPendingRedirect('/sso/authorize?client=report&state=st1')
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBe('/sso/authorize?client=report&state=st1')

    expect(consumePendingRedirect()).toBe('/sso/authorize?client=report&state=st1')
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBeNull()
    expect(consumePendingRedirect()).toBeNull()
  })

  it('不安全目标不入暂存', () => {
    stashPendingRedirect('//evil.local')
    stashPendingRedirect('https://evil.local')
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBeNull()
    expect(consumePendingRedirect()).toBeNull()
  })

  it('不安全目标忽略不入暂存，已存在的旧值保留', () => {
    stashPendingRedirect('/apps')
    stashPendingRedirect('//evil.local')
    expect(consumePendingRedirect()).toBe('/apps')
    expect(consumePendingRedirect()).toBeNull()
  })

  it('clearPendingRedirect 直接清除不读取', () => {
    stashPendingRedirect('/apps')
    clearPendingRedirect()
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBeNull()
    expect(consumePendingRedirect()).toBeNull()
  })
})

describe('parseOAuthState / toBindAuthorizeUrl（既有 state 编解码回归）', () => {
  it('默认 state 仅含 OAuthId', () => {
    expect(parseOAuthState('guid-1')).toEqual({ oAuthId: 'guid-1', bind: false })
  })

  it('绑定弹窗 state 附加 :bind 标志', () => {
    expect(parseOAuthState('guid-1:bind')).toEqual({ oAuthId: 'guid-1', bind: true })
  })

  it('toBindAuthorizeUrl 重写 state 附加 bind 标志', () => {
    const url = toBindAuthorizeUrl('https://feishu.local/authorize?state=guid-1')
    expect(new URL(url).searchParams.get('state')).toBe('guid-1:bind')
  })
})
