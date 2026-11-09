import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { BookOutlined } from '@ant-design/icons'
import { SiderHeader } from '../'

function renderHeader() {
  return render(
    <MemoryRouter initialEntries={['/team/1/wiki/7/files']}>
      <Routes>
        <Route path="/team/1/wiki/7/files" element={<SiderHeader name="产品文档" avatarFallback={<BookOutlined />} backTo="/team/1/knowledge" backLabel="返回知识库" />} />
        <Route path="/team/1/knowledge" element={<div>知识库列表页</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('SiderHeader', () => {
  it('上段渲染头像+名称，下段渲染整宽返回按钮', () => {
    renderHeader()
    expect(screen.getByText('产品文档')).toBeInTheDocument()
    const button = screen.getByRole('button', { name: /返回知识库/ })
    expect(button).toHaveClass('ant-btn-block')
    expect(button.querySelector('.anticon-arrow-left')).not.toBeNull()
    expect(document.querySelector('.ant-avatar-square')).not.toBeNull()
  })

  it('点击返回按钮跳转上级列表页', async () => {
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: /返回知识库/ }))
    expect(screen.getByText('知识库列表页')).toBeInTheDocument()
  })

  it('无头像地址时回退传入图标兜底', () => {
    renderHeader()
    expect(document.querySelector('.ant-avatar .anticon-book')).not.toBeNull()
  })

  it('无图标兜底时回退名称首字符', () => {
    render(
      <MemoryRouter>
        <SiderHeader name="abc" backTo="/team/1" backLabel="返回" />
      </MemoryRouter>,
    )
    expect(screen.getByText('A')).toBeInTheDocument()
  })
})
