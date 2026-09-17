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
    Prompt: 'prompt',
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
        emoji: '🚀',
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

  it('以卡片形式展示分类的名称、表情与描述，不展示创建人/更新人及时间', async () => {
    renderPage()

    expect(await screen.findByText('职业')).toBeInTheDocument()
    expect(screen.getByText('🚀')).toBeInTheDocument()
    expect(screen.getByText('职业相关插件')).toBeInTheDocument()
    expect(screen.queryByText('创建人')).not.toBeInTheDocument()
    expect(screen.queryByText('更新人')).not.toBeInTheDocument()
    expect(screen.queryByText(/admin/)).not.toBeInTheDocument()
    expect(screen.queryByText(/2026-09-03/)).not.toBeInTheDocument()
    expect(screen.getByText('职业').closest('.ant-card')).not.toBeNull()
  })

  it('新建分类时可选择表情并随表单提交', async () => {
    const user = userEvent.setup()
    vi.mocked(classifyApi.createClassify).mockResolvedValue(2)
    renderPage()
    await screen.findByText('职业')

    await user.click(screen.getByRole('button', { name: /新增分类/ }))
    await user.type(screen.getByLabelText('分类名称'), '新分类')

    // 打开表情面板，选中一个表情后输入框回填
    await user.click(screen.getByTitle('选择表情'))
    await user.click(await screen.findByRole('button', { name: '🚀' }))
    expect(screen.getByPlaceholderText('选择表情（可选）')).toHaveValue('🚀')

    // antd 按钮对两个汉字自动插空格，可访问名实际为「保 存」
    await user.click(screen.getByRole('button', { name: /保\s*存/ }))
    expect(classifyApi.createClassify).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'plugin', name: '新分类', emoji: '🚀' }),
    )
  })

  it('编辑分类时表情回填，清除后随更新提交空串', async () => {
    const user = userEvent.setup()
    vi.mocked(classifyApi.updateClassify).mockResolvedValue(undefined)
    renderPage()
    await screen.findByText('职业')

    await user.click(screen.getByRole('button', { name: /编辑/ }))
    expect(screen.getByPlaceholderText('选择表情（可选）')).toHaveValue('🚀')

    // antd 按钮对两个汉字自动插空格，可访问名实际为「清 除」
    await user.click(screen.getByRole('button', { name: /清\s*除/ }))
    expect(screen.getByPlaceholderText('选择表情（可选）')).toHaveValue('')

    await user.click(screen.getByRole('button', { name: /保\s*存/ }))
    expect(classifyApi.updateClassify).toHaveBeenCalledWith(
      expect.objectContaining({ classifyId: 1, emoji: '' }),
    )
  })

  it('分类为空时显示空状态', async () => {
    vi.mocked(classifyApi.getClassifies).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('暂无分类')).toBeInTheDocument()
  })

  it('按类型渲染四个分类页签', () => {
    renderPage()

    expect(screen.getByRole('tab', { name: '插件' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '应用' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '知识库' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: '提示词' })).toBeInTheDocument()
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
