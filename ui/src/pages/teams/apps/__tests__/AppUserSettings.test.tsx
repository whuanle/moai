import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppUserSettings } from '../chat/AppUserSettings'
import { getAppUserConfig, saveAppUserConfig } from '@/api/app'

vi.mock('@/api/app', () => ({
  getAppUserConfig: vi.fn(),
  saveAppUserConfig: vi.fn().mockResolvedValue(undefined),
}))

function renderPanel(onSaved = vi.fn(), onClose = vi.fn(), currentPromptId = 11) {
  return render(
    <AppUserSettings
      open
      appId="a1"
      experts={[
        { promptId: 11, name: '写作专家', description: '文案润色', teamId: 3 },
      ]}
      currentPromptId={currentPromptId}
      onClose={onClose}
      onSaved={onSaved}
    />,
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
    })
  })

  it('打开后加载用户配置：专家回显当前生效值，目录内技能全部可勾选/取消', async () => {
    renderPanel()

    await waitFor(() => expect(getAppUserConfig).toHaveBeenCalledWith('a1'))
    // 技能目录全部展示且无禁用项，仅勾选用户已启用的 sk-2
    expect(screen.getByText('系统技能')).toBeInTheDocument()
    expect(screen.getByText('团队技能')).toBeInTheDocument()
    const checkboxes = screen.getAllByRole('checkbox') as HTMLInputElement[]
    expect(checkboxes.length).toBe(2)
    expect(checkboxes.every((x) => !x.disabled)).toBe(true)
    expect(checkboxes.filter((x) => x.checked).length).toBe(1)
    // 专家按当前生效值回显（currentPromptId=11，即配置中的写作专家）
    expect(screen.getByText('写作专家')).toBeInTheDocument()
    expect(document.querySelector('.moai-chat__expert.is-active')).not.toBeNull()
  })

  it('专家草稿以当前生效值为准：与持久化默认不同时按当前值提交', async () => {
    const onSaved = vi.fn()
    // 持久化默认 promptId=11，但当前会话生效值为 0（未选专家）→ 保存提交 0
    renderPanel(onSaved, vi.fn(), 0)

    await waitFor(() => expect(screen.getByText('写作专家')).toBeInTheDocument())
    expect(document.querySelector('.moai-chat__expert.is-active')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', { promptId: 0, skills: ['sk-2'], toolApprovalMode: 'auto' })
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
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', { promptId: 11, skills: ['sk-2', 'sk-1'], toolApprovalMode: 'approval' })
    })
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
})
