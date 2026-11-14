import { describe, expect, it } from 'vitest'
import i18n from '../index'

/**
 * 真实 i18next 插值回归：i18next 默认插值分隔符为双花括号 {{ }}，
 * 文案若写成单花括号 {x} 不会被插值（原样输出到界面）；
 * 组件测试 mock 了 t() 无法发现这类缺陷，此处用真实实例守住。
 */
describe('i18n 插值回归（真实 i18next 实例）', () => {
  it.each([
    ['appMemory.platformDimensions', { dims: 1024 }, '1024'],
    ['appMemory.statsHint', { count: 7, users: 3 }, '7'],
    ['appMemory.myMemoriesHint', { count: 5 }, '5'],
  ])('%s 参数插值生效且不含字面占位符', (key, params, expected) => {
    const text = i18n.t(key, params) as string
    expect(text).toContain(expected)
    expect(text).not.toMatch(/\{\w+\}/)
  })
})
