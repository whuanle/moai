import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { KnowledgeGraphSettings } from '../KnowledgeGraphSettings'
import { useAppStore } from '@/store/app'
import type { KnowledgeGraphDetail } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  updateKnowledgeGraph: vi.fn(),
  deleteKnowledgeGraph: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderSettings(graph: KnowledgeGraphDetail | null) {
  return render(
    <MemoryRouter>
      <KnowledgeGraphSettings graph={graph} onChanged={vi.fn()} />
    </MemoryRouter>,
  )
}

describe('KnowledgeGraphSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 't', userId: '1', userName: 'o' },
      currentTeamId: '7',
      myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }],
    })
  })

  it('管理员渲染可编辑表单与保存/删除入口', () => {
    renderSettings({ kgId: '1', name: '支付域图谱', myRole: 2, createTime: '2026-09-10T00:00:00Z' })
    expect(screen.getByDisplayValue('支付域图谱')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /保\s*存/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /删\s*除/ })).toBeInTheDocument()
  })

  it('普通成员只读并提示无权限', () => {
    renderSettings({ kgId: '1', name: '支付域图谱', myRole: 0 })
    expect(screen.getByText('支付域图谱')).toBeInTheDocument()
    expect(screen.getByText(/仅团队管理员/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /保存/ })).toBeNull()
  })

  it('详情未加载时渲染占位而非空只读分支', () => {
    renderSettings(null)
    expect(screen.queryByText('基础信息')).toBeNull()
    expect(screen.queryByText(/仅团队管理员/)).toBeNull()
  })

  it('接入图展示数据库名并使用移除接入的确认文案', async () => {
    renderSettings({
      kgId: '1',
      name: '外部图谱',
      mode: 'connected',
      database: 'neo4j',
      myRole: 2,
      createTime: '2026-09-10T00:00:00Z',
    })
    expect(screen.getByText('neo4j')).toBeInTheDocument()
    expect(screen.queryByText('空白 / 自定义')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: /删\s*除/ }))
    expect(await screen.findByText('仅从平台移除该接入，不影响外部数据，确认移除？')).toBeInTheDocument()
  })
})
