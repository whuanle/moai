import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { ForgotPassword } from '../ForgotPassword'
import { getServerInfo } from '@/api/auth'
import { useAppStore, type ServerInfo } from '@/store/app'

vi.mock('@/api/auth', () => ({
  getServerInfo: vi.fn().mockResolvedValue(null),
  sendEmailCode: vi.fn(),
  resetPasswordByEmail: vi.fn(),
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

function renderForgotPassword() {
  return render(
    <MemoryRouter>
      <ForgotPassword />
    </MemoryRouter>,
  )
}

describe('ForgotPassword（找回密码页）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getServerInfo).mockResolvedValue(null as unknown as ServerInfo)
    document.title = ''
  })

  it('标题与浏览器标签页同步', async () => {
    useAppStore.setState({ serverInfo: serverInfo() })
    renderForgotPassword()

    expect(await screen.findByRole('heading', { name: '忘记密码' })).toBeInTheDocument()
    await waitFor(() => expect(document.title).toBe('MoAI'))
  })

  it('未启用邮件服务时提示联系管理员且发送按钮不可用', async () => {
    vi.mocked(getServerInfo).mockResolvedValue(serverInfo({ passwordResetEnabled: false }))
    renderForgotPassword()

    expect(await screen.findByText('未启用邮件服务，无法自助找回密码，请联系管理员重置')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('button', { name: '发送验证码' })).toBeDisabled())
    expect(screen.getByRole('button', { name: '重置密码' })).toBeDisabled()
  })

  it('启用邮件服务后展示完整重置表单', async () => {
    vi.mocked(getServerInfo).mockResolvedValue(serverInfo({ passwordResetEnabled: true }))
    renderForgotPassword()

    expect(await screen.findByRole('heading', { name: '忘记密码' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('button', { name: '发送验证码' })).toBeEnabled())
    expect(screen.getByRole('button', { name: '重置密码' })).toBeEnabled()
    expect(screen.getByLabelText('新密码')).toBeInTheDocument()
    expect(screen.getByLabelText('确认新密码')).toBeInTheDocument()
  })
})
