import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AppUserSettings } from '../chat/AppUserSettings'
import { getAppUserConfig, saveAppUserConfig } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppUserConfig: vi.fn(),
  saveAppUserConfig: vi.fn().mockResolvedValue(undefined),
}))

function renderPanel(onSaved = vi.fn(), onClose = vi.fn(), currentPromptId = 11) {
  return render(
    <MemoryRouter>
      <AppUserSettings
        open
        teamId={3}
        appId="a1"
      experts={[
        { promptId: 11, name: '写作专家', description: '文案润色', teamId: 3 },
      ]}
        currentPromptId={currentPromptId}
        onClose={onClose}
        onSaved={onSaved}
      />
    </MemoryRouter>,
  )
}

describe('AppUserSettings（应用设置面板）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 11,
      skills: ['sk-2'],
      defaultSkills: [
        { id: 'sk-1', key: 'docx_writer', name: '系统技能', description: '', isSystem: true, teamId: 0 },
        { id: 'sk-2', key: 'ppt_writer', name: '团队技能', description: '', isSystem: false, teamId: 3 },
      ],
      toolApprovalMode: 'auto',
      toolApprovalExemptNames: [],
      toolApprovalExemptPrefixes: [],
      toolApprovalAutoApprovedNames: [],
      toolApprovalAutoApprovedPrefixes: [],
      memoryMode: 2,
      memoryEnabled: true,
      memoryGlobalMode: 0,
      memoryGlobalAllowed: false,
    })
  })

  it('打开后加载用户配置：专家回显当前生效值，目录内技能全部可勾选/取消', async () => {
    renderPanel()

    await waitFor(() => expect(getAppUserConfig).toHaveBeenCalledWith('a1'))
    // 技能卡片展示，仅已启用的 sk-2 卡片带选中态
    expect(screen.getByText('系统技能')).toBeInTheDocument()
    expect(screen.getByText('团队技能')).toBeInTheDocument()
    expect(screen.getByText('团队技能').closest('.moai-chat__opt-card')).toHaveClass('is-active')
    expect(screen.getByText('系统技能').closest('.moai-chat__opt-card')).not.toHaveClass('is-active')
    // 专家按当前生效值回显（currentPromptId=11，即配置中的写作专家），选中卡带 is-active
    expect(screen.getByText('写作专家')).toBeInTheDocument()
    expect(screen.getByText('写作专家').closest('.moai-chat__opt-card')).toHaveClass('is-active')
    // 左缘拖拽手柄存在，默认宽度 460
    expect(screen.getByRole('separator', { name: '拖动调整面板宽度' })).toBeInTheDocument()
    expect(document.querySelector('.moai-chat__settings')).toHaveStyle({ width: '460px' })
  })

  it('专家草稿以当前生效值为准：与持久化默认不同时按当前值提交', async () => {
    const onSaved = vi.fn()
    // 持久化默认 promptId=11，但当前会话生效值为 0（未选专家）→ 保存提交 0
    renderPanel(onSaved, vi.fn(), 0)

    await waitFor(() => expect(screen.getByText('写作专家')).toBeInTheDocument())
    expect(document.querySelector('.moai-chat__expert.is-active')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', { promptId: 0, skills: ['sk-2'], toolApprovalMode: 'auto', memoryMode: 2 })
      expect(onSaved).toHaveBeenCalledWith(0, 'auto')
    })
  })

  it('勾选默认技能并保存：提交勾选集合、新会话默认专家与工具审批模式', async () => {
    const onSaved = vi.fn()
    const onClose = vi.fn()
    renderPanel(onSaved, onClose)

    await waitFor(() => expect(screen.getByText('系统技能')).toBeInTheDocument())
    // 初始仅勾选 sk-2，补勾 sk-1 后两项均提交
    fireEvent.click(screen.getByText('系统技能'))
    // 切换到审批模式后一并提交
    fireEvent.click(screen.getByRole('button', { name: /审批模式/ }))
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', { promptId: 11, skills: ['sk-2', 'sk-1'], toolApprovalMode: 'approval', memoryMode: 2 })
    })
  })

  it('专家卡片按热度取前十、搜索过滤且已选项置顶', async () => {
    const pool = Array.from({ length: 12 }, (_, i) => ({
      promptId: i + 100,
      name: `专家${i + 100}`,
      description: `desc-${i + 100}`,
      teamId: 3,
      useCount: i + 1,
    }))
    // 当前生效专家 promptId=105 不在热度前十里（useCount=6）
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 11,
      skills: [],
      defaultSkills: [],
      toolApprovalMode: 'auto',
      toolApprovalExemptNames: [],
      toolApprovalExemptPrefixes: [],
      toolApprovalAutoApprovedNames: [],
      toolApprovalAutoApprovedPrefixes: [],
      memoryMode: 0,
      memoryEnabled: false,
      memoryGlobalMode: 0,
      memoryGlobalAllowed: false,
    })
    const { container } = render(
      <MemoryRouter>
        <AppUserSettings
          open
          teamId={3}
          appId="a1"
          experts={[...pool, { promptId: 11, name: '写作专家', description: '文案', teamId: 3, useCount: 2 }]}
          currentPromptId={105}
          onClose={vi.fn()}
          onSaved={vi.fn()}
        />
      </MemoryRouter>,
    )
    await waitFor(() => expect(screen.getByText('专家105')).toBeInTheDocument())
    let cards = container.querySelectorAll('.moai-chat__opt-card')
    expect(cards.length).toBe(10)
    // 热度降序：首位是 useCount 最大的 专家111
    expect(cards[0].textContent).toContain('专家111')
    // 已选项不在热度前十仍被置顶展示（共 10 张，含置顶的 105）
    expect(container.textContent).not.toContain('专家100')
    // 搜索命中唯一专家
    fireEvent.change(screen.getByPlaceholderText('搜索专家'), { target: { value: '专家108' } })
    await waitFor(() => {
      cards = container.querySelectorAll('.moai-chat__opt-card')
      expect(cards.length).toBe(1)
      expect(cards[0].textContent).toContain('专家108')
    })
  })

  it('技能卡片按 useCount 热度排序取前十', async () => {
    const skills = Array.from({ length: 12 }, (_, i) => ({
      id: `sk-${i + 1}`,
      key: `skill_${i + 1}`,
      name: `技能${i + 1}`,
      description: '',
      isSystem: true,
      teamId: 0,
      // 热度与目录序相反：目录第一个热度最高
      useCount: 12 - i,
    }))
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 0,
      skills: [],
      defaultSkills: skills,
      toolApprovalMode: 'auto',
      toolApprovalExemptNames: [],
      toolApprovalExemptPrefixes: [],
      toolApprovalAutoApprovedNames: [],
      toolApprovalAutoApprovedPrefixes: [],
      memoryMode: 0,
      memoryEnabled: false,
      memoryGlobalMode: 0,
      memoryGlobalAllowed: false,
    })
    const { container } = render(
      <MemoryRouter>
        <AppUserSettings
          open
          teamId={3}
          appId="a1"
          experts={[]}
          currentPromptId={0}
          onClose={vi.fn()}
          onSaved={vi.fn()}
        />
      </MemoryRouter>,
    )
    await waitFor(() => expect(screen.getByText('技能1')).toBeInTheDocument())
    const cards = container.querySelectorAll('.moai-chat__opt-card')
    expect(cards.length).toBe(10)
    // 热度降序：首位是 useCount 最大的 技能1（目录第一个）；热度最低的 技能11/12 被截断
    expect(cards[0].textContent).toContain('技能1')
    expect(container.textContent).not.toContain('技能12')
  })

  it('保存成功后回调新会话默认专家与审批模式并关闭面板', async () => {
    const onSaved = vi.fn()
    const onClose = vi.fn()
    renderPanel(onSaved, onClose)

    await waitFor(() => expect(screen.getByRole('button', { name: /保存设置/ })).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => {
      expect(onSaved).toHaveBeenCalledWith(11, 'auto')
      expect(onClose).toHaveBeenCalled()
    })
  })

  it('应用允许全局接入时展示全局记忆开关，切换后随保存提交 memoryGlobalMode', async () => {
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 11,
      skills: [],
      defaultSkills: [],
      toolApprovalMode: 'auto',
      toolApprovalExemptNames: [],
      toolApprovalExemptPrefixes: [],
      toolApprovalAutoApprovedNames: [],
      toolApprovalAutoApprovedPrefixes: [],
      memoryMode: 2,
      memoryEnabled: true,
      memoryGlobalMode: 0,
      memoryGlobalAllowed: true,
    })
    renderPanel()

    // 允许时展示「全局记忆（跨应用）」分区，默认接入生效
    await waitFor(() => expect(screen.getByText('全局记忆（跨应用）')).toBeInTheDocument())
    // 切到「仅本应用」并保存：携带 memoryGlobalMode=1
    fireEvent.click(screen.getByRole('button', { name: '仅本应用' }))
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', expect.objectContaining({ memoryMode: 2, memoryGlobalMode: 1 }))
    })
  })

  it('应用未允许全局接入时不展示全局记忆开关，保存也不携带 memoryGlobalMode', async () => {
    renderPanel()

    await waitFor(() => expect(screen.getByText('长期记忆')).toBeInTheDocument())
    expect(screen.queryByText('全局记忆（跨应用）')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))
    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', expect.not.objectContaining({ memoryGlobalMode: expect.anything() }))
    })
  })
})
