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
  launcherText: null,
  avatar: null,
  panelWidth: 380,
  panelHeight: 560,
  defaultOpen: false,
  enabled: true,
}

function renderSection(isExternal = true, isAuth = false, canManage = true) {
  return render(<AppAccessSection appId="a1" isExternal={isExternal} isAuth={isAuth} canManage={canManage} userRole={2} />)
}

beforeEach(() => {
  vi.mocked(getAppAccessPoint).mockResolvedValue({ ...CONFIG })
  vi.mocked(saveAppAccessPoint).mockResolvedValue(undefined)
})

describe('AppAccessSection', () => {
  it('内部应用显示提示且不加载配置', async () => {
    renderSection(false)
    expect(await screen.findByText('只有外部应用可以配置访问点')).toBeInTheDocument()
    expect(getAppAccessPoint).not.toHaveBeenCalled()
  })

  it('加载默认配置并渲染表单与端点信息', async () => {
    renderSection()
    expect(await screen.findByText('访问点配置')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByDisplayValue('380')).toBeInTheDocument())
    expect(screen.getByText(/\/api\/external\/agent\/a1\/chat/)).toBeInTheDocument()
  })

  it('嵌入代码片段包含 appId 与 server 地址', async () => {
    renderSection()
    await screen.findByText('访问点配置')
    await waitFor(() => expect(screen.getByText(/data-app-id="a1"/)).toBeInTheDocument())
    expect(screen.queryByText(/data-key=/)).not.toBeInTheDocument()
  })

  it('需授权应用时嵌入代码提示 data-key', async () => {
    renderSection(true, true)
    await screen.findByText('访问点配置')
    await waitFor(() => expect(screen.getByText(/data-key="&lt;应用接入key&gt;"|data-key="/)).toBeInTheDocument())
  })

  it('保存访问点配置', async () => {
    renderSection()
    await waitFor(() => expect(screen.getByDisplayValue('380')).toBeInTheDocument())
    fireEvent(await screen.findByRole('button', { name: '保存配置' }), new MouseEvent('click', { bubbles: true }))
    await waitFor(() => expect(saveAppAccessPoint).toHaveBeenCalledWith('a1', expect.objectContaining({ panelWidth: 380, panelHeight: 560, enabled: true })))
  })
})
