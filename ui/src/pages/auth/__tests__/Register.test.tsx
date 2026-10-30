import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { Register } from '../Register'
import { getServerInfo } from '@/api/auth'
import { useAppStore, type ServerInfo } from '@/store/app'

vi.mock('@/api/auth', () => ({
  register: vi.fn(),
  getServerInfo: vi.fn().mockResolvedValue(null),
  sendEmailCode: vi.fn(),
}))

function serverInfo(overrides: Partial<ServerInfo> = {}): ServerInfo {
  return {
    serviceUrl: 'http://127.0.0.1:5000',
    publicStoreUrl: '',
    rsaPublic: '',
    logoPath: '',
    loginBackgroundPath: '',
    name: 'MoAI',
    registerEmailCodeRequired: false,
    passwordResetEnabled: false,
    ...overrides,
  }
}

function setServerInfo(name: string) {
  useAppStore.setState({ serverInfo: serverInfo({ name }) })
}

function renderRegister() {
  return render(
    <MemoryRouter>
      <Register />
    </MemoryRouter>,
  )
}

describe('Register（注册页）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getServerInfo).mockResolvedValue(null as unknown as ServerInfo)
    document.title = ''
  })

  it('主标题展示自定义网站名称并同步浏览器标签页标题', async () => {
    setServerInfo('自定义站名')
    renderRegister()

    expect(await screen.findByRole('heading', { name: '自定义站名' })).toBeInTheDocument()
    await waitFor(() => expect(document.title).toBe('自定义站名'))
  })

  it('未设置网站名称时回退默认名称 MoAI', async () => {
    setServerInfo('')
    renderRegister()

    expect(await screen.findByRole('heading', { name: 'MoAI' })).toBeInTheDocument()
  })

  it('未开启注册验证码时不展示验证码输入与发送按钮', async () => {
    vi.mocked(getServerInfo).mockResolvedValue(serverInfo({ registerEmailCodeRequired: false }))
    renderRegister()

    await screen.findByRole('heading', { name: 'MoAI' })
    await waitFor(() => expect(vi.mocked(getServerInfo)).toHaveBeenCalled())
    expect(screen.queryByText('邮箱验证码')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '发送验证码' })).not.toBeInTheDocument()
  })

  it('开启注册验证码时展示验证码输入与发送按钮', async () => {
    vi.mocked(getServerInfo).mockResolvedValue(serverInfo({ registerEmailCodeRequired: true }))
    renderRegister()

    expect(await screen.findByText('邮箱验证码')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '发送验证码' })).toBeInTheDocument()
  })
})
