import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  AppstoreOutlined,
  DeleteOutlined,
  EditOutlined,
  MoreOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  ShareAltOutlined,
} from '@ant-design/icons'
import { Button, Dropdown, Input, Popconfirm, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import type { PluginClassify } from '@/api/classify'
import {
  pluginApi,
  type ClassifyFilter,
  type DynamicPluginManageItem,
  type DynamicPluginTemplate,
} from '@/api/plugin'
import { AuditCell, DataTable, feedback, PageToolbar } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { PluginAvatar } from './components/PluginAvatarUpload'
import { DynamicPluginInstanceModal } from './components/DynamicPluginInstanceModal'
import { PluginRunDrawer } from './components/PluginRunDrawer'
import { PluginTeamAuthorizationDrawer } from './components/PluginTeamAuthorizationDrawer'
import {
  PluginCard,
  PluginCardEmpty,
  PluginCardGrid,
  PluginClassifyTag,
  PluginViewToggle,
  PluginVisibilityTag,
  type PluginView,
} from './components/PluginCardView'

const { Text } = Typography

interface DynamicPluginPanelProps {
  classifies: PluginClassify[]
}

export function DynamicPluginPanel({ classifies }: DynamicPluginPanelProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [items, setItems] = useState<DynamicPluginManageItem[]>([])
  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<DynamicPluginManageItem | null>(null)
  const [drawerTarget, setDrawerTarget] = useState<DynamicPluginManageItem | null>(null)
  const [authPlugin, setAuthPlugin] = useState<DynamicPluginManageItem | null>(null)
  const [filter, setFilter] = useState<ClassifyFilter>('all')
  const [searchText, setSearchText] = useState('')
  const [filterVis, setFilterVis] = useState<boolean | undefined>(undefined)
  const [view, setView] = useState<PluginView>('card')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [instances, tmpl] = await Promise.all([
        pluginApi.getManagePlugins('dynamic'),
        pluginApi.getDynamicTemplates(),
      ])
      setItems(instances as DynamicPluginManageItem[])
      setTemplates(tmpl)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const openCreate = () => {
    setEditing(null)
    setModalOpen(true)
  }

  const openEdit = (record: DynamicPluginManageItem) => {
    setEditing(record)
    setModalOpen(true)
  }

  const closeModal = () => {
    setEditing(null)
    setModalOpen(false)
  }

  const handleDelete = async (record: DynamicPluginManageItem) => {
    const pluginKey = record.pluginName ?? record.pluginKey
    if (!pluginKey) return
    try {
      await pluginApi.deleteDynamicPlugin(pluginKey)
      feedback.success(t('plugins.deletePluginSuccess'))
      if (editing?.pluginName === pluginKey) setEditing(null)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
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

  /** 实例引用的模板已不在注册表（模板被下线）：运行与编辑都不可用，仅保留删除. */
  const isTemplateMissing = useCallback(
    (record: DynamicPluginManageItem) =>
      Boolean(record.templeteKey) && !templates.some((tpl) => tpl.key === record.templeteKey),
    [templates],
  )

  /** 行/卡片操作区：运行、编辑内联（模板下线时禁用），授权团队收进更多菜单，删除走 Popconfirm；卡片底栏用图标+Tooltip 紧凑形态. */
  const renderActions = (record: DynamicPluginManageItem, iconOnly = false) => {
    const missing = isTemplateMissing(record)
    const moreItems = [
      ...(record.isPublic === false
        ? [{ key: 'auth', icon: <ShareAltOutlined />, label: t('plugins.authorization') }]
        : []),
    ]
    const btn = (key: string, icon: React.ReactNode, label: string, onClick: () => void, disabled = false, danger = false) =>
      iconOnly ? (
        <Tooltip key={key} title={disabled ? `${label}（${t('plugins.templateMissing')}）` : label}>
          <Button type="text" size="small" danger={danger} icon={icon} aria-label={label} disabled={disabled} onClick={onClick} />
        </Tooltip>
      ) : (
        <Button
          key={key}
          type="link"
          size="small"
          danger={danger}
          icon={icon}
          aria-label={label}
          disabled={disabled}
          onClick={onClick}
        >
          {label}
        </Button>
      )
    return (
      <Space size={0} wrap={false}>
        {btn('run', <PlayCircleOutlined />, t('plugins.run'), () => setDrawerTarget(record), missing)}
        {btn('edit', <EditOutlined />, t('plugins.editPlugin'), () => openEdit(record), missing)}
        {moreItems.length > 0 && (
          <Dropdown
            key="more"
            trigger={['click']}
            menu={{
              items: moreItems,
              onClick: ({ key }) => {
                if (key === 'auth') setAuthPlugin(record)
              },
            }}
          >
            <Button type="text" size="small" icon={<MoreOutlined />} aria-label={t('plugins.moreActions')} />
          </Dropdown>
        )}
        <Popconfirm
          title={t('plugins.deletePluginConfirm')}
          okButtonProps={{ danger: true }}
          onConfirm={() => void handleDelete(record)}
        >
          {btn('del', <DeleteOutlined />, t('plugins.deletePlugin'), () => undefined, false, true)}
        </Popconfirm>
      </Space>
    )
  }

  const renderTemplateTag = (record: DynamicPluginManageItem) => {
    if (!record.templeteKey) return '-'
    return isTemplateMissing(record) ? (
      <Space size={4} wrap>
        <Tag>{record.templeteKey}</Tag>
        <Tag color="red">{t('plugins.templateMissing')}</Tag>
      </Space>
    ) : (
      <Tag color="blue">{record.templeteKey}</Tag>
    )
  }

  const columns: TableColumnsType<DynamicPluginManageItem> = [
    {
      title: t('plugins.colPluginName'),
      dataIndex: 'pluginName',
      key: 'pluginName',
      width: 220,
      render: (_: string | null, record) => (
        <Space size={8}>
          <PluginAvatar objectKey={record.avatarPath} title={record.title ?? record.pluginName} size={28} />
          <div style={{ minWidth: 0 }}>
            <Text strong style={{ display: 'block' }}>{record.title || record.pluginName || '-'}</Text>
            <Text type="secondary" style={{ fontSize: 12, fontFamily: 'monospace' }}>{record.pluginName || '-'}</Text>
          </div>
        </Space>
      ),
    },
    {
      title: t('plugins.dynamicTemplate'),
      dataIndex: 'templeteKey',
      key: 'templeteKey',
      width: 200,
      render: (_: string | null, record) => renderTemplateTag(record),
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      key: 'classifyName',
      width: 120,
      render: (_: string | null, record: DynamicPluginManageItem) => (
        <PluginClassifyTag classifies={classifies} classifyId={record.classifyId} classifyName={record.classifyName} />
      ),
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
      width: 280,
      fixed: 'right',
      render: (_: unknown, record: DynamicPluginManageItem) => renderActions(record),
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
            <Button icon={<AppstoreOutlined />} onClick={() => navigate('/plugin/templates')}>
              {t('plugins.templateList')}
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('plugins.createDynamicInstance')}
            </Button>
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
                key={record.id ?? record.pluginName}
                avatarPath={record.avatarPath}
                title={record.title}
                name={record.pluginName ?? '-'}
                typeLabel={t('plugins.typeNative')}
                description={record.description}
                tags={
                  <>
                    {record.templeteKey && (
                      <Tag color={isTemplateMissing(record) ? 'red' : 'blue'}>{record.templeteKey}</Tag>
                    )}
                    {isTemplateMissing(record) && <Tag color="red">{t('plugins.templateMissing')}</Tag>}
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
        <DataTable<DynamicPluginManageItem>
          sticky
          scroll={{ x: 'max-content' }}
          rowKey="id"
          columns={columns}
          dataSource={filtered}
          loading={loading}
          pagination={false}
        />
      )}

      {drawerTarget && (
        <PluginRunDrawer
          open={Boolean(drawerTarget)}
          onClose={() => setDrawerTarget(null)}
          pluginKey={drawerTarget.pluginName ?? drawerTarget.pluginKey}
          paramsExample={drawerTarget.paramsExample}
        />
      )}

      <PluginTeamAuthorizationDrawer
        open={Boolean(authPlugin)}
        pluginId={authPlugin?.id ?? null}
        pluginName={authPlugin?.pluginName ?? null}
        onClose={() => setAuthPlugin(null)}
      />

      <DynamicPluginInstanceModal
        open={modalOpen}
        scope="system"
        templates={templates}
        classifies={classifies}
        existingKeys={items.map((i) => i.pluginName ?? '').filter(Boolean)}
        editing={editing}
        onSaved={load}
        onClose={closeModal}
      />
    </>
  )
}
