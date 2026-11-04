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
  it('omits reset button when onReset is not provided', () => {
    render(<QueryBar onSearch={() => {}} />)
    expect(screen.queryByText('重置')).toBeNull()
    expect(screen.getByText('查询')).toBeTruthy()
  })
  it('renders extra actions to the right of reset', () => {
    const onExtra = vi.fn()
    const onReset = vi.fn()
    render(<QueryBar onReset={onReset} extra={<button onClick={onExtra}>extra-action</button>} />)
    const reset = screen.getByText('重置')
    const extraBtn = screen.getByText('extra-action')
    // extra 渲染在重置按钮右侧（DOM 顺序其后）
    expect(reset.compareDocumentPosition(extraBtn) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    fireEvent.click(extraBtn)
    expect(onExtra).toHaveBeenCalled()
  })
})
