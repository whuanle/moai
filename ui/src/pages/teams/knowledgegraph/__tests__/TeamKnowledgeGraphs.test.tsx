import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useParams } from 'react-router'
import { TeamKnowledgeGraphs } from '../TeamKnowledgeGraphs'
import { createKnowledgeGraph, getKnowledgeGraphs, getKnowledgeGraphTemplates, uploadKnowledgeGraphAvatar } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphs: vi.fn(),
  getKnowledgeGraphTemplates: vi.fn().mockResolvedValue([]),
  createKnowledgeGraph: vi.fn().mockResolvedValue(1),
  updateKnowledgeGraph: vi.fn().mockResolvedValue(undefined),
  deleteKnowledgeGraph: vi.fn().mockResolvedValue(undefined),
  uploadKnowledgeGraphAvatar: vi.fn().mockResolvedValue(''),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderTeam() {
  return render(
    <MemoryRouter>
      <TeamKnowledgeGraphs teamId={7} />
    </MemoryRouter>,
  )
}

function DetailProbe() {
  const params = useParams<{ graphId: string; section?: string }>()
  return <div>probe-{params.section ?? 'none'}</div>
}

function renderTeamWithRoutes() {
  return render(
    <MemoryRouter initialEntries={['/team/7/kg']}>
      <Routes>
        <Route path="/team/:teamId/kg" element={<TeamKnowledgeGraphs teamId={7} />} />
        <Route path="/team/:teamId/kg/:graphId/:section?" element={<DetailProbe />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('TeamKnowledgeGraphs', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getKnowledgeGraphTemplates).mockResolvedValue([
      { key: 'blank', name: '空白 / 自定义' },
      { key: 'logistics', name: '物流运输', entityTypes: ['港口', '航段'] },
    ])
  })

  it('加载并展示团队知识图谱，管理员可见新建入口', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      enabled: true,
      items: [{ kgId: '1', teamId: '7', name: '支付域图谱', description: '运维' }],
    })

    renderTeam()

    expect(await screen.findByText('支付域图谱')).toBeInTheDocument()
    expect(getKnowledgeGraphs).toHaveBeenCalledWith(7)
    expect(screen.getByRole('button', { name: /新建知识图谱/ })).toBeInTheDocument()
  })

  it('普通成员不显示新建入口', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7',
      myRole: 0,
      enabled: true,
      items: [{ kgId: '1', teamId: '7', name: '支付域图谱' }],
    })

    renderTeam()

    expect(await screen.findByText('支付域图谱')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /新建知识图谱/ })).toBeNull()
  })

  it('未开启能力时提示禁用且新建按钮不可用', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: false, items: [] })

    renderTeam()

    expect(await screen.findByText(/未开启知识图谱能力/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /新建知识图谱/ })).toBeDisabled()
  })

  it('新建弹窗：来源单选在模板与数据库输入间切换', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [] })
    renderTeam()
    fireEvent.click(await screen.findByRole('button', { name: /新建知识图谱/ }))

    const modal = await screen.findByRole('dialog')
    expect(within(modal).getByText('模板')).toBeInTheDocument()
    expect(within(modal).queryByLabelText('数据库名')).toBeNull()

    fireEvent.click(within(modal).getByRole('radio', { name: '接入已有' }))
    expect(await within(modal).findByLabelText('数据库名')).toBeInTheDocument()
    expect(within(modal).queryByText('模板')).toBeNull()
  })

  it('新建弹窗：接入模式数据库必填，未填不发请求', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [] })
    renderTeam()
    fireEvent.click(await screen.findByRole('button', { name: /新建知识图谱/ }))

    const modal = await screen.findByRole('dialog')
    fireEvent.change(within(modal).getByLabelText('名称'), { target: { value: '外部图谱' } })
    fireEvent.click(within(modal).getByRole('radio', { name: '接入已有' }))
    fireEvent.click(screen.getByRole('button', { name: 'OK' }))

    expect(await within(modal).findByText('请输入 Neo4j 数据库名')).toBeInTheDocument()
    expect(createKnowledgeGraph).not.toHaveBeenCalled()
  })

  it('接入图卡片显示只读徽标', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      enabled: true,
      items: [{ kgId: '2', teamId: '7', name: '外部图谱', mode: 'connected' }],
    })
    renderTeam()

    expect(await screen.findByText('外部图谱')).toBeInTheDocument()
    expect(screen.getByText('外部接入 · 只读')).toBeInTheDocument()
  })

  it('点击图谱卡片默认进入图览', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7',
      myRole: 2,
      enabled: true,
      items: [{ kgId: '1', teamId: '7', name: '支付域图谱' }],
    })
    renderTeamWithRoutes()

    fireEvent.click(await screen.findByText('支付域图谱'))
    expect(await screen.findByText('probe-canvas')).toBeInTheDocument()
  })

  it('创建时选择头像：建图成功后登记头像', async () => {
    vi.mocked(createKnowledgeGraph).mockResolvedValue(9)
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [] })
    renderTeam()
    fireEvent.click(await screen.findByRole('button', { name: /新建知识图谱/ }))

    const modal = await screen.findByRole('dialog')
    fireEvent.change(within(modal).getByLabelText('名称'), { target: { value: '带头像图谱' } })
    const file = new File(['png'], 'avatar.png', { type: 'image/png' })
    const input = modal.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(within(modal).getByRole('button', { name: 'OK' }))

    await waitFor(() => expect(uploadKnowledgeGraphAvatar).toHaveBeenCalledWith(9, expect.any(File)))
    expect(createKnowledgeGraph).toHaveBeenCalledTimes(1)
    // jsdom 不执行 CSS 动画，Modal 关闭动画不会结束；以创建后列表刷新为完成信号
    await waitFor(() => expect(getKnowledgeGraphs).toHaveBeenCalledTimes(2))
  })

  it('头像登记失败不阻断图谱创建成功', async () => {
    vi.mocked(createKnowledgeGraph).mockResolvedValue(9)
    vi.mocked(uploadKnowledgeGraphAvatar).mockRejectedValueOnce(new Error('upload failed'))
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [] })
    renderTeam()
    fireEvent.click(await screen.findByRole('button', { name: /新建知识图谱/ }))

    const modal = await screen.findByRole('dialog')
    fireEvent.change(within(modal).getByLabelText('名称'), { target: { value: '带头像图谱' } })
    const file = new File(['png'], 'avatar.png', { type: 'image/png' })
    const input = modal.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(within(modal).getByRole('button', { name: 'OK' }))

    await waitFor(() => expect(createKnowledgeGraph).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(uploadKnowledgeGraphAvatar).toHaveBeenCalled())
    // 登记失败不阻断：创建成功仍触发列表刷新
    await waitFor(() => expect(getKnowledgeGraphs).toHaveBeenCalledTimes(2))
  })

  it('创建时不选头像则不调用头像登记', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [] })
    renderTeam()
    fireEvent.click(await screen.findByRole('button', { name: /新建知识图谱/ }))

    const modal = await screen.findByRole('dialog')
    fireEvent.change(within(modal).getByLabelText('名称'), { target: { value: '无头像图谱' } })
    fireEvent.click(within(modal).getByRole('button', { name: 'OK' }))

    await waitFor(() => expect(createKnowledgeGraph).toHaveBeenCalledTimes(1))
    expect(uploadKnowledgeGraphAvatar).not.toHaveBeenCalled()
  })
})
