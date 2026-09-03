import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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

describe('DynamicPluginPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(pluginApi.getManagePlugins).mockResolvedValue(MOCK_INSTANCES)
    vi.mocked(pluginApi.getDynamicTemplates).mockResolvedValue(MOCK_TEMPLATES)
  })

  it('渲染实例列表并加载模板', async () => {
    render(<DynamicPluginPanel classifies={[{ classifyId: 1, name: '工具' }]} />)

    await waitFor(() => {
      expect(pluginApi.getManagePlugins).toHaveBeenCalledWith('dynamic')
    })
    expect(await screen.findByText('greet_cn')).toBeInTheDocument()
    expect(await screen.findByText('dynamic_greet')).toBeInTheDocument()
  })

  it('点击新建实例打开弹窗', async () => {
    const user = userEvent.setup()
    render(<DynamicPluginPanel classifies={[]} />)
    await screen.findByText('greet_cn')

    await user.click(screen.getByText('新建实例'))
    expect(await screen.findByText('实例 Key')).toBeInTheDocument()
  })

  it('删除实例调用 deleteDynamicPlugin', async () => {
    const user = userEvent.setup()
    vi.mocked(pluginApi.deleteDynamicPlugin).mockResolvedValue(null)
    render(<DynamicPluginPanel classifies={[]} />)
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
