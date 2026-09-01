import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Users } from '../Users'
import { useAppStore } from '@/store/app'
import { getUsers } from '@/api/usermanage'

vi.mock('@/api/usermanage', () => ({
  getUsers: vi.fn().mockResolvedValue({
    totalCount: 2,
    items: [
      {
        id: 1,
        userName: 'admin',
        nickName: 'admin',
        email: 'admin@admin.com',
        isAdmin: true,
        isRoot: true,
        isDisable: false,
        createTime: '2026-09-01T00:00:00Z',
      },
      {
        id: 2,
        userName: 'bob',
        nickName: 'Bob',
        email: 'bob@moai.com',
        isAdmin: false,
        isRoot: false,
        isDisable: false,
        createTime: '2026-09-01T00:00:00Z',
      },
    ],
  }),
  getUserDetail: vi.fn(),
  setUserAdmin: vi.fn().mockResolvedValue(undefined),
  setUserDisable: vi.fn().mockResolvedValue(undefined),
  resetUserPassword: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/auth', () => ({
  refreshUserProfile: vi.fn().mockResolvedValue(null),
  getServerInfo: vi.fn().mockResolvedValue({ rsaPublic: 'test', serviceUrl: '', publicStoreUrl: '' }),
}))

function renderUsers() {
  return render(
    <MemoryRouter>
      <Users />
    </MemoryRouter>,
  )
}

describe('Users', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: {
        accessToken: 'token',
        userId: '1',
        userName: 'admin',
        isAdmin: true,
        isRoot: true,
      },
    })
  })

  it('root 看到用户列表和授权管理员操作', async () => {
    renderUsers()

    await waitFor(() => {
      expect(getUsers).toHaveBeenCalled()
    })
    expect(await screen.findByText('bob')).toBeInTheDocument()
    // root 行自身的危险操作应禁用
    const rows = document.querySelectorAll('table tr')
    expect(rows.length).toBeGreaterThan(1)
  })

  it('root 自己所在行不渲染危险操作，普通用户行渲染完整操作', async () => {
    renderUsers()
    expect(await screen.findByText('bob')).toBeInTheDocument()

    const findRow = (text: string) =>
      Array.from(document.querySelectorAll('table tr')).find((r) => r.textContent?.includes(text))

    const adminRowButtons = Array.from(findRow('admin')?.querySelectorAll('button') ?? []).map(
      (b) => b.textContent,
    )
    const bobRowButtons = Array.from(findRow('bob')?.querySelectorAll('button') ?? []).map(
      (b) => b.textContent,
    )

    // root 自己的行只有"查看"
    expect(adminRowButtons).toContain('查看')
    expect(adminRowButtons).not.toContain('设为管理员')
    expect(adminRowButtons).not.toContain('禁用')
    expect(adminRowButtons).not.toContain('重置密码')

    // bob 行有完整操作
    expect(bobRowButtons).toContain('设为管理员')
    expect(bobRowButtons).toContain('禁用')
    expect(bobRowButtons).toContain('重置密码')
  })

  it('非管理员访问重定向到 dashboard', () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '2', isAdmin: false } })
    renderUsers()
    // Navigate 触发后不再渲染表格
    expect(document.querySelector('table')).toBeNull()
  })
})
