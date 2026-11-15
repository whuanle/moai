import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import '@/i18n'
import { buildCallbackRedirect, SsoAuthorize } from '../SsoAuthorize'
import { subsystemSsoAuthorizeCode, subsystemSsoCheck } from '@/api/subsystem'
import { useAppStore } from '@/store/app'

vi.mock('@/api/subsystem', () => ({
  subsystemSsoAuthorizeCode: vi.fn(),
  subsystemSsoCheck: vi.fn(),
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

const signedIn = { userInfo: { accessToken: 'token', userId: '1', userName: 'u' } as never }

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
    useAppStore.setState({ userInfo: undefined })
  })

  it('缺少 client 参数时展示错误', () => {
    useAppStore.setState(signedIn)
    renderAt('/sso/authorize')
    expect(screen.getByText('缺少子系统标识（client 参数），无法发起委托登录。')).toBeTruthy()
  })

  it('预检要求登录时跳转登录页（登录后可回跳）', async () => {
    vi.mocked(subsystemSsoCheck).mockResolvedValue({ allowed: true, requiresLogin: true, name: '智能报表' })
    renderAt('/sso/authorize?client=report&state=st1')
    expect(await screen.findByTestId('login-page')).toBeTruthy()
    expect(subsystemSsoCheck).toHaveBeenCalledWith('report', undefined)
    expect(subsystemSsoAuthorizeCode).not.toHaveBeenCalled()
  })

  it('预检判定不可用时停在本页提示原因，不进入登录流程', async () => {
    vi.mocked(subsystemSsoCheck).mockResolvedValue({ allowed: false, reason: 'unavailable' })
    renderAt('/sso/authorize?client=report')

    expect(await screen.findByText('该子系统当前不可用：可能不存在、已停用或未开放登录。')).toBeTruthy()
    expect(screen.queryByTestId('login-page')).toBeNull()
    expect(subsystemSsoAuthorizeCode).not.toHaveBeenCalled()
  })

  it('当前用户未获授权时停在本页提示，不跳登录也不回跳', async () => {
    useAppStore.setState(signedIn)
    vi.mocked(subsystemSsoCheck).mockResolvedValue({ allowed: false, reason: 'not_authorized' })
    renderAt('/sso/authorize?client=report')

    expect(await screen.findByText('你所在的团队或组织未获授权使用该子系统。')).toBeTruthy()
    expect(screen.queryByTestId('login-page')).toBeNull()
    expect(subsystemSsoAuthorizeCode).not.toHaveBeenCalled()
  })

  it('回调地址未注册时提示具体原因', async () => {
    vi.mocked(subsystemSsoCheck).mockResolvedValue({ allowed: false, reason: 'callback_mismatch' })
    renderAt('/sso/authorize?client=report&redirect_uri=https%3A%2F%2Fevil.local%2Fcb')

    expect(await screen.findByText('回跳地址未在子系统注册列表内，无法完成登录。')).toBeTruthy()
  })

  it('本地残留过期 token 被预检判定未登录时清理登录态并跳登录', async () => {
    useAppStore.setState(signedIn)
    vi.mocked(subsystemSsoCheck).mockResolvedValue({ allowed: true, requiresLogin: true })
    renderAt('/sso/authorize?client=report')

    expect(await screen.findByTestId('login-page')).toBeTruthy()
    await waitFor(() => expect(useAppStore.getState().userInfo).toBeFalsy())
  })

  it('预检通过且已授权时签发委托码并进入跳转中状态', async () => {
    useAppStore.setState(signedIn)
    vi.mocked(subsystemSsoCheck).mockResolvedValue({ allowed: true, requiresLogin: false, name: '智能报表' })
    vi.mocked(subsystemSsoAuthorizeCode).mockResolvedValue({
      code: 'abc123',
      callbackUrl: 'https://report.local/cb',
      name: '智能报表',
    })
    renderAt('/sso/authorize?client=report&state=st1')

    await waitFor(() => expect(subsystemSsoAuthorizeCode).toHaveBeenCalledWith('report', 'st1', undefined))
    expect(await screen.findByText('授权成功，正在跳转到 智能报表…')).toBeTruthy()
  })

  it('预检请求失败时展示校验失败提示', async () => {
    vi.mocked(subsystemSsoCheck).mockRejectedValue(new Error('500'))
    renderAt('/sso/authorize?client=report')

    expect(await screen.findByText('无法校验该子系统是否可用，请稍后重试或联系管理员。')).toBeTruthy()
    expect(subsystemSsoAuthorizeCode).not.toHaveBeenCalled()
  })

  it('签发失败时展示错误与返回入口', async () => {
    useAppStore.setState(signedIn)
    vi.mocked(subsystemSsoCheck).mockResolvedValue({ allowed: true, requiresLogin: false })
    vi.mocked(subsystemSsoAuthorizeCode).mockRejectedValue(new Error('403'))
    renderAt('/sso/authorize?client=report')

    expect(await screen.findByText('无法完成委托登录')).toBeTruthy()
    expect(screen.getByText(/委托登录失败/)).toBeTruthy()
  })
})
