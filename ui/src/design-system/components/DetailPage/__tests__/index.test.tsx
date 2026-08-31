import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import '@/i18n'
import { DetailPage } from '../'

describe('DetailPage', () => {
  it('renders title and description items', () => {
    render(
      <DetailPage
        title="详情"
        items={[{ key: 'a', label: '名称', children: 'MoAI' }]}
        onEdit={() => {}}
        onBack={() => {}}
      />,
    )
    expect(screen.getByText('详情')).toBeInTheDocument()
    expect(screen.getByText('名称')).toBeInTheDocument()
    expect(screen.getByText('MoAI')).toBeInTheDocument()
    expect(screen.getByText('返回')).toBeInTheDocument()
    expect(screen.getByText('编辑')).toBeInTheDocument()
  })

  it('renders loading skeleton when loading', () => {
    const { container } = render(<DetailPage loading items={[]} />)
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('calls onBack and onEdit on button clicks', () => {
    const onBack = vi.fn()
    const onEdit = vi.fn()
    render(<DetailPage title="详情" items={[]} onBack={onBack} onEdit={onEdit} />)
    fireEvent.click(screen.getByText('返回'))
    expect(onBack).toHaveBeenCalledTimes(1)
    fireEvent.click(screen.getByText('编辑'))
    expect(onEdit).toHaveBeenCalledTimes(1)
  })
})
