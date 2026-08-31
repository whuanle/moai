import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Page } from '../'

describe('Page', () => {
  it('renders title, subtitle and children', () => {
    render(<Page title="标题" subtitle="副标题">内容</Page>)
    expect(screen.getByText('标题')).toBeInTheDocument()
    expect(screen.getByText('副标题')).toBeInTheDocument()
    expect(screen.getByText('内容')).toBeInTheDocument()
  })

  it('renders breadcrumb and extra', () => {
    render(<Page breadcrumb={[{ title: '首页' }, { title: '列表' }]} extra={<button>操作</button>} />)
    expect(screen.getByText('首页')).toBeInTheDocument()
    expect(screen.getByText('列表')).toBeInTheDocument()
    expect(screen.getByText('操作')).toBeInTheDocument()
  })
})
