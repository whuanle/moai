import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { ChatComposer } from '../chat/ChatComposer'
import type { AppUserTeamAgentOption } from '@/api/app'

vi.mock('@/api/agentChat', () => ({}))

const agent = (id: string, name: string): AppUserTeamAgentOption => ({
  id,
  key: id,
  name,
  description: '',
  avatarObjectKey: '',
})

function renderComposer(teamAgents: AppUserTeamAgentOption[], selected: string | null = null) {
  return render(
    <ChatComposer
      value=""
      onValueChange={vi.fn()}
      onSubmit={vi.fn()}
      sending={false}
      onStop={vi.fn()}
      placeholder="发消息"
      teamAgentOptions={teamAgents}
      selectedTeamAgentId={selected}
      onTeamAgentChange={vi.fn()}
    />,
  )
}

describe('ChatComposer 团队智能体切换器', () => {
  it('多选 ≥2 个智能体时展示切换器，选中默认项并带头像选项（@AP-S156）', () => {
    renderComposer([agent('plan', '规划师 Plan'), agent('build', '构建师 Build')], 'plan')
    // 关闭态显示当前生效者名称
    expect(screen.getByText('规划师 Plan')).toBeTruthy()
    fireEvent.mouseDown(screen.getByTitle('规划师 Plan'))
    expect(screen.getByText('构建师 Build')).toBeTruthy()
  })

  it('单选或未绑定时切换器隐藏（后端直接生效，@AP-S156）', () => {
    const { container: single } = renderComposer([agent('build', '构建师 Build')])
    expect(single.textContent).not.toContain('构建师 Build')
    const { container: none } = renderComposer([])
    expect(none.textContent).not.toContain('规划师')
  })
})
