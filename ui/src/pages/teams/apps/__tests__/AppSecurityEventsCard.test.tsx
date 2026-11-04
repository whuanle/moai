import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import '@/i18n'
import { AppSecurityEventsCard } from '../AppSecurityEventsCard'
import { getAppSecurityEvents } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppSecurityEvents: vi.fn(),
}))

describe('AppSecurityEventsCard（安全事件审计）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppSecurityEvents).mockResolvedValue({ items: [], total: 0, pageNo: 1, pageSize: 50 })
  })

  it('默认每页 50 条，展示规则命中记录（时间/作用域/规则/动作/次数），动作带语义色标签', async () => {
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
      pageSize: 50,
    })

    // 组件为「日志」分区「安全事件」Tab 内容：查询工具栏 + 表格直接渲染，标题由 Tab 标签承载
    render(<AppSecurityEventsCard appId="a1" canManage />)

    expect(await screen.findByText('工具结果')).toBeTruthy()
    expect(screen.getByText('已拦截')).toBeTruthy()
    expect(screen.getByText('银行卡号')).toBeTruthy()
    expect(screen.getByText('3')).toBeTruthy()
    expect(getAppSecurityEvents).toHaveBeenCalledWith('a1', 1, 50, { keyword: undefined, scope: undefined })
  })

  it('工具栏关键字与作用域筛选：查询按钮触发带过滤条件的重新加载', async () => {
    vi.mocked(getAppSecurityEvents).mockResolvedValue({ items: [], total: 0, pageNo: 1, pageSize: 50 })

    render(<AppSecurityEventsCard appId="a1" canManage />)

    fireEvent.change(await screen.findByPlaceholderText('搜索规则名称'), { target: { value: '手机号' } })
    fireEvent.mouseDown(screen.getAllByRole('combobox')[0])
    const option = await screen.findByText('用户输入')
    fireEvent.click(option)

    fireEvent.click(screen.getByRole('button', { name: /查\s*询/ }))

    await new Promise((resolve) => setTimeout(resolve, 0))
    expect(getAppSecurityEvents).toHaveBeenLastCalledWith('a1', 1, 50, { keyword: '手机号', scope: 'input' })
  })

  it('底部分页切换每页数量生效并回到第 1 页重新加载', async () => {
    vi.mocked(getAppSecurityEvents).mockResolvedValue({
      items: [],
      total: 60,
      pageNo: 1,
      pageSize: 50,
    })

    render(<AppSecurityEventsCard appId="a1" canManage />)
    await screen.findByText('共 60 条')

    // antd 换页/换容量统一走 onChange(current, pageSize)：选择 20 条/页 → 受控 pageSize 更新并以第 1 页重查
    // （jsdom 下 antd 分页下拉选项渲染为测试 locale 文案「20 / page」）
    const sizeChanger = document.querySelector('.ant-pagination .ant-select input[role="combobox"]') as HTMLElement | null
    expect(sizeChanger).toBeTruthy()
    fireEvent.mouseDown(sizeChanger!)
    fireEvent.click(await screen.findByText('20 / page'))

    await new Promise((resolve) => setTimeout(resolve, 0))
    expect(getAppSecurityEvents).toHaveBeenLastCalledWith('a1', 1, 20, { keyword: undefined, scope: undefined })
  })

  it('非管理员不渲染且不加载事件', () => {
    render(<AppSecurityEventsCard appId="a1" canManage={false} />)

    expect(screen.queryByText('已拦截')).toBeNull()
    expect(getAppSecurityEvents).not.toHaveBeenCalled()
  })
})
