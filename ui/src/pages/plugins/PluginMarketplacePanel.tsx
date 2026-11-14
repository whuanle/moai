import { useCallback, useEffect, useMemo, useState } from 'react'
import { DownloadOutlined, SearchOutlined } from '@ant-design/icons'
import { Avatar, Button, Card, Checkbox, Col, Empty, Image, Input, Pagination, Row, Space, Spin, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { PageToolbar, useFeedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  pluginMarketplaceApi,
  type OnlinePluginCategory,
  type OnlinePluginItem,
} from '@/api/plugin'

const { Text } = Typography

export function PluginMarketplacePanel() {
  const { t } = useTranslation()
  const feedback = useFeedback()
  const [catalog, setCatalog] = useState<OnlinePluginItem[]>([])
  const [categories, setCategories] = useState<OnlinePluginCategory[]>([])
  const [catalogLoading, setCatalogLoading] = useState(false)
  const [categoriesLoading, setCategoriesLoading] = useState(false)
  const [busyKey, setBusyKey] = useState('')
  const [selectedPluginIds, setSelectedPluginIds] = useState<Set<string>>(() => new Set())
  const [batchInstalling, setBatchInstalling] = useState(false)
  const [searchText, setSearchText] = useState('')
  const [keyword, setKeyword] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [pageNo, setPageNo] = useState(1)
  const [total, setTotal] = useState(0)
  const pageSize = 20
  const installableItems = useMemo(() => catalog.filter((item) => !item.isInstalled), [catalog])
  const selectedInstallableItems = useMemo(
    () => installableItems.filter((item) => selectedPluginIds.has(item.pluginId)),
    [installableItems, selectedPluginIds],
  )
  const selectedAllOnPage = installableItems.length > 0 && installableItems.every((item) => selectedPluginIds.has(item.pluginId))
  const selectedSomeOnPage = installableItems.some((item) => selectedPluginIds.has(item.pluginId))

  const loadCatalog = useCallback(async () => {
    setCatalogLoading(true)
    try {
      const response = await pluginMarketplaceApi.getCatalog(keyword, pageNo, pageSize, categoryId)
      setCatalog(response.items ?? [])
      setTotal(Number(response.total ?? 0))
    } catch {
      // 错误由 Kiota 中间件统一反馈
    } finally {
      setCatalogLoading(false)
    }
  }, [categoryId, keyword, pageNo])

  const loadCategories = useCallback(async () => {
    setCategoriesLoading(true)
    try {
      const response = await pluginMarketplaceApi.getCategories()
      setCategories(response ?? [])
    } catch {
      // 错误由 Kiota 中间件统一反馈
    } finally {
      setCategoriesLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadCatalog()
  }, [loadCatalog])

  useEffect(() => {
    void loadCategories()
  }, [loadCategories])

  const handleInstall = useCallback(async (item: OnlinePluginItem) => {
    setBusyKey(item.key)
    try {
      const result = await pluginMarketplaceApi.install(item.pluginId)
      feedback.success(result.enabled ? t('plugins.online.installSuccess') : t('plugins.online.installedOffline'))
      setSelectedPluginIds((current) => {
        const next = new Set(current)
        next.delete(item.pluginId)
        return next
      })
      await loadCatalog()
    } catch {
      // 错误由 Kiota 中间件统一反馈
    } finally {
      setBusyKey('')
    }
  }, [feedback, loadCatalog, t])

  const handleBatchInstall = useCallback(async () => {
    if (selectedInstallableItems.length === 0 || batchInstalling) return

    setBatchInstalling(true)
    let installedCount = 0
    let failedCount = 0
    let offlineCount = 0
    const failedPluginIds = new Set<string>()
    try {
      // 安装会下载并加载程序集，按顺序调用单插件接口，避免并发写包目录和运行时注册表。
      for (const item of selectedInstallableItems) {
        try {
          const result = await pluginMarketplaceApi.install(item.pluginId)
          installedCount += 1
          if (!result.enabled) offlineCount += 1
        } catch {
          failedCount += 1
          failedPluginIds.add(item.pluginId)
          // 单项 API 错误已由 Kiota 统一提示，继续尝试其余选中项。
        }
      }

      if (installedCount > 0 && failedCount === 0) {
        feedback.success(t('plugins.online.batchInstallSuccess', { count: installedCount, offline: offlineCount }))
      } else if (installedCount > 0 || failedCount > 0) {
        feedback.warning(t('plugins.online.batchInstallPartial', { success: installedCount, failed: failedCount, offline: offlineCount }))
      }

      setSelectedPluginIds(failedPluginIds)
      await loadCatalog()
    } finally {
      setBatchInstalling(false)
    }
  }, [batchInstalling, feedback, loadCatalog, selectedInstallableItems, t])

  const handleSearch = () => {
    const nextKeyword = searchText.trim()
    if (nextKeyword === keyword && pageNo === 1) {
      setSelectedPluginIds(new Set())
      void loadCatalog()
      return
    }
    setSelectedPluginIds(new Set())
    setPageNo(1)
    setKeyword(nextKeyword)
  }

  return (
    <>
      <PageToolbar
        filters={<Input value={searchText} onChange={(event) => setSearchText(event.target.value)} onPressEnter={handleSearch} allowClear maxLength={100} placeholder={t('plugins.online.searchPlaceholder')} style={{ width: 260 }} />}
        queries={<Button icon={<SearchOutlined />} loading={catalogLoading} onClick={handleSearch}>{t('plugins.online.search')}</Button>}
        actions={(
          <Space size="middle">
            <Checkbox
              checked={selectedAllOnPage}
              indeterminate={selectedSomeOnPage && !selectedAllOnPage}
              disabled={catalogLoading || batchInstalling || installableItems.length === 0}
              onChange={(event) => setSelectedPluginIds(event.target.checked ? new Set(installableItems.map((item) => item.pluginId)) : new Set())}
            >
              {t('plugins.online.selectAllInstallable')}
            </Checkbox>
            <Button
              type="primary"
              icon={<DownloadOutlined />}
              aria-label={t('plugins.online.installSelected', { count: selectedInstallableItems.length })}
              loading={batchInstalling}
              disabled={selectedInstallableItems.length === 0 || Boolean(busyKey)}
              onClick={() => void handleBatchInstall()}
            >
              {t('plugins.online.installSelected', { count: selectedInstallableItems.length })}
            </Button>
          </Space>
        )}
      />
      <Spin spinning={categoriesLoading}>
        <Space size={4} wrap style={{ marginBottom: spacing.md }}>
          <Tag.CheckableTag
            checked={!categoryId}
            onChange={() => {
              setCategoryId('')
              setSelectedPluginIds(new Set())
              setPageNo(1)
            }}
          >
            {t('plugins.online.categoryAll')}
          </Tag.CheckableTag>
          {categories.map((category) => (
            <Tag.CheckableTag
              key={category.id}
              checked={categoryId === category.id}
              onChange={() => {
                setCategoryId(category.id)
                setSelectedPluginIds(new Set())
                setPageNo(1)
              }}
            >
              {[category.emoji, category.name].filter(Boolean).join(' ')}
            </Tag.CheckableTag>
          ))}
        </Space>
      </Spin>
      <Spin spinning={catalogLoading}>
        {catalog.length === 0 && !catalogLoading ? (
          <Empty description={t('plugins.online.emptyCatalog')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {catalog.map((item) => (
              <Col key={item.pluginId} xs={24} sm={12} lg={8} xxl={6}>
                <Card
                  hoverable
                  style={{ height: '100%' }}
                  styles={{ body: { padding: spacing.md, display: 'flex', flexDirection: 'column', height: '100%' } }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                    {!item.isInstalled ? (
                      <Checkbox
                        checked={selectedPluginIds.has(item.pluginId)}
                        disabled={batchInstalling || Boolean(busyKey)}
                        aria-label={t('plugins.online.selectPlugin', { name: item.name || item.key })}
                        onChange={(event) => {
                          setSelectedPluginIds((current) => {
                            const next = new Set(current)
                            if (event.target.checked) next.add(item.pluginId)
                            else next.delete(item.pluginId)
                            return next
                          })
                        }}
                      />
                    ) : null}
                    {item.iconUrl ? <Image src={item.iconUrl} width={44} height={44} preview={false} alt={item.name} /> : <Avatar shape="square" size={44}>{(item.name || item.key).slice(0, 1)}</Avatar>}
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <Text strong ellipsis={{ tooltip: item.name || item.key }} style={{ display: 'block' }}>{item.name || item.key}</Text>
                      <Text type="secondary" ellipsis={{ tooltip: item.key }} style={{ display: 'block', fontFamily: 'monospace', fontSize: 12 }}>{item.key}</Text>
                    </div>
                  </div>
                  <Text
                    type="secondary"
                    style={{ marginTop: spacing.sm, minHeight: 44, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}
                    title={item.summary}
                  >
                    {item.summary || '-'}
                  </Text>
                  <Space size={4} wrap style={{ marginTop: spacing.sm }}>
                    {item.categoryName ? <Tag>{[item.categoryEmoji, item.categoryName].filter(Boolean).join(' ')}</Tag> : null}
                    <Tag>{t('plugins.online.versionLabel', { version: item.latestVersion || '-' })}</Tag>
                    <Tag>{t('plugins.online.downloadsLabel', { count: Number(item.downloadCount || 0) })}</Tag>
                  </Space>
                  <div style={{ marginTop: 'auto', paddingTop: spacing.sm, display: 'flex', justifyContent: 'flex-end' }}>
                    {item.isInstalled ? (
                      <Tag color="green">{t('plugins.online.installed')}</Tag>
                    ) : (
                      <Button type="link" size="small" icon={<DownloadOutlined />} aria-label={t('plugins.online.installPlugin', { name: item.name || item.key })} loading={busyKey === item.key} disabled={batchInstalling} onClick={() => void handleInstall(item)}>
                        {t('plugins.online.install')}
                      </Button>
                    )}
                  </div>
                </Card>
              </Col>
            ))}
          </Row>
        )}
      </Spin>
      {total > pageSize ? (
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: spacing.md }}>
          <Pagination current={pageNo} pageSize={pageSize} total={total} onChange={(page) => { setSelectedPluginIds(new Set()); setPageNo(page) }} showSizeChanger={false} />
        </div>
      ) : null}
    </>
  )
}
