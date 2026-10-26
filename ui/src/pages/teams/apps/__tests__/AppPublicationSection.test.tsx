import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppPublicationSection } from '../AppPublicationSection'
import type { AppDetail } from '../AppConfigSection'
import { applyPublication, getTeamPublicationList, withdrawPublication, type PublicationReviewItem } from '@/api/publication'

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('9'),
  getTeamPublicationList: vi.fn().mockResolvedValue([]),
  withdrawPublication: vi.fn().mockResolvedValue(undefined),
}))

const DETAIL: AppDetail = {
  appId: 'a1',
  teamId: '3',
  name: '客服助手',
  description: '售前售后问答',
  appType: 'agent',
  avatarPath: '',
  isPublic: false,
  publishStatus: 1,
  publishTime: '2026-10-18 09:00:00',
  myRole: 2,
}

function record(overrides: Partial<PublicationReviewItem>): PublicationReviewItem {
  return {
    publicationId: '9',
    resourceType: 'app',
    resourceId: 'a1',
    resourceName: '客服助手',
    publishVersion: 10200,
    teamId: '3',
    state: 'rejected',
    applyReason: '对外提供客服问答',
    reviewComment: '应用描述不完整，请补充后重新提交',
    createUserName: '张三',
    createTime: '2026-10-20 10:00:00',
    reviewTime: '2026-10-21 12:00:00',
    updateUserName: '管理员',
    ...overrides,
  }
}

function renderSection(detail: AppDetail = DETAIL, canManage = true, configStatus: number | null = 1) {
  return render(
    <AppPublicationSection
      teamId={3}
      appId={detail.appId ?? 'a1'}
      detail={detail}
      canManage={canManage}
      configStatus={configStatus}
      onReload={vi.fn()}
    />,
  )
}

describe('AppPublicationSection（上架管理分区）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTeamPublicationList).mockResolvedValue([])
  })

  it('未公开且无申请：展示未申请与版本草稿状态，管理员按版本号申请上架', async () => {
    const onReload = vi.fn()
    render(
      <AppPublicationSection teamId={3} appId="a1" detail={DETAIL} canManage configStatus={0} onReload={onReload} />,
    )

    expect(await screen.findByText('未申请')).toBeTruthy()
    expect(screen.getByText('已发布')).toBeTruthy()
    expect(screen.getByText('有未发布变更')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: /申请上架/ }))
    const version = (await screen.findByLabelText('应用版本')) as HTMLInputElement
    expect(version.value).toBe('1.0.0')
    fireEvent.change(version, { target: { value: '1.2.0' } })
    const reason = await screen.findByPlaceholderText(/上架理由/)
    fireEvent.change(reason, { target: { value: '对外提供客服问答' } })
    fireEvent.click(screen.getByRole('button', { name: /提交申请/ }))

    await waitFor(() => {
      expect(applyPublication).toHaveBeenCalledWith({
        resourceType: 'app',
        resourceId: 'a1',
        applyReason: '对外提供客服问答',
        version: '1.2.0',
      })
      expect(onReload).toHaveBeenCalled()
    })
  })

  it('申请弹窗版本号必填且格式非法时阻止提交', async () => {
    renderSection()

    fireEvent.click(await screen.findByRole('button', { name: /申请上架/ }))
    const version = (await screen.findByLabelText('应用版本')) as HTMLInputElement
    fireEvent.change(version, { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: /提交申请/ }))

    expect(await screen.findByText('请填写应用版本号')).toBeTruthy()
    expect(applyPublication).not.toHaveBeenCalled()
  })

  it('审核中：展示待审核标签并可撤回申请', async () => {
    vi.mocked(getTeamPublicationList).mockResolvedValue([
      record({ publicationId: '8', state: 'pending', reviewComment: null, reviewTime: null, updateUserName: null }),
      record({ publicationId: '7', state: 'rejected' }),
    ])
    renderSection()

    expect(await screen.findByText('审核中')).toBeTruthy()

    // antd 两字按钮会插入空格（“确 定”），沿用仓库既有的 popconfirm 主按钮选择器
    fireEvent.click(screen.getAllByRole('button', { name: /撤回申请/ })[0])
    fireEvent.click(document.querySelector('.ant-popconfirm-buttons .ant-btn-primary') as HTMLElement)

    await waitFor(() => {
      expect(withdrawPublication).toHaveBeenCalledWith('8')
    })
  })

  it('被驳回：展示拒绝原因并支持重新申请', async () => {
    vi.mocked(getTeamPublicationList).mockResolvedValue([record({})])
    renderSection()

    // 概览标签与记录行状态标签都会出现「已驳回」
    expect((await screen.findAllByText('已驳回')).length).toBeGreaterThan(0)
    expect(await screen.findByText('拒绝原因')).toBeTruthy()
    // 拒绝原因同时出现在概览 Alert 与记录行审批意见列
    expect((await screen.findAllByText(/应用描述不完整/)).length).toBeGreaterThan(0)
    expect(screen.getByRole('button', { name: /重新申请/ })).toBeTruthy()
  })

  it('已公开：展示已公开标签且不再提供申请入口', async () => {
    vi.mocked(getTeamPublicationList).mockResolvedValue([record({ state: 'approved', reviewComment: null })])
    renderSection({ ...DETAIL, isPublic: true })

    expect(await screen.findByText('已公开')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /申请上架/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /重新申请/ })).toBeNull()
  })

  it('版本与草稿：展示线上版本号与草稿版本号，版本未随发布生效时给出提示', async () => {
    renderSection({ ...DETAIL, version: 10203, publishVersion: 10000 }, true, 1)

    // 线上=1.0.0（发布快照），草稿=1.2.3
    expect(await screen.findByText('v1.0.0')).toBeTruthy()
    expect(screen.getByText('v1.2.3')).toBeTruthy()
    expect(screen.getByText('版本未发布')).toBeTruthy()
  })

  it('版本与草稿：版本一致时不再重复提示未发布', async () => {
    renderSection({ ...DETAIL, version: 10000, publishVersion: 10000 }, true, 1)

    expect(await screen.findAllByText('v1.0.0').then((els) => els.length)).toBe(2)
    expect(screen.getByText('与线上一致')).toBeTruthy()
    expect(screen.queryByText('版本未发布')).toBeNull()
  })

  it('申请记录列表展示历次申请的状态、申请理由与审批意见', async () => {
    vi.mocked(getTeamPublicationList).mockResolvedValue([
      record({}),
      record({ publicationId: '8', state: 'pending', applyReason: '补充描述后再试', reviewComment: null, reviewTime: null, updateUserName: null, createTime: '2026-10-22 09:00:00' }),
    ])
    renderSection()

    expect(getTeamPublicationList).toHaveBeenCalledWith(3, expect.objectContaining({ resourceType: 'app' }))
    expect(await screen.findByText('对外提供客服问答')).toBeTruthy()
    expect(await screen.findByText('补充描述后再试')).toBeTruthy()
    expect(screen.getAllByText('v1.2.0').length).toBe(2)
    expect(screen.getAllByText('张三').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('管理员')).toBeTruthy()
  })

  it('普通成员只读：可见状态与记录，无申请/撤回入口', async () => {
    vi.mocked(getTeamPublicationList).mockResolvedValue([
      record({ state: 'pending', reviewComment: null, reviewTime: null, updateUserName: null }),
    ])
    renderSection(DETAIL, false)

    expect(await screen.findByText('审核中')).toBeTruthy()
    expect(await screen.findByText('张三')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /撤回申请/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /申请上架/ })).toBeNull()
  })
})
