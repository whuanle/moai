import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AppstoreOutlined,
  EditOutlined,
  PlayCircleOutlined,
  ReloadOutlined,
  SearchOutlined,
  TagsOutlined,
} from '@ant-design/icons'
import { Button, Form, Input, Modal, Select, Space, Tabs, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate, useSearchParams } from 'react-router'
import { classifyApi, type PluginClassify } from '@/api/classify'
import {
  pluginApi,
  type ClassifyFilter,
  type PluginKind,
  type PluginManageItem,
  type StaticPluginManageItem,
} from '@/api/plugin'
import { AuditCell, DataTable, feedback, Page, PageToolbar } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { CustomPluginPanel } from './CustomPluginPanel'
import { DynamicPluginPanel } from './DynamicPluginPanel'
import { PluginStatsPanel } from './PluginStatsPanel'
import { PluginAvatar, PluginAvatarUpload } from './components/PluginAvatarUpload'
import { PluginRunDrawer } from './components/PluginRunDrawer'
import { PluginSemanticModal } from './components/PluginSemanticModal'
import {
  PluginCard,
  PluginCardEmpty,
  PluginCardGrid,
  PluginClassifyTag,
  PluginVisibilityTag,
  PluginViewToggle,
  manageTypeLabel,
  pluginTypeColor,
  type PluginView,
} from './components/PluginCardView'

const TAB_KEYS = ['custom', 'dynamic', 'static', 'stats'] as const

const { Text } = Typography

function PluginPanel({ kind, classifies }: { kind: PluginKind; classifies: PluginClassify[] }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [items, setItems] = useState<StaticPluginManageItem[]>([])
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState<ClassifyFilter>('all')
  const [searchText, setSearchText] = useState('')
  const [filterVis, setFilterVis] = useState<boolean | undefined>(undefined)
  const [view, setView] = useState<PluginView>('card')
  const [drawerTarget, setDrawerTarget] = useState<StaticPluginManageItem | null>(null)
  const [semanticTarget, setSemanticTarget] = useState<StaticPluginManageItem | null>(null)
  const [editTarget, setEditTarget] = useState<PluginManageItem | null>(null)
  const [editOpen, setEditOpen] = useState(false)
  const [editSubmitting, setEditSubmitting] = useState(false)
  const [form] = Form.useForm<{ title: string; description?: string; classifyId?: number }>()
  const isStatic = kind === 'static'

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await pluginApi.getManagePlugins(kind))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [kind])

  useEffect(() => {
    void load()
  }, [load])

  const openEdit = (record: PluginManageItem) => {
    setEditTarget(record)
    form.setFieldsValue({
      title: record.title ?? '',
      description: record.description ?? '',
      classifyId: record.classifyId || undefined,
    })
    setEditOpen(true)
  }

  const closeEdit = () => {
    setEditOpen(false)
    setEditTarget(null)
    form.resetFields()
  }

  const handleEditSave = async () => {
    const values = await form.validateFields()
    const pluginKey = editTarget?.pluginKey ?? editTarget?.pluginName ?? ''
    if (!pluginKey) return
    setEditSubmitting(true)
    try {
      await pluginApi.saveStaticPlugin({
        pluginKey,
        title: values.title,
        description: values.description ?? '',
        classifyId: values.classifyId ?? 0,
      })
      feedback.success(t('plugins.updateSuccess'))
      closeEdit()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setEditSubmitting(false)
    }
  }

  const filtered = useMemo(() => {
    const kw = searchText.trim().toLowerCase()
    return items.filter((i) => {
      if (filterVis !== undefined && Boolean(i.isPublic) !== filterVis) return false
      if (filter === 'uncategorized') {
        if (!(i.classifyId === 0 || !i.classifyId)) return false
      } else if (filter !== 'all' && i.classifyId !== Number(filter)) {
        return false
      }
      if (kw && !`${i.title ?? ''}${i.pluginName ?? ''}`.toLowerCase().includes(kw)) return false
      return true
    })
  }, [items, filter, searchText, filterVis])

  const filterTags = [
    { value: 'all' as const, label: t('plugins.classifyAll') },
    { value: 'uncategorized' as const, label: t('plugins.classifyUncategorized') },
    ...classifies.map((c) => ({ value: String(c.classifyId), label: `${c.emoji ? `${c.emoji} ` : ''}${c.name}` })),
  ]

  /** 行/卡片操作区：运行、编辑内联；卡片底栏用图标+Tooltip 紧凑形态. */
  const renderActions = (record: StaticPluginManageItem, iconOnly = false) => {
    const btn = (key: string, icon: React.ReactNode, label: string, onClick: () => void) =>
      iconOnly ? (
        <Tooltip key={key} title={label}>
          <Button type="text" size="small" icon={icon} aria-label={label} onClick={onClick} />
        </Tooltip>
      ) : (
        <Button key={key} type="link" size="small" icon={icon} aria-label={label} onClick={onClick}>
          {label}
        </Button>
      )
    return (
      <Space size={0} wrap={false}>
        {btn('run', <PlayCircleOutlined />, t('plugins.run'), () => setDrawerTarget(record))}
        {btn('edit', <EditOutlined />, t('plugins.editPlugin'), () => openEdit(record))}
        {btn('semantic', <TagsOutlined />, t('plugins.semanticConfig'), () => setSemanticTarget(record))}
      </Space>
    )
  }

  const columns: TableColumnsType<StaticPluginManageItem> = [
    {
      title: t('plugins.colPluginName'),
      dataIndex: 'pluginName',
      key: 'pluginName',
      width: 220,
      render: (v: string | null, record) => (
        <Space size={8}>
          <PluginAvatar objectKey={record.avatarPath} title={record.title ?? v} size={28} />
          <div style={{ minWidth: 0 }}>
            <Text strong style={{ display: 'block' }}>{record.title || v || '-'}</Text>
            <Text type="secondary" style={{ fontSize: 12, fontFamily: 'monospace' }}>{v || '-'}</Text>
          </div>
        </Space>
      ),
    },
    {
      title: t('plugins.colType'),
      dataIndex: 'type',
      key: 'type',
      width: 100,
      render: (v: number) => <Tag color={pluginTypeColor(v)}>{manageTypeLabel(t, v)}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      key: 'classifyName',
      width: 120,
      render: (v: string | null, record: StaticPluginManageItem) => (
        <PluginClassifyTag classifies={classifies} classifyId={record.classifyId} classifyName={v} />
      ),
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isSystem',
      key: 'isSystem',
      width: 90,
      render: (v: boolean) => (v ? <Tag color="geekblue">{t('plugins.isSystem')}</Tag> : <Tag>{t('plugins.notSystem')}</Tag>),
    },
    {
      title: t('plugins.colIsPublic'),
      dataIndex: 'isPublic',
      key: 'isPublic',
      width: 90,
      render: (v: boolean) => <PluginVisibilityTag isPublic={v} />,
    },
    {
      title: t('plugins.colCounter'),
      dataIndex: 'counter',
      key: 'counter',
      width: 90,
      render: (v: string | number | null) => Number(v ?? 0),
    },
    {
      title: t('plugins.colCreate'),
      key: 'create',
      width: 150,
      render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
    },
    {
      title: t('plugins.colUpdate'),
      key: 'update',
      width: 150,
      render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} />,
    },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 200,
      fixed: 'right',
      render: (_: unknown, record: StaticPluginManageItem) => renderActions(record),
    },
  ]

  return (
    <>
      <PageToolbar
        filters={
          <>
            <Input
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={t('plugins.searchPlaceholder')}
              prefix={<SearchOutlined style={{ color: 'inherit' }} />}
              allowClear
              maxLength={50}
              style={{ width: 240 }}
            />
            <Select
              value={filterVis}
              onChange={(v) => setFilterVis(v ?? undefined)}
              placeholder={t('plugins.visibilityAll')}
              allowClear
              style={{ width: 120 }}
              options={[
                { value: true, label: t('plugins.isPublic') },
                { value: false, label: t('plugins.notPublic') },
              ]}
            />
          </>
        }
        queries={
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('plugins.refresh')}
          </Button>
        }
        actions={
          <>
            <PluginViewToggle value={view} onChange={setView} />
            {isStatic && (
              <Button icon={<AppstoreOutlined />} onClick={() => navigate('/plugin/templates')}>
                {t('plugins.templateList')}
              </Button>
            )}
          </>
        }
      />

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: spacing.md }}>
        {filterTags.map((item) => (
          <Tag.CheckableTag
            key={item.value}
            checked={filter === item.value}
            onChange={() => setFilter(item.value)}
          >
            {item.label}
          </Tag.CheckableTag>
        ))}
      </div>

      {view === 'card' ? (
        filtered.length ? (
          <PluginCardGrid>
            {filtered.map((record) => (
              <PluginCard
                key={record.pluginKey || record.pluginName || record.id || ''}
                avatarPath={record.avatarPath}
                title={record.title}
                name={record.pluginName ?? '-'}
                typeLabel={manageTypeLabel(t, record.type)}
                typeColor={pluginTypeColor(record.type)}
                description={record.description}
                tags={
                  <>
                    {isStatic && record.isSystem && <Tag color="geekblue">{t('plugins.isSystem')}</Tag>}
                    <PluginClassifyTag
                      classifies={classifies}
                      classifyId={record.classifyId}
                      classifyName={record.classifyName}
                    />
                    <PluginVisibilityTag isPublic={record.isPublic} />
                  </>
                }
                counter={record.counter}
                updatedTime={record.updateTime}
                actions={renderActions(record, true)}
              />
            ))}
          </PluginCardGrid>
        ) : (
          <PluginCardEmpty />
        )
      ) : (
        <DataTable<StaticPluginManageItem>
          sticky
          scroll={{ x: 'max-content' }}
          rowKey={(r) => r.pluginKey || r.pluginName || r.id || ''}
          columns={columns}
          dataSource={filtered}
          loading={loading}
          pagination={false}
        />
      )}

      {isStatic && (
        <PluginRunDrawer
          open={Boolean(drawerTarget)}
          onClose={() => setDrawerTarget(null)}
          pluginKey={drawerTarget?.pluginKey ?? drawerTarget?.pluginName}
          paramsExample={drawerTarget?.paramsExample}
        />
      )}
      <PluginSemanticModal
        open={Boolean(semanticTarget)}
        pluginId={semanticTarget?.id ?? null}
        pluginTitle={semanticTarget?.title ?? null}
        onClose={() => setSemanticTarget(null)}
        onSaved={() => undefined}
      />
      {isStatic && (
        <Modal
          open={editOpen}
          title={t('plugins.editPlugin')}
          onCancel={closeEdit}
          onOk={handleEditSave}
          okText={t('plugins.save')}
          confirmLoading={editSubmitting}
          maskClosable={false}
          destroyOnClose
        >
          <Form form={form} layout="vertical">
            {editTarget && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16 }}>
                <PluginAvatarUpload
                  pluginId={editTarget.id ?? ''}
                  objectKey={editTarget.avatarPath}
                  title={editTarget.title}
                  onChanged={load}
                />
                <Typography.Text type="secondary">{t('plugins.avatarTip')}</Typography.Text>
              </div>
            )}
            <Form.Item name="title" label={t('plugins.formPluginTitle')} rules={[{ required: true, message: t('plugins.pluginTitleRequired') }]}>
              <Input maxLength={50} />
            </Form.Item>
            <Form.Item name="description" label={t('plugins.formDescription')}>
              <Input.TextArea maxLength={255} />
            </Form.Item>
            <Form.Item name="classifyId" label={t('plugins.formClassify')}>
              <Select
                allowClear
                placeholder={t('plugins.formClassifyPlaceholder')}
                options={classifies.map((c) => ({ value: c.classifyId, label: `${c.emoji ? `${c.emoji} ` : ''}${c.name}` }))}
              />
            </Form.Item>
          </Form>
        </Modal>
      )}
    </>
  )
}

export function Plugins() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const [searchParams, setSearchParams] = useSearchParams()

  const [classifies, setClassifies] = useState<PluginClassify[]>([])

  const loadClassifies = useCallback(async () => {
    try {
      setClassifies(await classifyApi.getPluginClassifies())
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [])

  useEffect(() => {
    if (!isAdmin) return
    void loadClassifies()
  }, [isAdmin, loadClassifies])

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  const tabParam = searchParams.get('tab')
  const activeKey = TAB_KEYS.includes(tabParam as (typeof TAB_KEYS)[number])
    ? (tabParam as (typeof TAB_KEYS)[number])
    : 'custom'

  const handleTabChange = (key: string) => {
    setSearchParams({ tab: key })
  }

  const items = [
    {
      key: 'custom',
      label: t('plugins.tabCustom'),
      children: <CustomPluginPanel classifies={classifies} />,
    },
    { key: 'dynamic', label: t('plugins.tabDynamic'), children: <DynamicPluginPanel classifies={classifies} /> },
    { key: 'static', label: t('plugins.tabStatic'), children: <PluginPanel kind="static" classifies={classifies} /> },
    { key: 'stats', label: t('plugins.tabStats'), children: <PluginStatsPanel /> },
  ]

  return (
    <Page>
      <Tabs activeKey={activeKey} onChange={handleTabChange} items={items} />
    </Page>
  )
}
