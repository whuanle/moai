import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { RequestInformation } from '@microsoft/kiota-abstractions'
import { pluginMarketplaceApi } from '@/api/plugin'

const { sendPrimitiveMock } = vi.hoisted(() => ({
  sendPrimitiveMock: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({
  createAiPluginRequestAdapter: () => ({ sendPrimitive: sendPrimitiveMock }),
  getAiPluginClient: vi.fn(),
  getApiClient: vi.fn(),
}))

/** 记录本次请求地址，并按传入的 JSON 载荷返回字节流. */
function mockResponse(payload: unknown) {
  const urls: string[] = []
  sendPrimitiveMock.mockImplementation(async (request: RequestInformation) => {
    urls.push(request.URL)
    return new TextEncoder().encode(JSON.stringify(payload)).buffer
  })
  return urls
}

describe('pluginMarketplaceApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('在线插件目录把关键字、分页与分类拼进查询串（Kiota 显式 URL 不会展开 queryParameters）', async () => {
    const urls = mockResponse({ items: [], total: 0 })

    await pluginMarketplaceApi.getCatalog('飞书', 3, 20, '12')

    expect(urls).toHaveLength(1)
    const url = new URL(urls[0])
    expect(url.pathname).toBe('/api/ai/plugin/online/catalog')
    expect(url.searchParams.get('keyword')).toBe('飞书')
    expect(url.searchParams.get('pageNo')).toBe('3')
    expect(url.searchParams.get('pageSize')).toBe('20')
    expect(url.searchParams.get('categoryId')).toBe('12')
  })

  it('空关键字与空分类不下发查询参数', async () => {
    const urls = mockResponse({ items: [], total: 0 })

    await pluginMarketplaceApi.getCatalog('', 1, 20, '')

    const url = new URL(urls[0])
    expect(url.searchParams.has('keyword')).toBe(false)
    expect(url.searchParams.has('categoryId')).toBe(false)
    expect(url.searchParams.get('pageNo')).toBe('1')
  })

  it('分类列表仍返回解析后的 JSON', async () => {
    mockResponse([{ id: '12', name: 'Search', emoji: '🔎' }])

    await expect(pluginMarketplaceApi.getCategories()).resolves.toEqual([
      { id: '12', name: 'Search', emoji: '🔎' },
    ])
  })
})
