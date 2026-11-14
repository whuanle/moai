import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { App } from 'antd'
import { FeedbackBridge } from '@/design-system'
import { PluginMarketplacePanel } from '../PluginMarketplacePanel'
import { pluginMarketplaceApi } from '@/api/plugin'

vi.mock('@/api/plugin', () => ({
  pluginMarketplaceApi: {
    getCatalog: vi.fn(),
    getCategories: vi.fn(),
    getInstalled: vi.fn(),
    install: vi.fn(),
    setEnabled: vi.fn(),
  },
}))

function renderMarketplace() {
  return render(
    <App>
      <FeedbackBridge />
      <PluginMarketplacePanel />
    </App>,
  )
}

describe('PluginMarketplacePanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(pluginMarketplaceApi.getCatalog).mockResolvedValue({
      items: [{
        pluginId: '44', key: 'demo.search', name: 'Demo Search', summary: 'Search tools',
        iconUrl: '', latestVersion: '1.2.0', downloadCount: '8', isInstalled: false,
        categoryId: '12', categoryName: 'Search', categoryEmoji: '🔎',
      }],
      total: 1,
    })
    vi.mocked(pluginMarketplaceApi.getCategories).mockResolvedValue([
      { id: '12', name: 'Search', emoji: '🔎' },
    ])
    vi.mocked(pluginMarketplaceApi.getInstalled).mockResolvedValue([])
    vi.mocked(pluginMarketplaceApi.install).mockResolvedValue({
      pluginKey: 'demo.search', name: 'Demo Search', summary: 'Search tools', description: '',
      avatarPath: '', version: '1.2.0', packageSha256: 'abc', enabled: true,
    })
    vi.mocked(pluginMarketplaceApi.setEnabled).mockResolvedValue({
      pluginKey: 'demo.search', name: 'Demo Search', summary: 'Search tools', description: '',
      avatarPath: '', version: '1.2.0', packageSha256: 'abc', enabled: false,
    })
  })

  it('shows the searchable marketplace as plugin cards without installed tabs or refresh controls', async () => {
    renderMarketplace()

    expect(await screen.findByText('Demo Search')).toBeInTheDocument()
    expect(await screen.findAllByText('🔎 Search')).toHaveLength(2)
    expect(screen.queryByRole('tab', { name: /插件市场|Marketplace|已安装|Installed/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /刷新|Refresh/i })).not.toBeInTheDocument()
    expect(pluginMarketplaceApi.getInstalled).not.toHaveBeenCalled()
  })

  it('filters the catalog using the MoPlatform category id', async () => {
    const user = userEvent.setup()
    renderMarketplace()

    const categoryLabels = await screen.findAllByText('🔎 Search')
    await user.click(categoryLabels[0])

    await waitFor(() => expect(pluginMarketplaceApi.getCatalog).toHaveBeenLastCalledWith('', 1, 20, '12'))
  })

  it('installs a published package from its card', async () => {
    const user = userEvent.setup()
    renderMarketplace()

    await user.click(await screen.findByRole('button', { name: '安装 Demo Search' }))

    await waitFor(() => expect(pluginMarketplaceApi.install).toHaveBeenCalledWith('44'))
    expect(pluginMarketplaceApi.getInstalled).not.toHaveBeenCalled()
  })

  it('installs multiple selected packages sequentially and continues after an individual failure', async () => {
    const user = userEvent.setup()
    vi.mocked(pluginMarketplaceApi.getCatalog).mockResolvedValue({
      items: [
        {
          pluginId: '44', key: 'demo.search', name: 'Demo Search', summary: 'Search tools',
          iconUrl: '', latestVersion: '1.2.0', downloadCount: '8', isInstalled: false,
          categoryId: '12', categoryName: 'Search', categoryEmoji: '🔎',
        },
        {
          pluginId: '45', key: 'demo.weather', name: 'Demo Weather', summary: 'Weather tools',
          iconUrl: '', latestVersion: '2.0.0', downloadCount: '3', isInstalled: false,
          categoryId: '12', categoryName: 'Search', categoryEmoji: '🔎',
        },
      ],
      total: 2,
    })
    vi.mocked(pluginMarketplaceApi.install)
      .mockRejectedValueOnce(new Error('package unavailable'))
      .mockResolvedValueOnce({
        pluginKey: 'demo.weather', name: 'Demo Weather', summary: 'Weather tools', description: '',
        avatarPath: '', version: '2.0.0', packageSha256: 'def', enabled: true,
      })
    renderMarketplace()

    await user.click(await screen.findByRole('checkbox', { name: '全选本页未安装插件' }))
    await user.click(screen.getByRole('button', { name: '安装所选（2）' }))

    await waitFor(() => expect(pluginMarketplaceApi.install).toHaveBeenCalledTimes(2))
    expect(pluginMarketplaceApi.install).toHaveBeenNthCalledWith(1, '44')
    expect(pluginMarketplaceApi.install).toHaveBeenNthCalledWith(2, '45')
    expect(await screen.findByRole('button', { name: '安装所选（1）' })).toBeInTheDocument()
  })
})
