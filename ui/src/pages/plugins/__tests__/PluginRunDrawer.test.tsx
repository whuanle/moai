import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PluginRunDrawer } from '../components/PluginRunDrawer'
import { pluginApi } from '@/api/plugin'

vi.mock('@/api/plugin', () => ({
  pluginApi: {
    getManagePlugins: vi.fn(),
    runPlugin: vi.fn(),
    saveStaticPlugin: vi.fn(),
  },
}))

describe('PluginRunDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('运行按钮调用 runPlugin 并展示结果', async () => {
    const user = userEvent.setup()
    vi.mocked(pluginApi.runPlugin).mockResolvedValue({
      key: 'static_echo',
      success: true,
      dataJson: '{"msg":"hi"}',
      errorEscaped: null,
      responseType: null,
    })
    render(
      <PluginRunDrawer
        open
        pluginKey="static_echo"
        paramsExample={'{"Message":"hi"}'}
        onClose={() => {}}
      />,
    )

    const runBtn = await screen.findByRole('button', { name: /运行|run/i })
    await user.click(runBtn)

    await waitFor(() => {
      expect(pluginApi.runPlugin).toHaveBeenCalledTimes(1)
      const call = vi.mocked(pluginApi.runPlugin).mock.calls[0][0]
      expect(call.key).toBe('static_echo')
      expect(JSON.parse(call.requestJson)).toEqual({ Message: 'hi' })
    })
  })

  it('运行失败时展示错误信息', async () => {
    const user = userEvent.setup()
    vi.mocked(pluginApi.runPlugin).mockResolvedValue({
      key: 'static_echo',
      success: false,
      dataJson: null,
      errorEscaped: '插件执行失败',
      responseType: null,
    })
    render(
      <PluginRunDrawer open pluginKey="static_echo" paramsExample={'{}'} onClose={() => {}} />,
    )

    const runBtn = await screen.findByRole('button', { name: /运行|run/i })
    await user.click(runBtn)

    await waitFor(() => {
      expect(pluginApi.runPlugin).toHaveBeenCalled()
    })
  })

  it('未提供 pluginKey 时不发起运行', async () => {
    const user = userEvent.setup()
    render(<PluginRunDrawer open pluginKey={null} paramsExample={'{}'} onClose={() => {}} />)

    const runBtn = await screen.findByRole('button', { name: /运行|run/i })
    await user.click(runBtn)

    expect(pluginApi.runPlugin).not.toHaveBeenCalled()
  })
})
