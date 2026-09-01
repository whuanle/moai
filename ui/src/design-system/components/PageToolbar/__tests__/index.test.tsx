import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { PageToolbar } from '../'

describe('PageToolbar', () => {
  it('renders filters and actions together', () => {
    render(
      <PageToolbar filters={<button>筛选</button>} actions={<button>新建</button>} />,
    )
    expect(screen.getByText('筛选')).toBeInTheDocument()
    expect(screen.getByText('新建')).toBeInTheDocument()
  })

  it('still renders actions when filters are absent', () => {
    render(<PageToolbar actions={<button>新建</button>} />)
    expect(screen.getByText('新建')).toBeInTheDocument()
  })
})
