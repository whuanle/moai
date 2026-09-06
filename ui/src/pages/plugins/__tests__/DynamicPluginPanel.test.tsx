import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { DynamicPluginPanel } from '../DynamicPluginPanel'
import { pluginApi, type DynamicPluginManageItem } from '@/api/plugin'

vi.mock('@/api/plugin', () => ({
  pluginApi: {
    getManagePlugins: vi.fn(),
    getDynamicTemplates: vi.fn(),
    saveDynamicPlugin: vi.fn(),
    deleteDynamicPlugin: vi.fn(),
    runPlugin: vi.fn(),
  },
}))

const MOCK_INSTANCES: DynamicPluginManageItem[] = [
  {
    id: 'i-1',
    pluginName: 'greet_cn',
    title: '中文问候',
    templeteKey: 'dynamic_greet',
    classifyId: 1,
    classifyName: '工具',
    config: '{"Prefix":"你好"}',
    paramsExample: '{"Name":"MoAI"}',
    kind: 'dynamic',
  },
]

const MOCK_TEMPLATES = [
  { key: 'dynamic_greet', name: '动态问候', isDynamic: true, configExample: '{"Prefix":"Hello"}', paramsExample: '{"Name":"MoAI"}' },
]

function renderPanel() {
  return render(
    <MemoryRouter initialEntries={['/plugin?tab=dynamic']}>
      <Routes>
        <Route path="/plugin" element={<DynamicPluginPanel classifies={[]} />} />
        <Route path="/plugin/templates" element={<div>模板列表页面标记</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('DynamicPluginPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(pluginApi.getManagePlugins).mockResolvedValue(MOCK_INSTANCES)
    vi.mocked(pluginApi.getDynamicTemplates).mockResolvedValue(MOCK_TEMPLATES)
  })

  it('渲染实例列表并加载模板', async () => {
    renderPanel()

    await waitFor(() => {
      expect(pluginApi.getManagePlugins).toHaveBeenCalledWith('dynamic')
    })
    expect(await screen.findByText('greet_cn')).toBeInTheDocument()
    expect(await screen.findByText('dynamic_greet')).toBeInTheDocument()
  })

  it('点击新建实例打开弹窗', async () => {
    const user = userEvent.setup()
    renderPanel()
    await screen.findByText('greet_cn')

    await user.click(screen.getByText('新建实例'))
    expect(await screen.findByText('实例 Key')).toBeInTheDocument()
  })

  it('模板下拉支持搜索', async () => {
    const user = userEvent.setup()
    renderPanel()
    await screen.findByText('greet_cn')

    await user.click(screen.getByText('新建实例'))
    const placeholder = await screen.findByText('请选择动态插件模板')
    expect(placeholder.closest('.ant-select')).toHaveClass('ant-select-show-search')
  })

  it('点击模板列表按钮跳转到模板页面', async () => {
    const user = userEvent.setup()
    renderPanel()
    await screen.findByText('greet_cn')

    await user.click(screen.getByRole('button', { name: /模板列表/ }))
    expect(await screen.findByText('模板列表页面标记')).toBeInTheDocument()
  })

  it('删除实例调用 deleteDynamicPlugin', async () => {
    const user = userEvent.setup()
    vi.mocked(pluginApi.deleteDynamicPlugin).mockResolvedValue(null)
    renderPanel()
    await screen.findByText('greet_cn')

    const delBtn = await screen.findByRole('button', { name: /删除插件/i })
    await user.click(delBtn)
    const okBtn = await screen.findByRole('button', { name: /ok/i })
    await user.click(okBtn)
    await waitFor(() => {
      expect(pluginApi.deleteDynamicPlugin).toHaveBeenCalledWith('greet_cn')
    })
  })
})
