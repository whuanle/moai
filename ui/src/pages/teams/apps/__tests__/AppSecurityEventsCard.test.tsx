import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import '@/i18n'
import { AppSecurityEventsCard } from '../AppSecurityEventsCard'
import { getAppSecurityEvents } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppSecurityEvents: vi.fn(),
}))

describe('AppSecurityEventsCard（安全事件审计）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppSecurityEvents).mockResolvedValue({ items: [], total: 0, pageNo: 1, pageSize: 10 })
  })

  it('展示规则命中记录（时间/作用域/规则/动作/次数），按当前页刷新', async () => {
    vi.mocked(getAppSecurityEvents).mockResolvedValue({
      items: [
        {
          id: 'e1',
          sessionId: 's1',
          userId: '1',
          scope: 'toolResult',
          ruleName: '',
          ruleType: 'bankCard',
          strategy: 'block',
          action: 'blocked',
          hitCount: 3,
          createTime: '2026-10-13T08:30:00+08:00',
        },
      ],
      total: 1,
      pageNo: 1,
      pageSize: 10,
    })

    // 组件为「日志」分区「安全事件」Tab 内容：表格直接渲染，标题由 Tab 标签承载
    render(<AppSecurityEventsCard appId="a1" canManage />)

    expect(await screen.findByText('工具结果')).toBeTruthy()
    expect(screen.getByText('已拦截')).toBeTruthy()
    expect(screen.getByText('银行卡号')).toBeTruthy()
    expect(screen.getByText('3')).toBeTruthy()
    expect(getAppSecurityEvents).toHaveBeenCalledWith('a1', 1, 10)
  })

  it('非管理员不渲染且不加载事件', () => {
    render(<AppSecurityEventsCard appId="a1" canManage={false} />)

    expect(screen.queryByText('已拦截')).toBeNull()
    expect(getAppSecurityEvents).not.toHaveBeenCalled()
  })
})
