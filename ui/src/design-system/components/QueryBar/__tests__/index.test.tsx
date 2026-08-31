import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import '@/i18n'
import { QueryBar } from '../'

describe('QueryBar', () => {
  it('submits values on search', () => {
    const onSearch = vi.fn()
    render(<QueryBar onSearch={onSearch} />)
    fireEvent.click(screen.getByText('查询'))
    expect(onSearch).toHaveBeenCalled()
  })
  it('resets filters on reset', () => {
    const onReset = vi.fn()
    render(<QueryBar onReset={onReset} />)
    fireEvent.click(screen.getByText('重置'))
    expect(onReset).toHaveBeenCalled()
  })
})
