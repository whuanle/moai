import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Variables } from '../Variables'
import { useAppStore } from '@/store/app'
import { getVariables } from '@/api/variable'

vi.mock('@/api/variable', () => ({
  getVariables: vi.fn().mockResolvedValue({
    teamId: '7',
    myRole: 0,
    items: [
      { variableId: '1', teamId: '7', key: 'WIKI_NAME', groupName: '基础配置', isSecret: false, value: '团队知识库', description: '', updateTime: '2026-09-02T00:00:00Z' },
      { variableId: '2', teamId: '7', key: 'FEISHU_SECRET', groupName: '飞书', isSecret: true, value: null, description: '密钥', updateTime: '2026-09-02T00:00:00Z' },
    ],
  }),
  getVariableDetail: vi.fn(),
  createVariable: vi.fn().mockResolvedValue(3),
  updateVariable: vi.fn().mockResolvedValue(undefined),
  deleteVariable: vi.fn().mockResolvedValue(undefined),
  substituteVariables: vi.fn().mockResolvedValue(''),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderVariables() {
  return render(
    <MemoryRouter>
      <Variables />
    </MemoryRouter>,
  )
}

describe('Variables', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getVariables).mockResolvedValue({
      teamId: '7',
      myRole: 0,
      items: [
        { variableId: '1', teamId: '7', key: 'WIKI_NAME', groupName: '基础配置', isSecret: false, value: '团队知识库', description: '', updateTime: '2026-09-02T00:00:00Z' },
        { variableId: '2', teamId: '7', key: 'FEISHU_SECRET', groupName: '飞书', isSecret: true, value: null, description: '密钥', updateTime: '2026-09-02T00:00:00Z' },
      ],
    })
    useAppStore.setState({ currentTeamId: '7' })
  })

  it('渲染变量列表：普通变量显示值、私密变量掩码', async () => {
    renderVariables()

    await waitFor(() => {
      expect(getVariables).toHaveBeenCalledWith(7, { groupName: undefined, keyword: undefined })
    })
    expect(await screen.findByText(/WIKI_NAME/)).toBeInTheDocument()
    expect(screen.getByText('团队知识库')).toBeInTheDocument()
    // 私密变量掩码
    expect(screen.getByText('••••••••')).toBeInTheDocument()
    // 类型标签
    expect(screen.getByText('私密')).toBeInTheDocument()
  })

  it('Admin 角色显示新建与操作列', async () => {
    renderVariables()
    expect(await screen.findByText(/WIKI_NAME/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '新建变量' })).toBeInTheDocument()
    expect(screen.getAllByLabelText('编辑').length).toBeGreaterThan(0)
  })

  it('Member 角色只读（无新建/操作列）', async () => {
    vi.mocked(getVariables).mockResolvedValue({ teamId: '7', myRole: 2, items: [] })
    renderVariables()
    await waitFor(() => {
      expect(getVariables).toHaveBeenCalled()
    })
    await waitFor(() => {
      expect(screen.queryByRole('button', { name: '新建变量' })).toBeNull()
    })
  })

  it('未选择团队时提示先选团队', () => {
    useAppStore.setState({ currentTeamId: null })
    renderVariables()
    expect(screen.getByText(/请先在左上角选择一个团队/)).toBeInTheDocument()
  })
})
