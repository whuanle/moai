import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DynamicPluginInstanceModal } from '../components/DynamicPluginInstanceModal'
import { pluginApi } from '@/api/plugin'
import { saveTeamDynamicPlugin } from '@/api/team-plugin'

vi.mock('@/api/plugin', () => ({
  pluginApi: { saveDynamicPlugin: vi.fn() },
}))

vi.mock('@/api/team-plugin', () => ({
  saveTeamDynamicPlugin: vi.fn(),
}))

vi.mock('@/api/classify', () => ({
  classifyApi: { getPluginClassifies: vi.fn().mockResolvedValue([]) },
  classifyLabel: (c: { name?: string | null }) => c.name ?? '',
}))

const TEMPLATES = [
  { key: 'dynamic_greet', name: '动态问候', description: '', isDynamic: true, configExample: '{"Prefix":"Hello"}' },
]

function renderModal(overrides: Partial<Parameters<typeof DynamicPluginInstanceModal>[0]> = {}) {
  const onSaved = vi.fn()
  const onClose = vi.fn()
  render(
    <DynamicPluginInstanceModal
      open
      scope="system"
      templates={TEMPLATES}
      classifies={[]}
      existingKeys={[]}
      onSaved={onSaved}
      onClose={onClose}
      {...overrides}
    />,
  )
  return { onSaved, onClose }
}

describe('DynamicPluginInstanceModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(pluginApi.saveDynamicPlugin).mockResolvedValue(null)
    vi.mocked(saveTeamDynamicPlugin).mockResolvedValue(null)
  })

  it('新建时实例 key 已被占用则报错且不提交', async () => {
    const user = userEvent.setup()
    const { onSaved } = renderModal({ existingKeys: ['dup'], presetTemplateKey: 'dynamic_greet' })

    await user.type(screen.getByLabelText('实例 Key'), 'dup')
    await user.type(screen.getByLabelText('插件标题'), '重复实例')
    await user.click(screen.getByRole('button', { name: /保 存|保存/ }))

    // feedback 在测试环境未注册为 no-op，断言被拦截的行为
    expect(pluginApi.saveDynamicPlugin).not.toHaveBeenCalled()
    expect(onSaved).not.toHaveBeenCalled()
  })

  it('创建成功回调 onSaved/onClose，配置默认取模板示例', async () => {
    const user = userEvent.setup()
    const { onSaved, onClose } = renderModal({ presetTemplateKey: 'dynamic_greet' })

    // 预选模板后下拉回显
    expect(await screen.findByText('动态问候 (dynamic_greet)')).toBeInTheDocument()

    await user.type(screen.getByLabelText('实例 Key'), 'greet_cn')
    await user.type(screen.getByLabelText('插件标题'), '中文问候')
    await user.click(screen.getByRole('button', { name: /保 存|保存/ }))

    await waitFor(() => {
      expect(pluginApi.saveDynamicPlugin).toHaveBeenCalledWith(
        expect.objectContaining({ pluginKey: 'greet_cn', templeteKey: 'dynamic_greet' }),
      )
    })
    expect(onSaved).toHaveBeenCalled()
    expect(onClose).toHaveBeenCalled()
  })

  it('编辑模式实例 Key 与模板禁用，提交沿用原 key 与模板', async () => {
    const user = userEvent.setup()
    renderModal({
      editing: {
        id: 'i-1',
        instanceKey: 'greet_cn',
        pluginName: 'greet_cn',
        templeteKey: 'dynamic_greet',
        title: '中文问候',
        config: '{"Prefix":"你好"}',
      },
    })

    expect(await screen.findByText('编辑实例')).toBeInTheDocument()
    expect(screen.getByLabelText('实例 Key')).toHaveAttribute('disabled')

    await user.click(screen.getByRole('button', { name: /保 存|保存/ }))

    await waitFor(() => {
      expect(pluginApi.saveDynamicPlugin).toHaveBeenCalledWith(
        expect.objectContaining({ pluginKey: 'greet_cn', templeteKey: 'dynamic_greet', title: '中文问候' }),
      )
    })
  })

  it('团队 scope 提交走团队保存接口', async () => {
    const user = userEvent.setup()
    renderModal({ scope: 'team', teamId: 7, presetTemplateKey: 'dynamic_greet' })

    await user.type(screen.getByLabelText('实例 Key'), 'team_greet')
    await user.type(screen.getByLabelText('插件标题'), '团队问候')
    await user.click(screen.getByRole('button', { name: /保 存|保存/ }))

    await waitFor(() => {
      expect(saveTeamDynamicPlugin).toHaveBeenCalledWith(
        expect.objectContaining({ teamId: 7, instanceKey: 'team_greet', templeteKey: 'dynamic_greet' }),
      )
    })
    expect(pluginApi.saveDynamicPlugin).not.toHaveBeenCalled()
  })
})
