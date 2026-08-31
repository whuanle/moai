import { describe, expect, it } from 'vitest'
import { getThemeConfig, themePresets, defaultThemeKey } from '../config'
import type { ThemeKey } from '../config'

describe('theme config', () => {
  it('returns a config for the default preset', () => {
    const config = getThemeConfig(defaultThemeKey)
    expect(config.token?.colorPrimary).toBe('#4A9EFF')
    expect(config.algorithm).toBeDefined()
  })
  it('supports light and dark presets', () => {
    expect(Object.keys(themePresets)).toEqual(['light', 'dark'])
    expect(getThemeConfig('dark').token?.colorPrimary).toBe('#4A9EFF')
  })
  it('keeps theme key type restricted', () => {
    const key: ThemeKey = 'light'
    expect(themePresets[key].name).toBeTruthy()
  })
})
