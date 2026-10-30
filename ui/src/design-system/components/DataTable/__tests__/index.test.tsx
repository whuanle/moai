import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import '@/i18n'
import { DataTable } from '../'

interface Row {
  id: number
  name: string
}

describe('DataTable', () => {
  it('renders columns and rows', () => {
    render(
      <DataTable<Row>
        rowKey="id"
        columns={[{ title: '名称', dataIndex: 'name' }]}
        dataSource={[{ id: 1, name: 'MoAI' }]}
        pagination={false}
      />,
    )
    expect(screen.getByText('名称')).toBeInTheDocument()
    expect(screen.getByText('MoAI')).toBeInTheDocument()
  })
  it('renders refresh action when onRefresh provided', () => {
    const onRefresh = vi.fn()
    render(
      <DataTable
        dataSource={[]}
        columns={[{ title: 'c', dataIndex: 'x' }]}
        onRefresh={onRefresh}
        pagination={false}
      />,
    )
    expect(screen.getByText('刷新')).toBeInTheDocument()
    fireEvent.click(screen.getByText('刷新'))
    expect(onRefresh).toHaveBeenCalled()
  })
  it('does not render refresh action when onRefresh is absent', () => {
    render(<DataTable dataSource={[]} columns={[{ title: 'c', dataIndex: 'x' }]} pagination={false} />)
    expect(screen.queryByText('刷新')).not.toBeInTheDocument()
  })
  it('renders toolbar when provided', () => {
    render(
      <DataTable
        dataSource={[]}
        columns={[{ title: 'c', dataIndex: 'x' }]}
        toolbar={<button>自定义工具</button>}
        pagination={false}
      />,
    )
    expect(screen.getByText('自定义工具')).toBeInTheDocument()
  })
  it('renders as a container-colored card to separate from bare toolbar row', () => {
    // 测试环境无应用 ConfigProvider，断言取 antd 默认主题 token 值（colorBgContainer/colorBorderSecondary/borderRadiusLG）
    const { container } = render(
      <DataTable dataSource={[]} columns={[{ title: 'c', dataIndex: 'x' }]} pagination={false} />,
    )
    const card = container.firstElementChild as HTMLElement
    expect(card).toHaveStyle({ background: '#ffffff' })
    expect(card).toHaveStyle({ border: '1px solid #f0f0f0' })
    expect(card).toHaveStyle({ 'border-radius': '8px' })
    expect(card).toHaveStyle({ overflow: 'hidden' })
  })
})
