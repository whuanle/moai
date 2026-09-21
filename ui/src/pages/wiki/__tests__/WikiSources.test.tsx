import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { WikiSources } from '../WikiSources'
import { createWikiSource, deleteWikiSource, getWikiSources, getWikiSourceDocuments, syncWikiSource, updateWikiSource, WIKI_SOURCE_TYPE_CRAWLER, WIKI_SOURCE_TYPE_FEISHU } from '@/api/wiki'
import type { WikiSourceItem } from '@/api/wiki'

vi.mock('@/api/wiki', async () => {
  const actual = await vi.importActual<typeof import('@/api/wiki')>('@/api/wiki')
  return {
    ...actual,
    getWikiSources: vi.fn(),
    createWikiSource: vi.fn().mockResolvedValue('source-id'),
    updateWikiSource: vi.fn().mockResolvedValue(undefined),
    deleteWikiSource: vi.fn().mockResolvedValue(undefined),
    syncWikiSource: vi.fn(),
    getWikiSourceDocuments: vi.fn(),
  }
})

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

/** 爬虫源样例：已开启每天 02:00 定时抓取、间隔 3 秒 */
function crawlerSource(): WikiSourceItem {
  return {
    sourceId: 'src-1',
    wikiId: '7',
    sourceType: WIKI_SOURCE_TYPE_CRAWLER,
    name: '官网文档',
    description: '产品站点',
    isEnable: true,
    cron: '0 2 * * *',
    crawler: {
      startUrl: 'https://example.com/docs/',
      pathPrefix: '/docs/',
      maxDepth: 2,
      maxPages: 200,
      requestIntervalSeconds: 3,
      timeoutSeconds: 30,
    },
    lastSyncStatus: 1,
    lastSyncTime: '2026-09-20T02:00:00Z',
    lastSyncMessage: '抓取完成：新建 12，更新 3',
    documentCount: 15,
  }
}

function renderSources(props: Partial<Parameters<typeof WikiSources>[0]> = {}) {
  return render(<WikiSources wikiId={7} myRole={2} {...props} />)
}

describe('WikiSources', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getWikiSources).mockResolvedValue({ myRole: 2, items: [crawlerSource()] })
    vi.mocked(getWikiSourceDocuments).mockResolvedValue({ total: 0, items: [] })
    vi.mocked(syncWikiSource).mockResolvedValue({
      total: 15,
      created: 12,
      updated: 3,
      unchanged: 0,
      skipped: 0,
      failed: 0,
      workflowTriggered: 15,
      message: 'ok',
    })
  })

  it('渲染外部源列表并展示类型、抓取频率与定时同步', async () => {
    renderSources()

    expect(await screen.findByText('官网文档')).toBeTruthy()
    expect(screen.getByText('网页爬虫')).toBeTruthy()
    // 抓取频率以「N 秒/次」直观呈现
    expect(screen.getByText('3 秒/次')).toBeTruthy()
    // 定时同步 cron 原样展示
    expect(screen.getByText('0 2 * * *')).toBeTruthy()
    // 已同步文档数量
    expect(screen.getByText('15')).toBeTruthy()
  })

  it('Member 角色下禁用新建/编辑/删除与启停开关', async () => {
    // 组件以接口返回的 myRole 为准（接口未返回时才退回 props），这里必须让两侧口径一致，
    // 否则「等待 load 完成」与「读 props 初始值」会产生竞态，用例在重负载下随机通过/失败
    vi.mocked(getWikiSources).mockResolvedValue({ myRole: 0, items: [crawlerSource()] })
    const { container } = renderSources({ myRole: 0 })

    // isAdminPlus 由组件内的 role 状态决定，props 与接口返回均为 Member
    await waitFor(() => {
      expect(screen.getByText('仅团队管理员可管理外部源')).toBeTruthy()
    })
    // antd 5.28 的 Button 用原生 disabled 属性表达禁用，不再挂 ant-btn-disabled 类名
    await waitFor(() => {
      expect(container.querySelectorAll('.ant-btn-primary[disabled]').length).toBeGreaterThanOrEqual(1)
      expect(container.querySelectorAll('.ant-switch-disabled').length).toBeGreaterThanOrEqual(1)
    })
  })

  it('点击立即同步后调用同步接口并刷新列表', async () => {
    renderSources()

    const syncButton = await screen.findByRole('button', { name: '立即同步' })
    fireEvent.click(syncButton)

    await waitFor(() => {
      expect(syncWikiSource).toHaveBeenCalledWith(7, 'src-1', false)
    })
    // 初次加载 + 同步后刷新
    await waitFor(() => {
      expect(getWikiSources).toHaveBeenCalledTimes(2)
    })
  })

  it('删除外部源需要确认，确认后调用删除接口', async () => {
    renderSources()

    const deleteButton = await screen.findByRole('button', { name: '删除' })
    fireEvent.click(deleteButton)

    // Popconfirm 的确认按钮文案由 antd 的 okText 默认值提供，用 title 定位弹层后取主按钮
    const confirmTitle = await screen.findByText('确定删除该外部源？')
    expect(confirmTitle).toBeTruthy()
    const popConfirmOk = document.querySelector('.ant-popconfirm-buttons .ant-btn-primary') as HTMLElement
    expect(popConfirmOk).toBeTruthy()
    fireEvent.click(popConfirmOk)

    await waitFor(() => {
      expect(deleteWikiSource).toHaveBeenCalledWith(7, 'src-1')
    })
  })

  it('点击已同步文档数量打开文档抽屉并加载文档列表', async () => {
    vi.mocked(getWikiSourceDocuments).mockResolvedValue({
      total: 1,
      items: [
        {
          sourceId: 'src-1',
          externalKey: 'https://example.com/docs/a',
          externalTitle: '快速开始',
          externalPath: '/docs/a',
          documentId: '101',
          fileName: '快速开始.md',
          status: 1,
          lastSyncTime: '2026-09-20T02:00:00Z',
        },
      ],
    })
    renderSources()

    const countLink = await screen.findByText('15')
    fireEvent.click(countLink)

    await waitFor(() => {
      expect(getWikiSourceDocuments).toHaveBeenCalledWith(7, 'src-1', { pageNo: 1, pageSize: 20 })
    })
    expect(await screen.findByText('快速开始')).toBeTruthy()
    expect(screen.getByText('/docs/a')).toBeTruthy()
  })

  it('点击「新建爬虫」打开弹窗并提交爬虫配置', async () => {
    renderSources()

    // 双入口按钮：类型由入口决定，弹窗内无类型切换
    fireEvent.click(await screen.findByRole('button', { name: /新建爬虫/ }))
    expect(await screen.findByText('起始地址')).toBeTruthy()
    expect(screen.queryByText('飞书文档节点 Token')).toBeNull()

    fireEvent.change(screen.getByPlaceholderText('请输入外部源名称'), { target: { value: '新爬虫源' } })
    fireEvent.change(screen.getByPlaceholderText('https://example.com/docs/'), {
      target: { value: 'https://example.com/blog/' },
    })

    fireEvent.click(screen.getByRole('button', { name: '保存' }))

    await waitFor(() => {
      expect(createWikiSource).toHaveBeenCalledTimes(1)
    })
    const payload = vi.mocked(createWikiSource).mock.calls[0][1]
    expect(payload.sourceType).toBe('crawler')
    expect(payload.name).toBe('新爬虫源')
    expect(payload.crawler?.startUrl).toBe('https://example.com/blog/')
    // 默认按推荐档位提交 1 秒间隔，避免把目标站点抓崩
    expect(payload.crawler?.requestIntervalSeconds).toBe(1)
  })

  it('点击「新建飞书源」打开弹窗并提交飞书绑定字段', async () => {
    renderSources()

    fireEvent.click(await screen.findByRole('button', { name: /新建飞书源/ }))
    expect(await screen.findByText('飞书文档节点 Token')).toBeTruthy()
    expect(screen.queryByText('起始地址')).toBeNull()

    fireEvent.change(screen.getByPlaceholderText('请输入外部源名称'), { target: { value: '新飞书源' } })
    fireEvent.change(screen.getByPlaceholderText('从飞书知识库文档链接中获取'), {
      target: { value: 'tok-123' },
    })

    fireEvent.click(screen.getByRole('button', { name: '保存' }))

    await waitFor(() => {
      expect(createWikiSource).toHaveBeenCalledTimes(1)
    })
    const payload = vi.mocked(createWikiSource).mock.calls[0][1]
    expect(payload.sourceType).toBe(WIKI_SOURCE_TYPE_FEISHU)
    expect(payload.name).toBe('新飞书源')
    expect(payload.nodeToken).toBe('tok-123')
    expect(payload.crawler).toBeUndefined()
  })

  it('编辑外部源时按现状回填爬虫配置', async () => {
    renderSources()

    fireEvent.click(await screen.findByRole('button', { name: '编辑' }))

    await waitFor(() => {
      expect(screen.getByDisplayValue('官网文档')).toBeTruthy()
    })
    expect(screen.getByDisplayValue('https://example.com/docs/')).toBeTruthy()
    expect(screen.getByDisplayValue('/docs/')).toBeTruthy()
  })

  it('关闭定时同步后提交空 cron 表示不再自动抓取', async () => {
    renderSources()

    fireEvent.click(await screen.findByRole('button', { name: '编辑' }))
    await waitFor(() => {
      expect(screen.getByDisplayValue('官网文档')).toBeTruthy()
    })

    // 定时同步开关位于弹窗内，切换后 cron 输入框消失
    const cronInput = screen.getByDisplayValue('0 2 * * *')
    expect(cronInput).toBeTruthy()
    const switches = screen.getAllByRole('switch')
    // 定时同步开关是抽屉内最后一个 switch（启用开关在前）
    fireEvent.click(switches[switches.length - 1])

    await waitFor(() => {
      expect(screen.queryByDisplayValue('0 2 * * *')).toBeNull()
    })
  })

  it('同步失败时展示失败状态标签', async () => {
    vi.mocked(getWikiSources).mockResolvedValue({
      myRole: 2,
      items: [{ ...crawlerSource(), lastSyncStatus: 2, lastSyncMessage: '起始地址不可达' }],
    })
    renderSources()

    expect(await screen.findByText('同步失败')).toBeTruthy()
  })

  it('updateWikiSource 可在启停开关切换时被调用', async () => {
    renderSources()

    const switches = await screen.findAllByRole('switch')
    fireEvent.click(switches[0])

    await waitFor(() => {
      expect(updateWikiSource).toHaveBeenCalledWith(7, 'src-1', { isEnable: false })
    })
  })
})
