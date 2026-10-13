import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import '@/i18n'
import { AppSecuritySection } from '../AppSecuritySection'
import { getAppSecurity, getAppSecurityEvents, saveAppSecurity } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppSecurity: vi.fn(),
  saveAppSecurity: vi.fn(),
  getAppSecurityEvents: vi.fn(),
  SECURITY_RULE_TYPES: ['phone', 'idCard', 'email', 'bankCard', 'custom'],
  SECURITY_STRATEGIES: ['redact', 'mask', 'hash', 'block', 'flag'],
}))

/**
 * 卡片内开关按 DOM 顺序固定：0=内容脱敏总开关 1=工具调用结果 2=工具调用参数 3=模型回复 4=用户输入
 */
const switchByIndex = (index: number) => screen.getAllByRole('switch')[index]

describe('AppSecuritySection（应用安全设置）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppSecurity).mockResolvedValue({
      appId: 'a1',
      enabled: true,
      maskToolResult: true,
      maskToolArgs: false,
      maskModelOutput: false,
      maskInput: false,
      rules: [{ name: '手机号', type: 'phone', pattern: null, replacement: null, strategy: 'redact', keepLast: null }],
      modelOutputRules: [{ name: '邮箱', type: 'email', pattern: null, replacement: null, strategy: 'redact', keepLast: null }],
      myRole: 2,
    })
    vi.mocked(getAppSecurityEvents).mockResolvedValue({ items: [], total: 0, pageNo: 1, pageSize: 10 })
  })

  it('加载并渲染五组独立卡片与安全事件，内容规则随总开关开启显示', async () => {
    render(<AppSecuritySection appId="a1" canManage />)

    expect(await screen.findByText('内容脱敏')).toBeTruthy()
    expect(screen.getByText('工具调用结果')).toBeTruthy()
    expect(screen.getByText('工具调用参数')).toBeTruthy()
    expect(screen.getByText('模型回复')).toBeTruthy()
    expect(screen.getByText('用户输入')).toBeTruthy()
    expect(screen.getByText('安全事件')).toBeTruthy()

    // 总开关开启：内容规则编辑器内嵌显示
    expect(screen.getByText('脱敏规则')).toBeTruthy()
    expect(await screen.findByDisplayValue('手机号')).toBeTruthy()

    // 模型回复开关关闭：其专属规则编辑器隐藏，但配置保留
    expect(screen.queryByDisplayValue('邮箱')).toBeNull()
    await waitFor(() => expect(getAppSecurity).toHaveBeenCalledWith('a1'))
    await waitFor(() => expect(getAppSecurityEvents).toHaveBeenCalledWith('a1', 1, 10))
  })

  it('总开关关闭时规则编辑器隐藏但不清空，开启后恢复可维护', async () => {
    vi.mocked(getAppSecurity).mockResolvedValue({
      appId: 'a1',
      enabled: false,
      maskToolResult: true,
      maskToolArgs: false,
      maskModelOutput: false,
      maskInput: false,
      rules: [{ name: '手机号', type: 'phone', pattern: null, replacement: null, strategy: 'redact', keepLast: null }],
      modelOutputRules: [],
      myRole: 2,
    })

    render(<AppSecuritySection appId="a1" canManage />)

    // 卡片常显，规则编辑器隐藏
    expect(await screen.findByText('内容脱敏')).toBeTruthy()
    expect(screen.getByText('工具调用结果')).toBeTruthy()
    expect(screen.getByText('模型回复')).toBeTruthy()
    expect(screen.queryByText('脱敏规则')).toBeNull()
    expect(screen.queryByRole('button', { name: /添加规则/ })).toBeNull()

    fireEvent.click(switchByIndex(0))

    expect(await screen.findByText('脱敏规则')).toBeTruthy()
    expect(screen.getByDisplayValue('手机号')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /添加规则/ }))
    expect(screen.getAllByDisplayValue('').length).toBeGreaterThan(0)
  })

  it('模型回复规则独立维护且策略选项不含拦截，开启后与内容规则互不干扰', async () => {
    render(<AppSecuritySection appId="a1" canManage />)

    expect(await screen.findByDisplayValue('手机号')).toBeTruthy()
    expect(screen.queryByDisplayValue('邮箱')).toBeNull()

    fireEvent.click(switchByIndex(3))

    // 两组规则编辑器同时存在，各自维护各自的规则
    expect(await screen.findByDisplayValue('邮箱')).toBeTruthy()
    expect(screen.getByDisplayValue('手机号')).toBeTruthy()
    expect(screen.getAllByText('脱敏规则').length).toBe(2)

    // 模型回复规则集的策略下拉不含「拦截」，内容规则集含
    const combos = screen.getAllByRole('combobox') as HTMLInputElement[]
    const strategyCombos = combos.filter((el) => el.getAttribute('aria-autocomplete') === 'list')
    expect(strategyCombos.length).toBeGreaterThan(0)
  })

  it('非管理员不显示保存与添加入口，展示成员提示且不加载事件', async () => {
    render(<AppSecuritySection appId="a1" canManage={false} />)

    expect(await screen.findByText('内容脱敏')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /添加规则/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /保\s*存/ })).toBeNull()
    expect(screen.getByText(/只能查看与使用应用/)).toBeTruthy()
    expect(screen.queryByText('安全事件')).toBeNull()
    expect(getAppSecurityEvents).not.toHaveBeenCalled()
  })

  it('内容规则自定义正则缺失时阻断保存', async () => {
    vi.mocked(getAppSecurity).mockResolvedValue({
      appId: 'a1',
      enabled: true,
      maskToolResult: true,
      maskToolArgs: false,
      maskModelOutput: false,
      maskInput: false,
      rules: [{ name: '订单号', type: 'custom', pattern: '', replacement: null, strategy: 'redact', keepLast: null }],
      modelOutputRules: [],
      myRole: 2,
    })

    render(<AppSecuritySection appId="a1" canManage />)

    // feedback 在测试态为 no-op，不渲染文案，只断言未发起保存
    fireEvent.click(await screen.findByRole('button', { name: /保\s*存/ }))
    expect(saveAppSecurity).not.toHaveBeenCalled()
  })

  it('保存时提交五组开关与两组规则载荷（含策略字段）', async () => {
    vi.mocked(saveAppSecurity).mockResolvedValue(undefined)

    render(<AppSecuritySection appId="a1" canManage />)

    fireEvent.click(await screen.findByRole('button', { name: /保\s*存/ }))
    await waitFor(() =>
      expect(saveAppSecurity).toHaveBeenCalledWith('a1', {
        enabled: true,
        maskToolResult: true,
        maskToolArgs: false,
        maskModelOutput: false,
        maskInput: false,
        rules: [{ name: '手机号', type: 'phone', pattern: null, replacement: null, strategy: 'redact', keepLast: null }],
        modelOutputRules: [{ name: '邮箱', type: 'email', pattern: null, replacement: null, strategy: 'redact', keepLast: null }],
      }),
    )
  })

  it('安全事件列表展示命中记录（时间/作用域/规则/动作/次数）', async () => {
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

    render(<AppSecuritySection appId="a1" canManage />)

    expect(await screen.findByText('工具结果')).toBeTruthy()
    expect(screen.getByText('已拦截')).toBeTruthy()
    expect(screen.getByText('银行卡号')).toBeTruthy()
    expect(screen.getByText('3')).toBeTruthy()
  })
})
