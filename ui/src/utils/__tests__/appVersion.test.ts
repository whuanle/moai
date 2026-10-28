import { describe, expect, it } from 'vitest'
import { APP_VERSION_PATTERN, formatAppVersion, parseAppVersion } from '../appVersion'

describe('appVersion（应用版本号 int 编码与 aaa.bb.cc 文本互转）', () => {
  it('format：int 编码还原为 aaa.bb.cc', () => {
    expect(formatAppVersion(10000)).toBe('1.0.0')
    expect(formatAppVersion(10203)).toBe('1.2.3')
    expect(formatAppVersion(1)).toBe('0.0.1')
    expect(formatAppVersion(102030)).toBe('10.20.30')
    expect(formatAppVersion(9999999)).toBe('999.99.99')
    expect(formatAppVersion(null)).toBe('')
    expect(formatAppVersion(undefined)).toBe('')
  })

  it('parse：aaa.bb.cc 文本编码为 int，可直接比较大小（1.10.0 > 1.9.0）', () => {
    expect(parseAppVersion('1.0.0')).toBe(10000)
    expect(parseAppVersion('1.2.3')).toBe(10203)
    expect(parseAppVersion('01.02.03')).toBe(10203)
    expect((parseAppVersion('1.10.0') ?? 0) > (parseAppVersion('1.9.0') ?? 0)).toBe(true)
  })

  it('parse：非法格式返回 null', () => {
    expect(parseAppVersion('1.2')).toBeNull()
    expect(parseAppVersion('1.2.3.4')).toBeNull()
    expect(parseAppVersion('1234.1.1')).toBeNull()
    expect(parseAppVersion('1.2.333')).toBeNull()
    expect(parseAppVersion('a.b.c')).toBeNull()
    expect(parseAppVersion('')).toBeNull()
    expect(parseAppVersion(undefined)).toBeNull()
  })

  it('pattern：与后端 AppVersionHelper 同规则（首位 1~3 位，后两段各 1~2 位）', () => {
    expect(APP_VERSION_PATTERN.test('1.0.0')).toBe(true)
    expect(APP_VERSION_PATTERN.test('999.99.99')).toBe(true)
    expect(APP_VERSION_PATTERN.test('1234.0.0')).toBe(false)
    expect(APP_VERSION_PATTERN.test('1.0.100')).toBe(false)
    expect(APP_VERSION_PATTERN.test('1.0')).toBe(false)
  })

  it('round-trip：parse 后 format 还原原文', () => {
    for (const text of ['0.0.1', '1.2.3', '10.20.30', '999.99.99']) {
      expect(formatAppVersion(parseAppVersion(text))).toBe(text)
    }
  })
})
