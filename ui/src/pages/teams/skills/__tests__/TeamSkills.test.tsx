import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { ConfigProvider } from 'antd'
import zhCN from 'antd/locale/zh_CN'
import { TeamSkills } from '../TeamSkills'
import { deleteSkill, downloadSkillFiles, getSkill, getTeamSkills } from '@/api/skills'
import { applyPublication } from '@/api/publication'

vi.mock('@/api/skills', () => ({
  getTeamSkills: vi.fn().mockResolvedValue([]),
  getSkill: vi.fn().mockResolvedValue({}),
  deleteSkill: vi.fn().mockResolvedValue(undefined),
  downloadSkillFiles: vi.fn().mockResolvedValue(0),
  createSkill: vi.fn().mockResolvedValue('skill-id'),
  updateSkill: vi.fn().mockResolvedValue(undefined),
  setSkillAvatar: vi.fn().mockResolvedValue(undefined),
  uploadSkillFile: vi.fn().mockResolvedValue(1),
  extractSkillPackage: vi.fn().mockResolvedValue({ name: '', description: '', instructions: '', files: [] }),
}))

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('5'),
  withdrawPublication: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/classify', () => ({
  classifyApi: {
    getClassifies: vi.fn().mockResolvedValue([]),
  },
  ClassifyType: { Prompt: 'prompt', Skill: 'skill' },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c.emoji, c.name].filter(Boolean).join(' '),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderTeamSkills(canManage = true) {
  return render(
    <ConfigProvider locale={zhCN}>
      <MemoryRouter initialEntries={['/team/9/skills']}>
        <TeamSkills teamId={9} canManage={canManage} />
      </MemoryRouter>
    </ConfigProvider>,
  )
}

describe('TeamSkills', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTeamSkills).mockResolvedValue([
      {
        id: 's1',
        key: 'translate',
        name: '翻译技能',
        description: '中英互译',
        avatarPath: '',
        isPublic: true,
        fileCount: 2,
        useCount: 7,
        updateTime: '2026-11-01T00:00:00Z',
      },
      {
        id: 's2',
        key: 'weekly',
        name: '周报助手',
        description: '',
        avatarPath: '',
        isPublic: false,
        pendingPublicationId: '5',
        fileCount: 1,
        useCount: 0,
        updateTime: '2026-11-02T00:00:00Z',
      },
    ])
  })

  it('卡片列表展示名称/状态/文件数/使用次数与 key，不使用表格', async () => {
    renderTeamSkills()

    expect(await screen.findByText('翻译技能')).toBeInTheDocument()
    expect(screen.getByText('周报助手')).toBeInTheDocument()
    expect(screen.getByText('已上架')).toBeInTheDocument()
    expect(screen.getByText('审核中')).toBeInTheDocument()
    expect(screen.getByText('2 个文件')).toBeInTheDocument()
    expect(screen.getByText('被使用 7 次')).toBeInTheDocument()
    expect(screen.getByText('translate')).toBeInTheDocument()
    // 卡片视图：无表格、无条数统计
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByText('共 2 条')).not.toBeInTheDocument()
    expect(getTeamSkills).toHaveBeenCalledWith(9, { keywords: undefined })
  })

  it('canManage=false 时隐藏新建入口，底栏仅详情与下载', async () => {
    renderTeamSkills(false)

    await screen.findByText('翻译技能')
    expect(screen.queryByText('新建技能')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '编辑' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '删除' })).not.toBeInTheDocument()
    // 成员可见详情与下载（每卡 2 个按钮）
    expect(screen.getAllByRole('button', { name: '详情' }).length).toBe(2)
    expect(screen.getAllByRole('button', { name: '下载' }).length).toBe(2)
  })

  it('canManage=true 时展示新建入口与编辑/上架/删除操作', async () => {
    renderTeamSkills(true)

    await screen.findByText('翻译技能')
    expect(screen.getByText('新建技能')).toBeInTheDocument()
    // 翻译技能已上架 → 无上架按钮；周报助手待审核 → 撤回按钮；两卡均可编辑
    expect(screen.getAllByRole('button', { name: '编辑' }).length).toBe(2)
    expect(screen.queryByRole('button', { name: '申请上架' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: '撤回申请' })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: '删除' }).length).toBe(2)
  })

  it('关键字搜索触发带 keywords 的查询', async () => {
    renderTeamSkills()

    await screen.findByText('翻译技能')
    fireEvent.change(screen.getByPlaceholderText('搜索技能标识/名称/描述'), { target: { value: '翻译' } })
    fireEvent.click(screen.getByText('查询'))
    await waitFor(() => {
      expect(getTeamSkills).toHaveBeenLastCalledWith(9, { keywords: '翻译' })
    })
  })

  it('删除经 Popconfirm 确认后调用删除并刷新列表', async () => {
    renderTeamSkills(true)

    await screen.findByText('翻译技能')
    fireEvent.click(screen.getAllByRole('button', { name: '删除' })[0])
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() => {
      expect(deleteSkill).toHaveBeenCalledWith('s1')
    })
    await waitFor(() => {
      expect(getTeamSkills).toHaveBeenCalledTimes(2)
    })
  })

  it('详情按钮拉取详情并打开弹窗，下载触发下载', async () => {
    vi.mocked(getSkill).mockResolvedValue({ id: 's1', key: 'translate', name: '翻译技能', instructions: '用法' })
    renderTeamSkills(true)

    await screen.findByText('翻译技能')
    fireEvent.click(screen.getAllByRole('button', { name: '详情' })[0])
    await waitFor(() => {
      expect(getSkill).toHaveBeenCalledWith('s1')
    })

    fireEvent.click(screen.getAllByRole('button', { name: '下载' })[0])
    await waitFor(() => {
      expect(downloadSkillFiles).toHaveBeenCalledWith('s1')
    })
  })

  it('未上架技能展示上架入口，弹窗提交申请', async () => {
    vi.mocked(getTeamSkills).mockResolvedValue([
      {
        id: 's3',
        key: 'private-skill',
        name: '私有技能',
        description: '未上架',
        avatarPath: '',
        isPublic: false,
        fileCount: 1,
        useCount: 0,
        updateTime: '2026-11-02T00:00:00Z',
      },
    ])
    renderTeamSkills(true)

    await screen.findByText('私有技能')
    fireEvent.click(screen.getByRole('button', { name: '申请上架' }))
    fireEvent.change(await screen.findByPlaceholderText('向管理员说明该技能的用途（可选）'), { target: { value: '团队共用' } })
    fireEvent.click(screen.getByRole('button', { name: '提交申请' }))
    await waitFor(() => {
      expect(applyPublication).toHaveBeenCalledWith({ resourceType: 'skill', resourceId: 's3', applyReason: '团队共用' })
    })
  })
})
