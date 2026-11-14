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

  it('orders filters, queries, divider, actions', () => {
    render(
      <PageToolbar
        filters={<button>筛选</button>}
        queries={<button>刷新</button>}
        actions={<button>新建</button>}
      />,
    )
    const filterBtn = screen.getByText('筛选')
    const queryBtn = screen.getByText('刷新')
    const actionBtn = screen.getByText('新建')
    // DOM 顺序：筛选 → 刷新（查询类） → 新建（操作类），分隔线在刷新与新建之间
    expect(filterBtn.compareDocumentPosition(queryBtn) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(queryBtn.compareDocumentPosition(actionBtn) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(document.querySelector('.ant-divider-vertical')).toBeInTheDocument()
  })
})
