import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AuditCell } from '../'

describe('AuditCell', () => {
  it('两行展示操作人与时间', () => {
    render(<AuditCell user="admin" time="2026-09-04T14:06:00+08:00" />)
    expect(screen.getByText('admin')).toBeInTheDocument()
    expect(screen.getByText(/2026-09-04 14:06/)).toBeInTheDocument()
  })

  it('无操作人时降级为单行时间', () => {
    render(<AuditCell time="2026-09-04T14:06:00+08:00" />)
    expect(screen.getByText(/2026-09-04 14:06/)).toBeInTheDocument()
  })

  it('空值显示占位符 -', () => {
    render(<AuditCell />)
    expect(screen.getByText('-')).toBeInTheDocument()
  })
})
