import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { App as AntdApp } from 'antd'
import '@/i18n'
import { OAuthLogin } from '../OAuthLogin'
import { applyLoginResponse, oauthLogin, oauthRegister } from '@/api/auth'
import { PENDING_REDIRECT_STORAGE_KEY } from '@/utils/oauth'

vi.mock('@/api/auth', () => ({
  applyLoginResponse: vi.fn(),
  oauthLogin: vi.fn(),
  oauthRegister: vi.fn(),
}))

vi.mock('@/api/account', () => ({
  oauthBindByCode: vi.fn(),
}))

function renderAt(path: string) {
  return render(
    // 组件内 useFeedback 依赖 antd App 上下文，测试需包一层 App
    <AntdApp>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/oauth/login" element={<OAuthLogin />} />
          <Route path="/apps" element={<div data-testid="apps-page">apps</div>} />
          <Route path="/sso/authorize" element={<div data-testid="sso-authorize">sso</div>} />
          <Route path="/login" element={<div data-testid="login-page">login</div>} />
        </Routes>
      </MemoryRouter>
    </AntdApp>,
  )
}

const boundLoginResponse = {
  isBindUser: true,
  loginCommandResponse: { accessToken: 'token', userId: '1', userName: 'u' },
  tempOAuthBindId: undefined,
  name: '张三',
}

describe('OAuthLogin（第三方登录回调页）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    sessionStorage.clear()
    vi.mocked(applyLoginResponse).mockReturnValue({ accessToken: 'token' } as never)
  })

  it('登录成功且无暂存回跳目标时回应用广场', async () => {
    vi.mocked(oauthLogin).mockResolvedValue(boundLoginResponse as never)
    renderAt('/oauth/login?code=c1&state=guid-1')

    await waitFor(() => expect(screen.getByTestId('apps-page')).toBeTruthy())
    expect(applyLoginResponse).toHaveBeenCalled()
  })

  it('登录成功消费暂存回跳目标，回到子系统 SSO 跳转页并清除暂存', async () => {
    sessionStorage.setItem(PENDING_REDIRECT_STORAGE_KEY, '/sso/authorize?client=report&state=st1')
    vi.mocked(oauthLogin).mockResolvedValue(boundLoginResponse as never)
    renderAt('/oauth/login?code=c1&state=guid-1')

    await waitFor(() => expect(screen.getByTestId('sso-authorize')).toBeTruthy())
    expect(screen.queryByTestId('apps-page')).toBeNull()
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBeNull()
  })

  it('暂存目标不安全（协议相对地址）时回应用广场', async () => {
    sessionStorage.setItem(PENDING_REDIRECT_STORAGE_KEY, '//evil.local')
    vi.mocked(oauthLogin).mockResolvedValue(boundLoginResponse as never)
    renderAt('/oauth/login?code=c1&state=guid-1')

    await waitFor(() => expect(screen.getByTestId('apps-page')).toBeTruthy())
    // 消费即清除，不残留脏数据
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBeNull()
  })

  it('首次登录进入注册确认，注册成功同样消费暂存回跳目标', async () => {
    sessionStorage.setItem(PENDING_REDIRECT_STORAGE_KEY, '/sso/authorize?client=report')
    vi.mocked(oauthLogin).mockResolvedValue({ isBindUser: false, tempOAuthBindId: 't1', name: '张三' } as never)
    vi.mocked(oauthRegister).mockResolvedValue({ accessToken: 'token' } as never)
    renderAt('/oauth/login?code=c1&state=guid-1')

    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: '注册并登录' }))
    await waitFor(() => expect(screen.getByTestId('sso-authorize')).toBeTruthy())
    expect(oauthRegister).toHaveBeenCalledWith('t1')
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBeNull()
  })

  it('登录失败回登录页，不清空后续登录可用的暂存目标', async () => {
    sessionStorage.setItem(PENDING_REDIRECT_STORAGE_KEY, '/sso/authorize?client=report')
    vi.mocked(oauthLogin).mockRejectedValue(new Error('boom'))
    renderAt('/oauth/login?code=c1&state=guid-1')

    await waitFor(() => expect(screen.getByTestId('login-page')).toBeTruthy())
    expect(sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)).toBe('/sso/authorize?client=report')
  })
})
