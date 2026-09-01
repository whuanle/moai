import { describe, expect, it } from 'vitest'
import { brandColors, colorPrimary, radius, spacing, fontFamily } from '../tokens'

describe('design tokens', () => {
  it('uses brand blue as the primary color', () => {
    expect(colorPrimary).toBe('#2970FF')
    expect(brandColors.primary).toBe(colorPrimary)
  })
  it('exposes a spacing scale on the 8px grid', () => {
    expect(spacing.xs).toBe(8)
    expect(spacing.md).toBe(16)
    expect(spacing.lg).toBe(24)
  })
  it('exposes a default radius and font family', () => {
    expect(radius.default).toBe(8)
    expect(fontFamily).toContain('sans-serif')
  })
})
