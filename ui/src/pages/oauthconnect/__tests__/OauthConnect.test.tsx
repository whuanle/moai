import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { OauthConnect } from '../OauthConnect'
import { useAppStore } from '@/store/app'
import { getOAuthConnections } from '@/api/oauthconnect'

vi.mock('@/api/oauthconnect', () => ({
  getOAuthConnections: vi.fn().mockResolvedValue({
    redirectUri: 'http://127.0.0.1:4000/oauth_login',
    items: [
      {
        id: '1',
        name: '飞书登录',
        provider: 'feishu',
        key: 'cli_a8ec83ec',
        iconUrl: '',
        createTime: '2026-09-01T14:26:00Z',
        createUserName: 'admin',
        updateTime: '2026-09-01T14:26:00Z',
        updateUserName: 'admin',
      },
      {
        id: '2',
        name: 'casdoor',
        provider: 'custom',
        key: '4d29ccea',
        iconUrl: '',
        wellKnown: 'https://casdoor.example.com/.well-known/openid-configuration',
        createTime: '2026-09-02T09:53:00Z',
        createUserName: 'admin',
        updateTime: '2026-10-27T10:13:00Z',
        updateUserName: 'admin',
      },
    ],
  }),
  createOAuthConnection: vi.fn().mockResolvedValue(undefined),
  updateOAuthConnection: vi.fn().mockResolvedValue(undefined),
  deleteOAuthConnection: vi.fn().mockResolvedValue(undefined),
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <OauthConnect />
    </MemoryRouter>,
  )
}

describe('OauthConnect', () => {
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

  it('管理员看到连接器列表与行内操作', async () => {
    renderPage()

    await waitFor(() => {
      expect(getOAuthConnections).toHaveBeenCalled()
    })
    expect(await screen.findByText('飞书登录')).toBeInTheDocument()
    expect(screen.getByText('casdoor')).toBeInTheDocument()
    // 每行展示回调地址而非授权端点
    expect(screen.getAllByText('http://127.0.0.1:4000/oauth_login').length).toBeGreaterThan(0)

    const findRow = (text: string) =>
      Array.from(document.querySelectorAll('table tr')).find((r) => r.textContent?.includes(text))

    const rowButtons = (text: string) =>
      Array.from(findRow(text)?.querySelectorAll('button') ?? [])
        .map((b) => b.getAttribute('aria-label'))
        .filter(Boolean)

    // 回调地址带复制按钮（aria-label=Copy），行内操作为查看/编辑/删除
    expect(rowButtons('casdoor')).toEqual(expect.arrayContaining(['查看', '编辑', '删除']))
  })

  it('关键字按名称/应用 Key 过滤列表', async () => {
    renderPage()
    expect(await screen.findByText('casdoor')).toBeInTheDocument()

    fireEvent.change(screen.getByPlaceholderText('搜索名称或应用 Key'), {
      target: { value: 'casdoor' },
    })
    fireEvent.click(screen.getByRole('button', { name: /查\s*询/ }))

    await waitFor(() => {
      expect(screen.queryByText('飞书登录')).not.toBeInTheDocument()
    })
    expect(screen.getByText('casdoor')).toBeInTheDocument()
  })

  it('查看打开详情弹窗展示连接信息', async () => {
    renderPage()
    expect(await screen.findByText('casdoor')).toBeInTheDocument()

    // 点击 casdoor 行内的"查看"
    const row = Array.from(document.querySelectorAll('table tr')).find((r) =>
      r.textContent?.includes('casdoor'),
    )
    const viewButton = Array.from(row?.querySelectorAll('button') ?? []).find(
      (b) => b.getAttribute('aria-label') === '查看',
    )
    expect(viewButton).toBeTruthy()
    fireEvent.click(viewButton!)

    // 详情弹窗展示应用 Key、发现端点与审计字段
    const modal = (await screen.findByText('连接详情')).closest('.ant-modal')
    expect(modal?.textContent).toContain('4d29ccea')
    expect(modal?.textContent).toContain('https://casdoor.example.com/.well-known/openid-configuration')
  })

  it('非管理员访问重定向到应用广场', () => {
    useAppStore.setState({ userInfo: { accessToken: 'token', userId: '2', isAdmin: false } })
    renderPage()
    expect(document.querySelector('table')).toBeNull()
  })
})
