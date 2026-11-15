import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { LoginSafety } from '../LoginSafety'
import { useAppStore } from '@/store/app'
import { useBadgeStore } from '@/store/badges'
import { getRiskEventUnreadCount } from '@/api/loginsafety'

vi.mock('@/api/loginsafety', () => ({
  getLoginLogs: vi.fn().mockResolvedValue({ total: 0, items: [] }),
  getRiskEvents: vi.fn().mockResolvedValue({ total: 0, items: [] }),
  getRiskEventUnreadCount: vi.fn().mockResolvedValue(0),
  markRiskEventsRead: vi.fn().mockResolvedValue(undefined),
  getIpBans: vi.fn().mockResolvedValue({ total: 0, items: [] }),
  liftIpBan: vi.fn().mockResolvedValue(undefined),
  addIpBan: vi.fn().mockResolvedValue(undefined),
}))

function renderPage(entry = '/admin/loginsafety') {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <LoginSafety />
    </MemoryRouter>,
  )
}

describe('LoginSafety', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useBadgeStore.setState({ counts: {} })
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

  it('风险事件 Tab 显示待阅读数量角标', async () => {
    vi.mocked(getRiskEventUnreadCount).mockResolvedValue(3)

    renderPage()

    await waitFor(() => {
      expect(getRiskEventUnreadCount).toHaveBeenCalled()
    })
    // 角标数字来自未读计数接口
    expect(await screen.findByText('3')).toBeInTheDocument()
    expect(useBadgeStore.getState().counts.logEvents).toBe(3)
  })

  it('未读为 0 时不渲染角标', async () => {
    vi.mocked(getRiskEventUnreadCount).mockResolvedValue(0)

    renderPage()

    await waitFor(() => {
      expect(getRiskEventUnreadCount).toHaveBeenCalled()
    })
    expect(document.querySelector('.ant-badge-count')).toBeNull()
  })

  it('非管理员重定向离开页面', () => {
    useAppStore.setState({
      userInfo: {
        accessToken: 'token',
        userId: '2',
        userName: 'bob',
        isAdmin: false,
        isRoot: false,
      },
    })

    renderPage()

    expect(document.querySelector('.ant-tabs')).toBeNull()
  })
})
