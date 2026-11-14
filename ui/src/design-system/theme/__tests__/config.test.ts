import { describe, expect, it } from 'vitest'
import { theme } from 'antd'
import {
  defaultThemeKey,
  defaultThemeKeyByMode,
  getThemeConfig,
  getThemePreset,
  isThemeKey,
  themePresetList,
  themePresets,
} from '../config'

describe('theme config', () => {
  it('returns a config for the default preset', () => {
    const config = getThemeConfig(defaultThemeKey)
    expect(config.token?.colorPrimary).toBe('#6D5AE6')
    expect(config.algorithm).toBe(theme.defaultAlgorithm)
  })

  it('exposes four light and four dark presets with unique keys', () => {
    expect(themePresetList).toHaveLength(8)
    expect(new Set(themePresetList.map((preset) => preset.key)).size).toBe(8)
    expect(themePresetList.filter((preset) => preset.mode === 'light')).toHaveLength(4)
    expect(themePresetList.filter((preset) => preset.mode === 'dark')).toHaveLength(4)
  })

  it('builds a mode-correct full config for every preset', () => {
    for (const preset of themePresetList) {
      const config = themePresets[preset.key].config
      expect(config.algorithm).toBe(preset.mode === 'dark' ? theme.darkAlgorithm : theme.defaultAlgorithm)
      expect(config.token?.colorPrimary).toBe(preset.palette.accent)
      expect(config.token?.colorBgLayout).toBe(preset.palette.bgLayout)
      expect(config.token?.colorBgContainer).toBe(preset.palette.bgContainer)
      expect(config.components?.Menu).toBeDefined()
      expect(config.components?.Layout).toBeDefined()
    }
  })

  it('keeps legacy light/dark keys as family defaults', () => {
    expect(defaultThemeKeyByMode.light).toBe('light')
    expect(defaultThemeKeyByMode.dark).toBe('dark')
    expect(getThemePreset('light').mode).toBe('light')
    expect(getThemePreset('dark').mode).toBe('dark')
  })

  it('validates theme keys', () => {
    expect(isThemeKey('green')).toBe(true)
    expect(isThemeKey('midnight')).toBe(true)
    expect(isThemeKey('light')).toBe(true)
    expect(isThemeKey('nope')).toBe(false)
    expect(isThemeKey(null)).toBe(false)
  })

  it('gives every preset an i18n name key and a fallback name', () => {
    for (const preset of themePresetList) {
      expect(preset.nameKey).toBe(`common.themePresets.${preset.key}`)
      expect(preset.name).toBeTruthy()
    }
  })
})
