import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppAccessSection } from '../AppAccessSection'
import { getAppAccessPoint, saveAppAccessPoint } from '@/api/accessPoint'

vi.mock('@/api/accessPoint', () => ({
  getAppAccessPoint: vi.fn(),
  saveAppAccessPoint: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/utils/storage', () => ({
  uploadImageWithKey: vi.fn(),
  resolveStorageUrl: vi.fn((v: string) => v),
}))

const CONFIG = {
  appId: 'a1',
  title: null,
  subtitle: null,
  placeholder: null,
  primaryColor: null,
  position: 'bottomRight',
  displayMode: 'floating',
  launcherText: null,
  avatar: null,
  panelWidth: 380,
  panelHeight: 560,
  defaultOpen: false,
  enabled: true,
}

function renderSection(isExternal = true, canManage = true) {
  return render(<AppAccessSection appId="a1" isExternal={isExternal} canManage={canManage} userRole={2} />)
}

beforeEach(() => {
  vi.mocked(getAppAccessPoint).mockResolvedValue({ ...CONFIG })
  vi.mocked(saveAppAccessPoint).mockResolvedValue(undefined)
})

describe('AppAccessSection', () => {
  it('外部应用：加载配置并渲染表单与对话端点、嵌入代码', async () => {
    renderSection(true)
    expect(await screen.findByText('访问点配置')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByDisplayValue('380')).toBeInTheDocument())
    expect(screen.getByText(/\/api\/teamres\/agent\/a1\/chat/)).toBeInTheDocument()
    // 显示形式默认悬浮，且悬浮位置字段可见
    expect(screen.getByText('悬浮组件（右下/左下角）')).toBeInTheDocument()
    expect(screen.getByText('悬浮位置')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText(/data-app-id="a1"/)).toBeInTheDocument())
    // is_auth 已退役：不再出现 data-key 快速验证片段
    expect(screen.queryByText(/data-key=/)).not.toBeInTheDocument()
  })

  it('内部应用：同样加载配置，并展示身份穿透接入信息（颁发端点 + AG-UI 直连 + tokenProvider 组件）', async () => {
    renderSection(false)
    expect(await screen.findByText('访问点配置')).toBeInTheDocument()
    expect(getAppAccessPoint).toHaveBeenCalled()
    await waitFor(() => expect(screen.getByDisplayValue('380')).toBeInTheDocument())
    // 接入信息面板：颁发端点 + 未绑定 404 指引
    expect(screen.getByText('接入信息（内部应用身份穿透）')).toBeInTheDocument()
    await waitFor(() => expect(screen.getAllByText(/\/api\/subsys\/system\/app-tokens/).length).toBeGreaterThan(0))
    expect(screen.getByText(/第三方身份未绑定用户时返回 404/)).toBeInTheDocument()
    // AG-UI 直连片段 + 组件接入片段
    await waitFor(() => expect(screen.getByText('AG-UI 直连（免组件）')).toBeInTheDocument())
    expect(screen.getByText('组件接入（tokenProvider）')).toBeInTheDocument()
    expect(screen.getAllByText(/oauthProviderId/).length).toBeGreaterThan(0)
    // 内部应用不展示外部应用的悬浮组件嵌入片段
    expect(screen.queryByText(/data-app-id="a1"/)).not.toBeInTheDocument()
  })

  it('切到内嵌小窗后：位置字段隐藏，嵌入片段带占位容器与 data-target', async () => {
    renderSection()
    await waitFor(() => expect(screen.getByDisplayValue('380')).toBeInTheDocument())
    // antd Select：点开显示形式下拉并选择内嵌小窗
    fireEvent.mouseDown(screen.getByText('悬浮组件（右下/左下角）').closest('.ant-select-selector')!)
    expect(await screen.findByText('内嵌小窗（占位容器嵌入页面）')).toBeInTheDocument()
    fireEvent.click(screen.getByText('内嵌小窗（占位容器嵌入页面）'))
    // 悬浮位置字段隐藏
    expect(screen.queryByText('悬浮位置')).not.toBeInTheDocument()
    // 嵌入片段带占位容器 + inline 属性
    await waitFor(() => expect(screen.getByText(/id="moai-chat"/)).toBeInTheDocument())
    expect(screen.getByText(/data-mode="inline"/)).toBeInTheDocument()
    expect(screen.getByText(/data-target="#moai-chat"/)).toBeInTheDocument()
  })

  it('保存访问点配置', async () => {
    renderSection()
    await waitFor(() => expect(screen.getByDisplayValue('380')).toBeInTheDocument())
    fireEvent(await screen.findByRole('button', { name: '保存配置' }), new MouseEvent('click', { bubbles: true }))
    await waitFor(() => expect(saveAppAccessPoint).toHaveBeenCalledWith('a1', expect.objectContaining({ panelWidth: 380, panelHeight: 560, displayMode: 'floating', enabled: true })))
  })
})
