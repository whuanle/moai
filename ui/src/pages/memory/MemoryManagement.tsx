import { useCallback, useEffect, useState } from 'react'
import { ArrowLeftOutlined, DeleteOutlined, EditOutlined, GlobalOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Form, Input, Modal, Popconfirm, Select, Space, Tabs, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DataTable, Page, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { formatDateTime } from '@/utils/datetime'
import {
  clearMyAppMemory,
  clearMyGlobalMemory,
  deleteMyGlobalMemory,
  deleteMyMemory,
  getMyGlobalMemories,
  getMyMemories,
  getMyMemoryOverview,
  promoteMemoryToGlobal,
  saveMyGlobalMemory,
  updateMyGlobalMemory,
  updateMyMemory,
  type MyMemoryAppItem,
  type MyMemoryItem,
} from '@/api/memory'

const { Text } = Typography

const PAGE_SIZE = 20

/** 记忆类型常量（对齐后端 AgentMemorySettings.Type*） */
const TYPE_EPISODIC = 1
const TYPE_PROCEDURAL = 2

/** 记忆类型下拉选项（0 语义 / 1 情景 / 2 程序） */
const memoryTypeOptions = (t: (key: string) => string) => [
  { value: 0, label: t('appMemory.typeSemantic') },
  { value: TYPE_EPISODIC, label: t('appMemory.typeEpisodic') },
  { value: TYPE_PROCEDURAL, label: t('appMemory.typeProcedural') },
]

interface MemoryFormValues {
  content?: string
  memoryType?: number
}

/**
 * 记忆管理页（个人跨应用视图）：「应用记忆」按应用查看/编辑/删除本人记忆并可存为全局；
 * 「全局记忆」维护个人跨应用记忆（新建/编辑/删除/清空），平台未开启时展示引导提示.
 */
export function MemoryManagement() {
  const { t } = useTranslation()
  const isRoot = useAppStore((state) => state.userInfo?.isRoot === true)

  const [activeTab, setActiveTab] = useState<'apps' | 'global'>('apps')

  const [overviewLoading, setOverviewLoading] = useState(false)
  const [apps, setApps] = useState<MyMemoryAppItem[]>([])
  const [globalEnabled, setGlobalEnabled] = useState(false)
  const [globalMyCount, setGlobalMyCount] = useState(0)

  const [selectedApp, setSelectedApp] = useState<MyMemoryAppItem | null>(null)
  const [appMemories, setAppMemories] = useState<MyMemoryItem[]>([])
  const [appTotal, setAppTotal] = useState(0)
  const [appPage, setAppPage] = useState(1)
  const [appMemoryEnabled, setAppMemoryEnabled] = useState(true)
  const [appMemoriesLoading, setAppMemoriesLoading] = useState(false)

  const [globalMemories, setGlobalMemories] = useState<MyMemoryItem[]>([])
  const [globalTotal, setGlobalTotal] = useState(0)
  const [globalPage, setGlobalPage] = useState(1)
  const [globalLoading, setGlobalLoading] = useState(false)

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<MyMemoryItem | null>(null)
  /** 编辑目标：global=全局记忆；否则为应用记忆（应用 id 存于 editing.appId） */
  const [editingScope, setEditingScope] = useState<'app' | 'global'>('app')
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<MemoryFormValues>()

  const loadOverview = useCallback(async () => {
    setOverviewLoading(true)
    try {
      const res = await getMyMemoryOverview()
      setApps(res.apps)
      setGlobalEnabled(res.global.enabled)
      setGlobalMyCount(res.global.myCount)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setOverviewLoading(false)
    }
  }, [])

  const loadAppMemories = useCallback(async (appId: string, page: number) => {
    setAppMemoriesLoading(true)
    try {
      const res = await getMyMemories(appId, page, PAGE_SIZE)
      setAppMemories(res.items)
      setAppTotal(res.total)
      setAppMemoryEnabled(res.memoryEnabled)
      // 删除末页最后一条后回退一页
      if (res.items.length === 0 && page > 1) {
        setAppPage(page - 1)
        await loadAppMemories(appId, page - 1)
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setAppMemoriesLoading(false)
    }
  }, [])

  const loadGlobalMemories = useCallback(async (page: number) => {
    setGlobalLoading(true)
    try {
      const res = await getMyGlobalMemories(page, PAGE_SIZE)
      setGlobalMemories(res.items)
      setGlobalTotal(res.total)
      setGlobalEnabled(res.globalEnabled)
      if (res.items.length === 0 && page > 1) {
        setGlobalPage(page - 1)
        await loadGlobalMemories(page - 1)
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setGlobalLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadOverview()
    void loadGlobalMemories(1)
  }, [loadOverview, loadGlobalMemories])

  useEffect(() => {
    if (selectedApp?.appId) {
      void loadAppMemories(selectedApp.appId, appPage)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedApp?.appId, appPage])

  const openCreateGlobal = () => {
    setEditing(null)
    setEditingScope('global')
    form.resetFields()
    form.setFieldsValue({ memoryType: 0 })
    setFormOpen(true)
  }

  const openEdit = (item: MyMemoryItem, scope: 'app' | 'global') => {
    setEditing(item)
    setEditingScope(scope)
    form.setFieldsValue({ content: item.content ?? '', memoryType: item.memoryType ?? 0 })
    setFormOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing) {
        if (editingScope === 'global') {
          await updateMyGlobalMemory(String(editing.id), values.content ?? '', values.memoryType ?? 0)
        } else {
          await updateMyMemory(String(editing.appId), String(editing.id), values.content ?? '', values.memoryType ?? 0)
        }
        feedback.success(t('memoryMgmt.updateSuccess'))
      } else {
        await saveMyGlobalMemory(values.content ?? '', values.memoryType ?? 0)
        feedback.success(t('memoryMgmt.createSuccess'))
      }
      setFormOpen(false)
      if (selectedApp?.appId && editingScope === 'app') {
        void loadAppMemories(selectedApp.appId, appPage)
      }
      if (editingScope === 'global') {
        void loadGlobalMemories(globalPage)
      }
      void loadOverview()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
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

  const handlePromote = async (item: MyMemoryItem) => {
    if (!item.appId) return
    try {
      await promoteMemoryToGlobal(item.appId, String(item.id))
      feedback.success(t('memoryMgmt.promoteSuccess'))
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
      width: 140,
      fixed: 'right',
      render: (_: unknown, record: MyMemoryItem) => (
        <Space size={0}>
          <Tooltip title={appMemoryEnabled ? t('memoryMgmt.edit') : t('memoryMgmt.editDisabledAppMemory')}>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('memoryMgmt.edit')}
              disabled={!appMemoryEnabled}
              onClick={() => openEdit(record, 'app')}
            />
          </Tooltip>
          <Tooltip title={globalEnabled ? t('memoryMgmt.promote') : t('memoryMgmt.promoteDisabled')}>
            <Button
              type="text"
              size="small"
              icon={<GlobalOutlined />}
              aria-label={t('memoryMgmt.promote')}
              disabled={!globalEnabled}
              onClick={() => void handlePromote(record)}
            />
          </Tooltip>
          <Popconfirm
            title={t('myMemory.deleteConfirm')}
            okText={t('common.ok')}
            cancelText={t('common.cancel')}
            onConfirm={() => void handleDeleteAppMemory(record)}
          >
            <Tooltip title={t('memoryMgmt.delete')}>
              <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('memoryMgmt.delete')} />
            </Tooltip>
          </Popconfirm>
        </Space>
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
      width: 96,
      fixed: 'right',
      render: (_: unknown, record: MyMemoryItem) => (
        <Space size={0}>
          <Tooltip title={t('memoryMgmt.edit')}>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('memoryMgmt.edit')}
              disabled={!globalEnabled}
              onClick={() => openEdit(record, 'global')}
            />
          </Tooltip>
          <Popconfirm
            title={t('memoryMgmt.deleteConfirm')}
            okText={t('common.ok')}
            cancelText={t('common.cancel')}
            onConfirm={() => void handleDeleteGlobal(record)}
          >
            <Tooltip title={t('memoryMgmt.delete')}>
              <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('memoryMgmt.delete')} />
            </Tooltip>
          </Popconfirm>
        </Space>
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
            <Text type="secondary">{t('memoryMgmt.appMemoryHint')}</Text>
            <DataTable<MyMemoryItem>
              rowKey="id"
              columns={appMemoryColumns}
              dataSource={appMemories}
              loading={appMemoriesLoading}
              sticky
              scroll={{ x: 760 }}
              pagination={{
                current: appPage,
                pageSize: PAGE_SIZE,
                total: appTotal,
                onChange: setAppPage,
              }}
              onRefresh={() => selectedApp.appId && void loadAppMemories(selectedApp.appId, appPage)}
              refreshLoading={appMemoriesLoading}
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
                      dataSource={apps}
                      loading={overviewLoading}
                      sticky
                      scroll={{ x: 760 }}
                      pagination={apps.length > PAGE_SIZE ? { pageSize: PAGE_SIZE } : false}
                      toolbar={
                        <Text type="secondary">{t('memoryMgmt.appsHint')}</Text>
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
                    <>
                      <div style={{ marginBottom: spacing.md }}>
                        {globalEnabled ? (
                          <Text type="secondary">{t('memoryMgmt.globalHint')}</Text>
                        ) : (
                          <Text type="secondary">
                            {isRoot
                              ? t('memoryMgmt.globalDisabledRoot')
                              : t('memoryMgmt.globalDisabled')}
                          </Text>
                        )}
                      </div>
                      <DataTable<MyMemoryItem>
                        rowKey="id"
                        columns={globalColumns}
                        dataSource={globalMemories}
                        loading={globalLoading}
                        sticky
                        scroll={{ x: 760 }}
                        pagination={{
                          current: globalPage,
                          pageSize: PAGE_SIZE,
                          total: globalTotal,
                          onChange: setGlobalPage,
                        }}
                        toolbar={
                          <Space size={12}>
                            <Button type="primary" icon={<PlusOutlined />} disabled={!globalEnabled} onClick={openCreateGlobal}>
                              {t('memoryMgmt.create')}
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
                        onRefresh={() => void loadGlobalMemories(globalPage)}
                        refreshLoading={globalLoading}
                      />
                    </>
                  ) : null,
              },
            ]}
          />
        )}
      </div>

      <Modal
        open={formOpen}
        title={
          editing
            ? editingScope === 'global'
              ? t('memoryMgmt.editGlobalTitle')
              : t('memoryMgmt.editAppTitle')
            : t('memoryMgmt.createTitle')
        }
        onOk={() => void handleSubmit()}
        onCancel={() => setFormOpen(false)}
        okText={t('memoryMgmt.save')}
        cancelText={t('common.cancel')}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="content"
            label={t('memoryMgmt.colContent')}
            rules={[
              { required: true, message: t('memoryMgmt.contentRequired') },
              { max: 500, message: t('memoryMgmt.contentMax') },
            ]}
          >
            <Input.TextArea placeholder={t('memoryMgmt.contentPlaceholder')} rows={4} maxLength={500} showCount />
          </Form.Item>
          <Form.Item name="memoryType" label={t('memoryMgmt.colType')} initialValue={0}>
            <Select options={memoryTypeOptions(t)} style={{ width: 200 }} />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
