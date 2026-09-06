import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { ClassifyPage } from '../Classify'
import { useAppStore } from '@/store/app'
import { classifyApi } from '@/api/classify'

vi.mock('@/api/classify', () => ({
  classifyApi: {
    getClassifies: vi.fn(),
    createClassify: vi.fn(),
    updateClassify: vi.fn(),
    deleteClassify: vi.fn(),
  },
  ClassifyType: {
    Plugin: 'plugin',
    App: 'app',
    Kb: 'kb',
  },
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/classify']}>
      <Routes>
        <Route path="/dashboard" element={<div>仪表盘页面标记</div>} />
        <Route path="/classify" element={<ClassifyPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ClassifyPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true },
    })
    vi.mocked(classifyApi.getClassifies).mockResolvedValue([
      {
        classifyId: 1,
        name: '职业',
        description: '职业相关插件',
        createUserName: null,
        createTime: '2026-09-03T11:14:00Z',
        updateUserName: 'admin',
        updateTime: '2026-09-03T11:14:00Z',
      },
    ])
  })

  it('非管理员跳转仪表盘', () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '2', userName: 'user', isAdmin: false } })
    renderPage()
    expect(screen.getByText('仪表盘页面标记')).toBeInTheDocument()
  })

  it('以卡片形式展示分类的名称、描述与创建人', async () => {
    renderPage()

    expect(await screen.findByText('职业')).toBeInTheDocument()
    expect(screen.getByText('职业相关插件')).toBeInTheDocument()
    expect(screen.getByText('创建人')).toBeInTheDocument()
    expect(screen.getByText(/admin · /)).toBeInTheDocument()
    expect(screen.getByText('职业').closest('.ant-card')).not.toBeNull()
  })

  it('分类为空时显示空状态', async () => {
    vi.mocked(classifyApi.getClassifies).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('暂无分类')).toBeInTheDocument()
  })

  it('删除需确认，确认后调用删除接口', async () => {
    const user = userEvent.setup()
    vi.mocked(classifyApi.deleteClassify).mockResolvedValue(undefined)
    renderPage()
    await screen.findByText('职业')

    await user.click(screen.getByRole('button', { name: /删除/ }))
    expect(await screen.findByText('确定删除该分类吗？')).toBeInTheDocument()
    expect(classifyApi.deleteClassify).not.toHaveBeenCalled()

    // Popconfirm 弹出确认按钮（无 ConfigProvider 环境下默认英文 OK）
    await user.click(await screen.findByRole('button', { name: /ok/i }))
    expect(classifyApi.deleteClassify).toHaveBeenCalledWith(1)
  })
})
