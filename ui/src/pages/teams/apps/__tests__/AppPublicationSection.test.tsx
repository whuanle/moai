import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppPublicationSection } from '../AppPublicationSection'
import type { AppDetail } from '../AppConfigSection'
import { publishApp, unpublishApp } from '@/api/app'
import { applyPublication, getTeamPublicationList, takedownPublication, withdrawPublication, type PublicationReviewItem } from '@/api/publication'

vi.mock('@/api/app', () => ({
  publishApp: vi.fn().mockResolvedValue(undefined),
  unpublishApp: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('9'),
  getTeamPublicationList: vi.fn().mockResolvedValue([]),
  takedownPublication: vi.fn().mockResolvedValue(undefined),
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
  version: 10000,
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
  const onReload = vi.fn()
  const onConfigStatusChange = vi.fn()
  const renderResult = render(
    <AppPublicationSection
      teamId={3}
      appId={detail.appId ?? 'a1'}
      detail={detail}
      canManage={canManage}
      configStatus={configStatus}
      onConfigStatusChange={onConfigStatusChange}
      onReload={onReload}
    />,
  )
  return { ...renderResult, onReload, onConfigStatusChange }
}

describe('AppPublicationSection（上架管理分区）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTeamPublicationList).mockResolvedValue([])
  })

  it('未公开且无申请：展示未申请状态，申请上架无需填写版本号（自动记录当前发布版本）', async () => {
    const onReload = vi.fn()
    render(
      <AppPublicationSection teamId={3} appId="a1" detail={DETAIL} canManage configStatus={1} onReload={onReload} />,
    )

    expect(await screen.findByText('未申请')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: /申请上架/ }))
    // 无版本号输入框：上架把最新的团队发布上架，版本号自动记录应用当前发布版本
    expect(await screen.findByText(/上架自动记录应用当前发布版本/)).toBeTruthy()
    expect(screen.queryByPlaceholderText('如 1.0.3')).toBeNull()
    const reason = await screen.findByPlaceholderText(/上架理由/)
    fireEvent.change(reason, { target: { value: '对外提供客服问答' } })
    fireEvent.click(screen.getByRole('button', { name: /提交申请/ }))

    await waitFor(() => {
      expect(applyPublication).toHaveBeenCalledWith({
        resourceType: 'app',
        resourceId: 'a1',
        applyReason: '对外提供客服问答',
      })
      expect(onReload).toHaveBeenCalled()
    })
  })

  it('团队应用卡未发布：提供「发布」按钮，弹窗自动预填下一版本号，确认后携带版本发布', async () => {
    const { onReload, onConfigStatusChange } = renderSection({ ...DETAIL, publishStatus: 0, publishTime: undefined }, true, null)

    expect(await screen.findByText('未发布')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /^发\s*布$/ }))
    const versionInput = (await screen.findByLabelText('应用版本')) as HTMLInputElement
    // 当前版本 1.0.0（int 10000）→ 自动填入下一版本 1.0.1（int 10001）
    expect(versionInput.value).toBe('1.0.1')
    fireEvent.click(screen.getByRole('button', { name: /确\s*定/ }))

    await waitFor(() => {
      expect(publishApp).toHaveBeenCalledWith('a1', '1.0.1')
      expect(onConfigStatusChange).toHaveBeenCalledWith(1)
      expect(onReload).toHaveBeenCalled()
    })
  })

  it('发布弹窗版本号相同或更低时就地拦截（版本号必须严格递增）', async () => {
    renderSection({ ...DETAIL, publishStatus: 0, publishTime: undefined, version: 10000 })

    fireEvent.click(await screen.findByRole('button', { name: /^发\s*布$/ }))
    // 预填为下一版本 1.0.1，手动改回当前版本 1.0.0（相同）→ 拦截
    const versionInput = await screen.findByLabelText('应用版本')
    expect((versionInput as HTMLInputElement).value).toBe('1.0.1')
    fireEvent.change(versionInput, { target: { value: '1.0.0' } })
    fireEvent.click(screen.getByRole('button', { name: /确\s*定/ }))

    expect(await screen.findByText(/版本号必须大于当前版本/)).toBeTruthy()
    expect(publishApp).not.toHaveBeenCalled()

    // 更小版本同样拦截
    fireEvent.change(versionInput, { target: { value: '0.0.1' } })
    fireEvent.click(screen.getByRole('button', { name: /确\s*定/ }))
    expect(await screen.findByText(/版本号必须大于当前版本/)).toBeTruthy()
    expect(publishApp).not.toHaveBeenCalled()
  })

  it('已发布：团队应用卡常驻提供「重新发布」与「撤回发布」（重新发布幂等，不依赖草稿状态）', async () => {
    // 草稿有未发布变更（configStatus=0）与未知（null）/一致（1）均提供重新发布入口
    const r0 = renderSection({ ...DETAIL }, true, 0)
    expect(await screen.findByText('已发布')).toBeTruthy()
    expect(screen.getByRole('button', { name: /重新发布/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /撤回发布/ })).toBeTruthy()
    r0.unmount()

    const r1 = renderSection({ ...DETAIL }, true, 1)
    expect(await screen.findByText('已发布')).toBeTruthy()
    expect(screen.getByRole('button', { name: /重新发布/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /撤回发布/ })).toBeTruthy()
    r1.unmount()

    const r2 = renderSection({ ...DETAIL }, true, null)
    expect(await screen.findByText('已发布')).toBeTruthy()
    expect(screen.getByRole('button', { name: /重新发布/ })).toBeTruthy()
    expect(screen.getByRole('button', { name: /撤回发布/ })).toBeTruthy()
    r2.unmount()
  })

  it('审核中：展示待审核标签并可撤回申请，撤回后刷新申请记录与工作台数据', async () => {
    vi.mocked(getTeamPublicationList).mockResolvedValue([
      record({ publicationId: '8', state: 'pending', reviewComment: null, reviewTime: null, updateUserName: null }),
      record({ publicationId: '7', state: 'rejected' }),
    ])
    const { onReload } = renderSection()

    expect(await screen.findByText('审核中')).toBeTruthy()

    // antd 两字按钮会插入空格（“确 定”），沿用仓库既有的 popconfirm 主按钮选择器
    fireEvent.click(screen.getAllByRole('button', { name: /撤回申请/ })[0])
    fireEvent.click(document.querySelector('.ant-popconfirm-buttons .ant-btn-primary') as HTMLElement)

    await waitFor(() => {
      expect(withdrawPublication).toHaveBeenCalledWith('8')
      // 与上架管理联动的操作完成后双刷新：申请记录 + 工作台详情（挂载时 1 次 + 撤回后 1 次）
      expect(getTeamPublicationList).toHaveBeenCalledTimes(2)
      expect(onReload).toHaveBeenCalled()
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

  it('版本与草稿：展示线上版本号与当前版本号，版本未随发布生效时给出提示', async () => {
    renderSection({ ...DETAIL, version: 10203, publishVersion: 10000 }, true, 1)

    // 线上=1.0.0（发布快照），当前版本=1.2.3
    expect(await screen.findByText('v1.0.0')).toBeTruthy()
    expect(screen.getByText('v1.2.3')).toBeTruthy()
    expect(screen.getByText('版本未发布')).toBeTruthy()
  })

  it('已发布：撤回发布确认后调用接口并刷新工作台', async () => {
    const onReload = vi.fn()
    render(
      <AppPublicationSection teamId={3} appId="a1" detail={DETAIL} canManage configStatus={1} onReload={onReload} />,
    )

    fireEvent.click(await screen.findByRole('button', { name: /撤回发布/ }))
    // antd 两字按钮会插入空格（“确 定”），沿用仓库既有的 popconfirm 主按钮选择器
    fireEvent.click(document.querySelector('.ant-popconfirm-buttons .ant-btn-primary') as HTMLElement)

    await waitFor(() => {
      expect(unpublishApp).toHaveBeenCalledWith('a1')
      // 撤回发布联动撤下待审申请：申请记录与工作台详情一并刷新
      expect(getTeamPublicationList).toHaveBeenCalledTimes(2)
      expect(onReload).toHaveBeenCalled()
    })
  })

  it('已公开：上架管理（应用市场）展示发布版本号与下架入口，确认后调用下架接口并刷新', async () => {
    const onReload = vi.fn()
    vi.mocked(getTeamPublicationList).mockResolvedValue([
      record({ state: 'approved', reviewComment: null, publishVersion: 10200 }),
    ])
    render(
      <AppPublicationSection
        teamId={3}
        appId="a1"
        detail={{ ...DETAIL, isPublic: true }}
        canManage
        configStatus={1}
        onReload={onReload}
      />,
    )

    // 卡片顺序：团队应用在前、上架管理（应用市场）随后
    expect(await screen.findByText('团队应用')).toBeTruthy()
    expect(screen.getByText('上架管理（应用市场）')).toBeTruthy()
    expect(await screen.findByText('已公开')).toBeTruthy()
    // 市场发布版本号取最近一次审批通过的上架申请版本（10200 = 1.2.0），同时出现在申请记录行
    expect(screen.getByText('发布版本号')).toBeTruthy()
    expect((await screen.findAllByText('v1.2.0')).length).toBeGreaterThanOrEqual(2)
    expect(screen.queryByRole('button', { name: /申请上架/ })).toBeNull()
    fireEvent.click(await screen.findByRole('button', { name: /下\s*架/ }))
    fireEvent.click(document.querySelector('.ant-popconfirm-buttons .ant-btn-primary') as HTMLElement)

    await waitFor(() => {
      expect(takedownPublication).toHaveBeenCalledWith({ resourceType: 'app', resourceId: 'a1' })
      expect(onReload).toHaveBeenCalled()
    })
  })

  it('成员只读：已公开时可见状态但无下架与发布入口', async () => {
    renderSection({ ...DETAIL, isPublic: true }, false)

    expect(await screen.findByText('已公开')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /下\s*架/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /撤回发布/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /^发\s*布$/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /重新发布/ })).toBeNull()
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
    expect(screen.queryByRole('button', { name: /撤回发布/ })).toBeNull()
  })
})
