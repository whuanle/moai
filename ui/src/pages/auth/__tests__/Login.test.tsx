import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { Login } from '../Login'
import { useAppStore } from '@/store/app'

vi.mock('@/api/auth', () => ({
  getOAuthProviders: vi.fn().mockResolvedValue([]),
  login: vi.fn(),
  getServerInfo: vi.fn().mockResolvedValue(null),
}))

function setServerInfo(name: string) {
  useAppStore.setState({
    serverInfo: {
      serviceUrl: 'http://127.0.0.1:5000',
      publicStoreUrl: '',
      rsaPublic: '',
      logoPath: '',
      loginBackgroundPath: '',
      name,
      registerEmailCodeRequired: false,
      passwordResetEnabled: false,
    },
  })
}

function renderLogin() {
  return render(
    <MemoryRouter>
      <Login />
    </MemoryRouter>,
  )
}

describe('Login（登录页）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    document.title = ''
  })

  it('主标题展示自定义网站名称并同步浏览器标签页标题', async () => {
    setServerInfo('自定义站名')
    renderLogin()

    expect(await screen.findByRole('heading', { name: '自定义站名' })).toBeInTheDocument()
    await waitFor(() => expect(document.title).toBe('自定义站名'))
  })

  it('未设置网站名称时回退默认名称 MoAI', async () => {
    setServerInfo('')
    renderLogin()

    expect(await screen.findByRole('heading', { name: 'MoAI' })).toBeInTheDocument()
  })
})
