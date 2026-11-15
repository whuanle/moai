import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import '@/i18n'
import { SubsystemPortal } from '../SubsystemPortal'
import { getAvailableSubsystems } from '@/api/subsystem'

vi.mock('@/api/subsystem', () => ({
  getAvailableSubsystems: vi.fn(),
}))

/** 委托 SSO 跳转页占位：回显查询串，用于断言进入时携带的 appid */
function SsoProbe() {
  const location = useLocation()
  return <div data-testid="sso-page">{location.search}</div>
}

function renderAt() {
  return render(
    <MemoryRouter initialEntries={['/subsystems']}>
      <Routes>
        <Route path="/subsystems" element={<SubsystemPortal />} />
        <Route path="/sso/authorize" element={<SsoProbe />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('SubsystemPortal（子系统门户）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('展示当前用户可进入的子系统（名称/描述/进入按钮）', async () => {
    vi.mocked(getAvailableSubsystems).mockResolvedValue([
      { subsystemId: '01924f5e-0000-7000-8000-0000000000f1', key: 'report', name: '智能报表', description: '经营看板', avatarPath: '' },
    ])

    renderAt()

    expect(await screen.findByText('智能报表')).toBeTruthy()
    expect(screen.getByText('经营看板')).toBeTruthy()
    expect(screen.getByRole('button', { name: /进\s*入/ })).toBeTruthy()
  })

  it('无可进入子系统时展示空状态与权限说明', async () => {
    vi.mocked(getAvailableSubsystems).mockResolvedValue([])

    renderAt()

    expect(await screen.findByText('暂无可进入的子系统')).toBeTruthy()
    expect(screen.getByText(/管理员在「应用接入」中启用子系统/)).toBeTruthy()
  })

  it('点击进入跳转委托 SSO 页并携带子系统 appid', async () => {
    const user = userEvent.setup()
    vi.mocked(getAvailableSubsystems).mockResolvedValue([{ key: 'report', name: '智能报表' }])

    renderAt()
    await user.click(await screen.findByRole('button', { name: /进\s*入/ }))

    expect((await screen.findByTestId('sso-page')).textContent).toContain('client=report')
  })

  it('加载失败时保持空状态（错误由全局中间件提示）', async () => {
    vi.mocked(getAvailableSubsystems).mockRejectedValue(new Error('500'))

    renderAt()

    expect(await screen.findByText('暂无可进入的子系统')).toBeTruthy()
  })
})
