import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { PluginTemplates } from '../PluginTemplates'
import { useAppStore } from '@/store/app'
import { pluginApi } from '@/api/plugin'

vi.mock('@/api/plugin', () => ({
  pluginApi: {
    getDynamicTemplates: vi.fn(),
  },
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/plugin/templates']}>
      <Routes>
        <Route path="/plugin" element={<div>动态插件页面标记</div>} />
        <Route path="/plugin/templates" element={<PluginTemplates />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('PluginTemplates', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true },
    })
    vi.mocked(pluginApi.getDynamicTemplates).mockResolvedValue([
      { key: 'dynamic_greet', name: '动态问候', description: '问候插件模板', isDynamic: true },
    ])
  })

  it('以卡片形式展示模板的 key、名称、描述', async () => {
    renderPage()

    expect(await screen.findByText('dynamic_greet')).toBeInTheDocument()
    expect(screen.getByText('动态问候')).toBeInTheDocument()
    expect(screen.getByText('问候插件模板')).toBeInTheDocument()
    expect(screen.getByText('dynamic_greet').closest('.ant-card')).not.toBeNull()
  })

  it('点击返回按钮回到动态插件页', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('dynamic_greet')

    await user.click(screen.getByRole('button', { name: /返回插件/ }))
    expect(await screen.findByText('动态插件页面标记')).toBeInTheDocument()
  })

  it('描述为空时显示占位文案', async () => {
    vi.mocked(pluginApi.getDynamicTemplates).mockResolvedValue([
      { key: 'dynamic_empty', name: '无描述模板', isDynamic: true },
    ])
    renderPage()

    expect(await screen.findByText('暂无描述')).toBeInTheDocument()
  })

  it('模板为空时显示空状态', async () => {
    vi.mocked(pluginApi.getDynamicTemplates).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('暂无模板')).toBeInTheDocument()
  })

  it('点击刷新重新加载模板', async () => {
    renderPage()
    await screen.findByText('dynamic_greet')

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /刷新/ }))
    await screen.findByText('dynamic_greet')
    expect(pluginApi.getDynamicTemplates).toHaveBeenCalledTimes(2)
  })
})
