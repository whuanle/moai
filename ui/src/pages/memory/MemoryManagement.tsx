import { useCallback, useEffect, useState } from 'react'
import { ArrowLeftOutlined, DeleteOutlined, SearchOutlined } from '@ant-design/icons'
import { Button, Input, Popconfirm, Space, Tabs, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DataTable, Page, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  clearMyAppMemory,
  clearMyGlobalMemory,
  deleteMyGlobalMemory,
  deleteMyMemory,
  getMyGlobalMemories,
  getMyMemories,
  getMyMemoryOverview,
  type MyMemoryAppItem,
  type MyMemoryItem,
} from '@/api/memory'

const { Text } = Typography

const PAGE_SIZE = 20

/** 记忆类型常量（对齐后端 AgentMemorySettings.Type*） */
const TYPE_EPISODIC = 1
const TYPE_PROCEDURAL = 2

/**
 * 记忆管理页（个人跨应用视图）：「应用记忆」按应用查看/删除/清空本人记忆；
 * 「全局记忆」删除/清空个人跨应用记忆。记忆内容由 AI 自动维护
 * （memory_save 工具与会话结束自动提取），页面不提供人工新建/编辑/存为全局.
 */
export function MemoryManagement() {
  const { t } = useTranslation()

  const [activeTab, setActiveTab] = useState<'apps' | 'global'>('apps')

  const [overviewLoading, setOverviewLoading] = useState(false)
  const [apps, setApps] = useState<MyMemoryAppItem[]>([])
  const [globalMyCount, setGlobalMyCount] = useState(0)

  /** 应用记忆 Tab 工具栏搜索（应用名/团队名，前端过滤） */
  const [appsKeyword, setAppsKeyword] = useState('')

  const [selectedApp, setSelectedApp] = useState<MyMemoryAppItem | null>(null)
  const [appMemories, setAppMemories] = useState<MyMemoryItem[]>([])
  const [appTotal, setAppTotal] = useState(0)
  const [appPage, setAppPage] = useState(1)
  const [appMemoryEnabled, setAppMemoryEnabled] = useState(true)
  const [appMemoriesLoading, setAppMemoriesLoading] = useState(false)
  /** 应用记忆明细搜索（按内容，服务端过滤）：input 为输入框实时值，keyword 为已提交生效值 */
  const [appMemKeywordInput, setAppMemKeywordInput] = useState('')
  const [appMemKeyword, setAppMemKeyword] = useState('')

  const [globalMemories, setGlobalMemories] = useState<MyMemoryItem[]>([])
  const [globalTotal, setGlobalTotal] = useState(0)
  const [globalPage, setGlobalPage] = useState(1)
  const [globalLoading, setGlobalLoading] = useState(false)
  /** 全局记忆搜索（按内容，服务端过滤）：input 为输入框实时值，keyword 为已提交生效值 */
  const [globalKeywordInput, setGlobalKeywordInput] = useState('')
  const [globalKeyword, setGlobalKeyword] = useState('')

  const loadOverview = useCallback(async () => {
    setOverviewLoading(true)
    try {
      const res = await getMyMemoryOverview()
      setApps(res.apps)
      setGlobalMyCount(res.global.myCount)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setOverviewLoading(false)
    }
  }, [])

  const loadAppMemories = useCallback(async (appId: string, page: number, keyword = appMemKeyword) => {
    setAppMemoriesLoading(true)
    try {
      const res = await getMyMemories(appId, page, PAGE_SIZE, keyword || undefined)
      setAppMemories(res.items)
      setAppTotal(res.total)
      setAppMemoryEnabled(res.memoryEnabled)
      // 删除末页最后一条后回退一页
      if (res.items.length === 0 && page > 1) {
        setAppPage(page - 1)
        await loadAppMemories(appId, page - 1, keyword)
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setAppMemoriesLoading(false)
    }
  }, [appMemKeyword])

  const loadGlobalMemories = useCallback(async (page: number, keyword = globalKeyword) => {
    setGlobalLoading(true)
    try {
      const res = await getMyGlobalMemories(page, PAGE_SIZE, keyword || undefined)
      setGlobalMemories(res.items)
      setGlobalTotal(res.total)
      if (res.items.length === 0 && page > 1) {
        setGlobalPage(page - 1)
        await loadGlobalMemories(page - 1, keyword)
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setGlobalLoading(false)
    }
  }, [globalKeyword])

  useEffect(() => {
    void loadOverview()
    void loadGlobalMemories(1)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (selectedApp?.appId) {
      void loadAppMemories(selectedApp.appId, appPage)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedApp?.appId, appPage, appMemKeyword])

  /** 全局记忆搜索：提交关键字并回到第一页重查 */
  const handleGlobalSearch = (value: string) => {
    const kw = value.trim()
    setGlobalKeywordInput(value)
    setGlobalKeyword(kw)
    setGlobalPage(1)
    void loadGlobalMemories(1, kw)
  }

  /** 应用记忆明细搜索：提交关键字并回到第一页（由 effect 重查） */
  const handleAppMemSearch = (value: string) => {
    const kw = value.trim()
    setAppMemKeywordInput(value)
    setAppMemKeyword(kw)
    setAppPage(1)
  }

  const handleDeleteAppMemory = async (item: MyMemoryItem) => {
    if (!item.appId) return
    try {
      await deleteMyMemory(item.appId, String(item.id))
      feedback.success(t('myMemory.deleteSuccess'))
      if (selectedApp?.appId) {
        void loadAppMemories(selectedApp.appId, appPage)
      }
      void loadOverview()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleClearAppMemories = async () => {
    if (!selectedApp?.appId) return
    try {
      await clearMyAppMemory(selectedApp.appId)
      feedback.success(t('myMemory.clearSuccess'))
      setAppPage(1)
      void loadAppMemories(selectedApp.appId, 1)
      void loadOverview()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDeleteGlobal = async (item: MyMemoryItem) => {
    try {
      await deleteMyGlobalMemory(String(item.id))
      feedback.success(t('memoryMgmt.deleteSuccess'))
      void loadGlobalMemories(globalPage)
      void loadOverview()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleClearGlobal = async () => {
    try {
      await clearMyGlobalMemory()
      feedback.success(t('memoryMgmt.clearSuccess'))
      setGlobalPage(1)
      void loadGlobalMemories(1)
      void loadOverview()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const typeName = (type: number | null | undefined) =>
    type === TYPE_EPISODIC
      ? t('appMemory.typeEpisodic')
      : type === TYPE_PROCEDURAL
        ? t('appMemory.typeProcedural')
        : t('appMemory.typeSemantic')

  // 应用记忆 Tab 关键字过滤（应用名/团队名，前端过滤）
  const appsKw = appsKeyword.trim().toLowerCase()
  const filteredApps = appsKw
    ? apps.filter((a) => (a.appName ?? '').toLowerCase().includes(appsKw) || (a.teamName ?? '').toLowerCase().includes(appsKw))
    : apps

  const typeColumn = {
    title: t('memoryMgmt.colType'),
    dataIndex: 'memoryType',
    width: 96,
    render: (v: number | null | undefined) => <Tag>{typeName(v)}</Tag>,
  }

  const contentColumn = {
    title: t('memoryMgmt.colContent'),
    dataIndex: 'content',
    ellipsis: true,
    render: (v: string | null | undefined) => (
      <Text style={{ whiteSpace: 'normal', wordBreak: 'break-word' }}>{v}</Text>
    ),
  }

  const updateTimeColumn = {
    title: t('memoryMgmt.colUpdateTime'),
    dataIndex: 'updateTime',
    width: 160,
    render: (v: string | null | undefined) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
  }

  const appListColumns: TableColumnsType<MyMemoryAppItem> = [
    {
      title: t('memoryMgmt.colAppName'),
      dataIndex: 'appName',
      width: 220,
      render: (v: string | null | undefined) => <Text strong>{v || '-'}</Text>,
    },
    {
      title: t('memoryMgmt.colTeam'),
      dataIndex: 'teamName',
      width: 180,
      ellipsis: true,
      render: (v: string | null | undefined) => v || '-',
    },
    {
      title: t('memoryMgmt.colCount'),
      dataIndex: 'memoryCount',
      width: 110,
      render: (v: number | null | undefined) => v ?? 0,
    },
    {
      title: t('memoryMgmt.colLastActive'),
      dataIndex: 'lastActiveTime',
      width: 160,
      render: (v: string | null | undefined) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
    },
    {
      title: t('memoryMgmt.colActions'),
      key: 'actions',
      width: 96,
      fixed: 'right',
      render: (_: unknown, record: MyMemoryAppItem) => (
        <Button type="link" size="small" onClick={() => {
          setSelectedApp(record)
          setAppPage(1)
        }}>
          {t('memoryMgmt.view')}
        </Button>
      ),
    },
  ]

  const appMemoryColumns: TableColumnsType<MyMemoryItem> = [
    typeColumn,
    contentColumn,
    updateTimeColumn,
    {
      title: t('memoryMgmt.colActions'),
      key: 'actions',
      width: 110,
      fixed: 'right',
      render: (_: unknown, record: MyMemoryItem) => (
        <Popconfirm
          title={t('myMemory.deleteConfirm')}
          okText={t('common.ok')}
          cancelText={t('common.cancel')}
          onConfirm={() => void handleDeleteAppMemory(record)}
        >
          <Button type="link" size="small" danger icon={<DeleteOutlined />}>
            {t('memoryMgmt.delete')}
          </Button>
        </Popconfirm>
      ),
    },
  ]

  const globalColumns: TableColumnsType<MyMemoryItem> = [
    typeColumn,
    contentColumn,
    updateTimeColumn,
    {
      title: t('memoryMgmt.colActions'),
      key: 'actions',
      width: 110,
      fixed: 'right',
      render: (_: unknown, record: MyMemoryItem) => (
        <Popconfirm
          title={t('memoryMgmt.deleteConfirm')}
          okText={t('common.ok')}
          cancelText={t('common.cancel')}
          onConfirm={() => void handleDeleteGlobal(record)}
        >
          <Button type="link" size="small" danger icon={<DeleteOutlined />}>
            {t('memoryMgmt.delete')}
          </Button>
        </Popconfirm>
      ),
    },
  ]

  return (
    <Page>
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: spacing.md,
        }}
      >
        {activeTab === 'apps' && selectedApp ? (
          <>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
                <Button
                  icon={<ArrowLeftOutlined />}
                  onClick={() => {
                    setSelectedApp(null)
                    void loadOverview()
                  }}
                />
                <Typography.Title level={4} style={{ margin: 0 }} ellipsis>
                  {selectedApp.appName || '-'}
                </Typography.Title>
                {selectedApp.teamName && <Tag>{selectedApp.teamName}</Tag>}
                {!appMemoryEnabled && <Tag>{t('myMemory.disabledTag')}</Tag>}
              </div>
              <Popconfirm
                title={t('myMemory.clearConfirm')}
                okText={t('common.ok')}
                cancelText={t('common.cancel')}
                onConfirm={() => void handleClearAppMemories()}
              >
                <Button danger disabled={appTotal === 0}>
                  {t('myMemory.clear')}
                </Button>
              </Popconfirm>
            </div>
            <DataTable<MyMemoryItem>
              rowKey="id"
              columns={appMemoryColumns}
              dataSource={appMemories}
              loading={appMemoriesLoading}
              sticky
              scroll={{ x: 860 }}
              pagination={{
                current: appPage,
                pageSize: PAGE_SIZE,
                total: appTotal,
                onChange: setAppPage,
              }}
              toolbar={
                <Space size={8}>
                  <Input
                    value={appMemKeywordInput}
                    onChange={(e) => {
                      setAppMemKeywordInput(e.target.value)
                      if (!e.target.value) handleAppMemSearch('')
                    }}
                    onPressEnter={() => handleAppMemSearch(appMemKeywordInput)}
                    allowClear
                    maxLength={100}
                    placeholder={t('memoryMgmt.searchAppMemoryPlaceholder')}
                    style={{ width: 260 }}
                  />
                  <Button onClick={() => handleAppMemSearch(appMemKeywordInput)}>
                    {t('memoryMgmt.search')}
                  </Button>
                </Space>
              }
            />
          </>
        ) : (
          <Tabs
            activeKey={activeTab}
            onChange={(key) => setActiveTab(key as 'apps' | 'global')}
            items={[
              {
                key: 'apps',
                label: t('memoryMgmt.tabApps'),
                children:
                  activeTab === 'apps' ? (
                    <DataTable<MyMemoryAppItem>
                      rowKey="appId"
                      columns={appListColumns}
                      dataSource={filteredApps}
                      loading={overviewLoading}
                      sticky
                      scroll={{ x: 760 }}
                      pagination={filteredApps.length > PAGE_SIZE ? { pageSize: PAGE_SIZE } : false}
                      toolbar={
                        <Input
                          value={appsKeyword}
                          onChange={(e) => setAppsKeyword(e.target.value)}
                          allowClear
                          maxLength={100}
                          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
                          placeholder={t('memoryMgmt.searchAppsPlaceholder')}
                          style={{ width: 260 }}
                        />
                      }
                      onRefresh={() => void loadOverview()}
                      refreshLoading={overviewLoading}
                    />
                  ) : null,
              },
              {
                key: 'global',
                label: t('memoryMgmt.tabGlobal', { count: globalMyCount }),
                children:
                  activeTab === 'global' ? (
                    <DataTable<MyMemoryItem>
                      rowKey="id"
                      columns={globalColumns}
                      dataSource={globalMemories}
                      loading={globalLoading}
                      sticky
                      scroll={{ x: 830 }}
                      pagination={{
                        current: globalPage,
                        pageSize: PAGE_SIZE,
                        total: globalTotal,
                        onChange: setGlobalPage,
                      }}
                      toolbar={
                        <Space size={12}>
                          <Input
                            value={globalKeywordInput}
                            onChange={(e) => {
                              setGlobalKeywordInput(e.target.value)
                              if (!e.target.value) handleGlobalSearch('')
                            }}
                            onPressEnter={() => handleGlobalSearch(globalKeywordInput)}
                            allowClear
                            maxLength={100}
                            placeholder={t('memoryMgmt.searchGlobalPlaceholder')}
                            style={{ width: 260 }}
                          />
                          <Button onClick={() => handleGlobalSearch(globalKeywordInput)}>
                            {t('memoryMgmt.search')}
                          </Button>
                          <Popconfirm
                            title={t('memoryMgmt.clearConfirm')}
                            okText={t('common.ok')}
                            cancelText={t('common.cancel')}
                            onConfirm={() => void handleClearGlobal()}
                          >
                            <Button danger disabled={globalTotal === 0}>
                              {t('memoryMgmt.clear')}
                            </Button>
                          </Popconfirm>
                        </Space>
                      }
                    />
                  ) : null,
              },
            ]}
          />
        )}
      </div>
    </Page>
  )
}
