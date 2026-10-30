import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import '@/i18n'
import { buildCallbackRedirect, SsoAuthorize } from '../SsoAuthorize'
import { subsystemSsoAuthorizeCode } from '@/api/subsystem'
import { useAppStore } from '@/store/app'

vi.mock('@/api/subsystem', () => ({
  subsystemSsoAuthorizeCode: vi.fn(),
}))

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/sso/authorize" element={<SsoAuthorize />} />
        <Route path="/login" element={<div data-testid="login-page">login</div>} />
        <Route path="/apps" element={<div data-testid="apps-page">apps</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('buildCallbackRedirect（回跳地址拼装）', () => {
  it('追加 code 与 state（回调已带查询参数时用 & 拼接）', () => {
    expect(buildCallbackRedirect('https://report.local/cb?ts=1', 'abc', 'st1'))
      .toBe('https://report.local/cb?ts=1&code=abc&state=st1')
  })

  it('无 state 时仅追加 code', () => {
    expect(buildCallbackRedirect('https://report.local/cb', 'abc'))
      .toBe('https://report.local/cb?code=abc')
  })
})

describe('SsoAuthorize（子系统委托 SSO 跳转页）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('未登录时跳转登录页（登录后可回跳）', () => {
    useAppStore.setState({ userInfo: undefined })
    renderAt('/sso/authorize?client=report&state=st1')
    expect(screen.getByTestId('login-page')).toBeTruthy()
  })

  it('缺少 client 参数时展示错误', () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '1', userName: 'u' } as never })
    renderAt('/sso/authorize')
    expect(screen.getByText('缺少子系统标识（client 参数），无法发起委托登录。')).toBeTruthy()
  })

  it('登录态下签发委托码并进入跳转中状态', async () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '1', userName: 'u' } as never })
    vi.mocked(subsystemSsoAuthorizeCode).mockResolvedValue({
      code: 'abc123',
      callbackUrl: 'https://report.local/cb',
      name: '智能报表',
    })
    renderAt('/sso/authorize?client=report&state=st1')

    await waitFor(() => expect(subsystemSsoAuthorizeCode).toHaveBeenCalledWith('report', 'st1'))
    expect(await screen.findByText('授权成功，正在跳转到 智能报表…')).toBeTruthy()
  })

  it('签发失败时展示错误与返回入口', async () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '1', userName: 'u' } as never })
    vi.mocked(subsystemSsoAuthorizeCode).mockRejectedValue(new Error('403'))
    renderAt('/sso/authorize?client=report')

    expect(await screen.findByText('无法完成委托登录')).toBeTruthy()
    expect(screen.getByText(/委托登录失败/)).toBeTruthy()
  })
})
