import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppUserSettings } from '../chat/AppUserSettings'
import { getAppUserConfig, saveAppUserConfig } from '@/api/app'
import { getSkillOptions } from '@/api/skills'

vi.mock('@/api/app', () => ({
  getAppUserConfig: vi.fn(),
  saveAppUserConfig: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/skills', () => ({
  getSkillOptions: vi.fn(),
}))

function renderPanel(onSaved = vi.fn(), onClose = vi.fn()) {
  return render(
    <AppUserSettings
      open
      appId="a1"
      teamId={3}
      experts={[
        { promptId: 11, name: '写作专家', description: '文案润色', teamId: 0 },
      ]}
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
      lockedSkills: ['sk-1'],
    })
    vi.mocked(getSkillOptions).mockResolvedValue([
      { id: 'sk-1', key: 'docx_writer', name: '应用必选技能', description: '', isSystem: true, teamId: 0 },
      { id: 'sk-2', key: 'ppt_writer', name: '团队技能', description: '', isSystem: false, teamId: 3 },
      { id: 'sk-3', key: 'mine', name: '我的技能', description: '', isSystem: false, teamId: 0 },
    ])
  })

  it('打开后加载用户配置：默认专家回显，应用绑定技能锁定，自选技能可勾选', async () => {
    renderPanel()

    await waitFor(() => expect(getAppUserConfig).toHaveBeenCalledWith('a1'))
    // 锁定技能勾选且禁用，带「应用必选」标记
    expect(screen.getByText('应用必选技能')).toBeInTheDocument()
    expect(screen.getByText('应用必选')).toBeInTheDocument()
    const checkboxes = screen.getAllByRole('checkbox') as HTMLInputElement[]
    const lockedCheckbox = checkboxes.find((x) => x.checked && x.disabled)
    expect(lockedCheckbox).toBeTruthy()
    // 勾选中的 = 锁定技能 + 自选技能 sk-2
    expect(checkboxes.filter((x) => x.checked).length).toBe(2)
    // 专家默认回显
    expect(screen.getByText('写作专家')).toBeInTheDocument()
  })

  it('取消自选技能并保存：提交剩余勾选与新会话默认专家', async () => {
    const onSaved = vi.fn()
    const onClose = vi.fn()
    renderPanel(onSaved, onClose)

    await waitFor(() => expect(screen.getByText('团队技能')).toBeInTheDocument())
    // 初始自选为 sk-2（团队技能），点击取消勾选后仅剩空列表
    fireEvent.click(screen.getByText('团队技能'))
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', { promptId: 11, skills: [] })
    })
  })

  it('保存成功后回调新会话默认专家并关闭面板', async () => {
    const onSaved = vi.fn()
    const onClose = vi.fn()
    renderPanel(onSaved, onClose)

    await waitFor(() => expect(screen.getByRole('button', { name: /保存设置/ })).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: /保存设置/ }))

    await waitFor(() => {
      expect(onSaved).toHaveBeenCalledWith(11)
      expect(onClose).toHaveBeenCalled()
    })
  })
})
