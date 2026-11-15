import { describe, expect, it } from 'vitest'
import { isTokenExpired } from '@/utils/jwt'

function makeJwt(expOffsetSeconds: number): string {
  const now = Math.floor(Date.now() / 1000)
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_')
  return `${encode({ alg: 'none', typ: 'JWT' })}.${encode({ exp: now + expOffsetSeconds, iat: now })}.sig`
}

describe('isTokenExpired', () => {
  it('默认只判断真实过期：仍在有效期内返回 false', () => {
    expect(isTokenExpired(makeJwt(30))).toBe(false)
  })

  it('默认只判断真实过期：已过期返回 true', () => {
    expect(isTokenExpired(makeJwt(-1))).toBe(true)
  })

  it('提前量：剩余有效期不足提前量时视为需要续期', () => {
    expect(isTokenExpired(makeJwt(120), 300)).toBe(true)
  })

  it('提前量：剩余有效期超过提前量时不续期', () => {
    expect(isTokenExpired(makeJwt(1800), 300)).toBe(false)
  })

  it('无法解码的 token 一律视为过期', () => {
    expect(isTokenExpired('not-a-jwt', 300)).toBe(true)
  })
})
