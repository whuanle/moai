import { useCallback, useEffect, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  MoreOutlined,
  ReloadOutlined,
  SearchOutlined,
  ShareAltOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import { Button, Dropdown, Input, Popconfirm, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { type PluginClassify } from '@/api/classify'
import {
  customPluginApi,
  type CustomPlugin,
  type CustomPluginType,
} from '@/api/plugin'
import { AuditCell, DataTable, feedback, PageToolbar } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { McpPluginModal, type McpFormValues } from './components/McpPluginModal'
import { OpenApiModal, type OpenApiFormValues } from './components/OpenApiModal'
import { FunctionListModal } from './components/FunctionListModal'
import { PluginAvatar } from './components/PluginAvatarUpload'
import { PluginTeamAuthorizationDrawer } from './components/PluginTeamAuthorizationDrawer'
import {
  PluginCard,
  PluginCardEmpty,
  PluginCardGrid,
  PluginClassifyTag,
  PluginVisibilityTag,
  PluginViewToggle,
  pluginTypeColor,
  type PluginView,
} from './components/PluginCardView'

const { Text } = Typography

function typeLabel(t: (k: string) => string, type: string | null | undefined): string {
  if (type === 'mcp') return t('plugins.typeMcp')
  if (type === 'openApi') return t('plugins.typeOpenapi')
  return String(type ?? '-')
}

interface KeyValueItem {
  key: string
  value: string
}

/** 将 MCP 表单值转为后端需要的 header/query 数组（含 transportMode）. */
function toMcpKeyValue(values: McpFormValues, mode: 'header' | 'query'): KeyValueItem[] {
  const base = (values[mode] ?? []).filter((item) => item.key && item.value)
  if (mode === 'header' && values.httpTransportMode) {
    base.push({ key: '.HttpTransportMode', value: values.httpTransportMode })
  }
  return base
}

interface CustomPluginPanelProps {
  classifies: PluginClassify[]
}

export function CustomPluginPanel({ classifies }: CustomPluginPanelProps) {
  const { t } = useTranslation()

  const [items, setItems] = useState<CustomPlugin[]>([])
  const [loading, setLoading] = useState(true)
  const [searchText, setSearchText] = useState('')
  const [searchName, setSearchName] = useState<string | undefined>(undefined)
  const [filterType, setFilterType] = useState<CustomPluginType | undefined>(undefined)
  const [filterVis, setFilterVis] = useState<boolean | undefined>(undefined)
  const [classifyFilter, setClassifyFilter] = useState('all')
  const [view, setView] = useState<PluginView>('card')

  const [mcpModalOpen, setMcpModalOpen] = useState(false)
  const [openApiModalOpen, setOpenApiModalOpen] = useState(false)
  const [editing, setEditing] = useState<CustomPlugin | null>(null)
  const [editMode, setEditMode] = useState<'mcp' | 'openApi' | null>(null)
  const [functionPlugin, setFunctionPlugin] = useState<CustomPlugin | null>(null)
  const [authPlugin, setAuthPlugin] = useState<CustomPlugin | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const classifyId = classifyFilter === 'all' ? undefined : classifyFilter === 'uncategorized' ? 0 : Number(classifyFilter)
      const data = await customPluginApi.getCustomPlugins({
        name: searchName,
        type: filterType,
        classifyId,
        isPublic: filterVis,
      })
      setItems(data)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [searchName, filterType, filterVis, classifyFilter])

  useEffect(() => {
    void load()
  }, [load])

  // 搜索输入防抖后触发服务端查询
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchName(searchText.trim() || undefined)
    }, 300)
    return () => clearTimeout(timer)
  }, [searchText])

  const openImportMcp = () => {
    setEditing(null)
    setEditMode(null)
    setMcpModalOpen(true)
  }

  const openImportOpenApi = () => {
    setEditing(null)
    setEditMode(null)
    setOpenApiModalOpen(true)
  }

  const openEdit = (record: CustomPlugin) => {
    setEditing(record)
    const isMcp = record.type === 'mcp'
    setEditMode(isMcp ? 'mcp' : 'openApi')
    if (isMcp) setMcpModalOpen(true)
    else setOpenApiModalOpen(true)
  }

  const submitMcp = async (values: McpFormValues) => {
    if (editing?.pluginId) {
      await customPluginApi.updateMcp({
        pluginId: editing.pluginId,
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: toMcpKeyValue(values, 'header'),
        query: toMcpKeyValue(values, 'query'),
        isPublic: values.isPublic,
        classifyId: values.classifyId,
      })
      feedback.success(t('plugins.updatePluginSuccess'))
    } else {
      await customPluginApi.importMcp({
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: toMcpKeyValue(values, 'header'),
        query: toMcpKeyValue(values, 'query'),
        isPublic: values.isPublic,
        classifyId: values.classifyId,
      })
      feedback.success(t('plugins.importMcpSuccess'))
    }
    setMcpModalOpen(false)
    setEditing(null)
    void load()
  }

  const submitOpenApi = async (
    values: OpenApiFormValues,
    file: File | null,
    fileId: string | undefined,
  ) => {
    if (editing?.pluginId) {
      await customPluginApi.updateOpenApi({
        pluginId: editing.pluginId,
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: (values.header ?? []).map((i) => ({ key: i.key, value: i.value })),
        query: (values.query ?? []).map((i) => ({ key: i.key, value: i.value })),
        fileId,
        fileName: file?.name,
        isPublic: values.isPublic,
        classifyId: values.classifyId,
      })
      feedback.success(t('plugins.updatePluginSuccess'))
    } else {
      if (!fileId) {
        feedback.warning(t('plugins.formUploadOpenApiRequired'))
        return
      }
      await customPluginApi.importOpenApi({
        name: values.name,
        title: values.title,
        description: values.description,
        fileId,
        fileName: file?.name,
        isPublic: values.isPublic,
        classifyId: values.classifyId,
      })
      feedback.success(t('plugins.importOpenApiSuccess'))
    }
    setOpenApiModalOpen(false)
    setEditing(null)
    void load()
  }

  const handleDelete = async (record: CustomPlugin) => {
    if (!record.pluginId) return
    await customPluginApi.deleteCustomPlugin(record.pluginId)
    feedback.success(t('plugins.deletePluginSuccess'))
    if (editing?.pluginId === record.pluginId) setEditing(null)
    void load()
  }

  const handleRefreshMcp = async (record: CustomPlugin) => {
    if (!record.pluginId) return
    await customPluginApi.refreshMcp(record.pluginId)
    feedback.success(t('plugins.refreshMcpSuccess'))
    void load()
  }

  /** 行/卡片操作区：查看函数、编辑内联，刷新函数与授权团队收进更多菜单，删除走 Popconfirm；卡片底栏用图标+Tooltip 紧凑形态. */
  const renderActions = (record: CustomPlugin, iconOnly = false) => {
    const moreItems = [
      ...(record.type === 'mcp'
        ? [{ key: 'refresh', icon: <SyncOutlined />, label: t('plugins.refreshMcp') }]
        : []),
      ...(record.isPublic === false
        ? [{ key: 'auth', icon: <ShareAltOutlined />, label: t('plugins.authorization') }]
        : []),
    ]
    const btn = (key: string, icon: React.ReactNode, label: string, onClick: () => void, danger = false) =>
      iconOnly ? (
        <Tooltip key={key} title={label}>
          <Button type="text" size="small" danger={danger} icon={icon} aria-label={label} onClick={onClick} />
        </Tooltip>
      ) : (
        <Button
          key={key}
          type="link"
          size="small"
          danger={danger}
          icon={icon}
          aria-label={label}
          onClick={onClick}
        >
          {label}
        </Button>
      )
    return (
      <Space size={0} wrap={false}>
        {btn('view', <EyeOutlined />, t('plugins.viewFunctions'), () => setFunctionPlugin(record))}
        {btn('edit', <EditOutlined />, t('plugins.editPlugin'), () => openEdit(record))}
        {moreItems.length > 0 && (
          <Dropdown
            key="more"
            trigger={['click']}
            menu={{
              items: moreItems,
              onClick: ({ key }) => {
                if (key === 'refresh') void handleRefreshMcp(record)
                if (key === 'auth') setAuthPlugin(record)
              },
            }}
          >
            <Button type="text" size="small" icon={<MoreOutlined />} aria-label={t('plugins.moreActions')} />
          </Dropdown>
        )}
        <Popconfirm
          title={t('plugins.deletePlugin')}
          description={t('plugins.deletePluginConfirm')}
          okButtonProps={{ danger: true }}
          onConfirm={() => void handleDelete(record)}
        >
          {btn('del', <DeleteOutlined />, t('plugins.deletePlugin'), () => undefined, true)}
        </Popconfirm>
      </Space>
    )
  }

  const columns: TableColumnsType<CustomPlugin> = [
    {
      title: t('plugins.colPluginName'),
      dataIndex: 'pluginName',
      key: 'pluginName',
      width: 160,
      render: (_: string | null, record) => (
        <Space size={8} style={{ display: 'flex', minWidth: 0 }}>
          <PluginAvatar objectKey={record.avatarPath} title={record.title ?? record.pluginName} size={28} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <Text strong ellipsis style={{ display: 'block' }}>{record.title || record.pluginName || '-'}</Text>
            <Text type="secondary" ellipsis style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}>
              {record.pluginName}
            </Text>
          </div>
        </Space>
      ),
    },
    {
      title: t('plugins.colType'),
      dataIndex: 'type',
      key: 'type',
      width: 100,
      render: (v: string | null) => <Tag color={pluginTypeColor(v)}>{typeLabel(t, v)}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyId',
      key: 'classifyId',
      width: 110,
      render: (v: number | null | undefined) => <PluginClassifyTag classifies={classifies} classifyId={v} />,
    },
    {
      title: t('plugins.colServer'),
      dataIndex: 'server',
      key: 'server',
      width: 180,
      ellipsis: true,
      render: (v: string | null) => (
        <Tooltip title={v || undefined}>
          <Text type="secondary" style={{ fontFamily: 'monospace', fontSize: 12 }}>{v || '-'}</Text>
        </Tooltip>
      ),
    },
    {
      title: t('plugins.formDescription'),
      dataIndex: 'description',
      key: 'description',
      width: 220,
      ellipsis: true,
      render: (v: string | null) => <Text type="secondary">{v || '-'}</Text>,
    },
    {
      title: t('plugins.colIsPublic'),
      dataIndex: 'isPublic',
      key: 'isPublic',
      width: 80,
      render: (v: boolean | null) => <PluginVisibilityTag isPublic={v} />,
    },
    {
      title: t('plugins.colCounter'),
      dataIndex: 'counter',
      key: 'counter',
      width: 80,
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
      width: 350,
      fixed: 'right',
      render: (_, record) => renderActions(record),
    },
  ]

  const classifyTags = [
    { value: 'all', label: t('plugins.classifyAll') },
    { value: 'uncategorized', label: t('plugins.classifyUncategorized') },
    ...classifies.map((c) => ({ value: String(c.classifyId), label: `${c.emoji ? `${c.emoji} ` : ''}${c.name}` })),
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
              value={filterType}
              onChange={(v) => setFilterType(v ?? undefined)}
              placeholder={t('plugins.filterType')}
              allowClear
              style={{ width: 130 }}
              options={[
                { value: 'mcp', label: t('plugins.typeMcp') },
                { value: 'openApi', label: t('plugins.typeOpenapi') },
              ]}
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
            <Button onClick={openImportOpenApi}>{t('plugins.importOpenApi')}</Button>
            <Button type="primary" onClick={openImportMcp}>{t('plugins.importMcp')}</Button>
          </>
        }
      />

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: spacing.md }}>
        {classifyTags.map((item) => (
          <Tag.CheckableTag
            key={item.value}
            checked={classifyFilter === item.value}
            onChange={() => setClassifyFilter(item.value)}
          >
            {item.label}
          </Tag.CheckableTag>
        ))}
      </div>

      {view === 'card' ? (
        items.length ? (
          <PluginCardGrid>
            {items.map((record) => (
              <PluginCard
                key={record.pluginId}
                avatarPath={record.avatarPath}
                title={record.title}
                name={record.pluginName ?? '-'}
                server={record.server}
                typeLabel={typeLabel(t, record.type)}
                typeColor={pluginTypeColor(record.type)}
                description={record.description}
                tags={
                  <>
                    <PluginClassifyTag classifies={classifies} classifyId={record.classifyId} />
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
        <DataTable<CustomPlugin>
          rowKey="pluginId"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1580 }}
          pagination={false}
        />
      )}

      <McpPluginModal
        open={mcpModalOpen}
        isEdit={editMode === 'mcp'}
        editing={editing}
        classifies={classifies}
        onOk={submitMcp}
        enableAvatar
        onAvatarChanged={load}
        onCancel={() => {
          setMcpModalOpen(false)
          setEditing(null)
          setEditMode(null)
        }}
      />

      <OpenApiModal
        open={openApiModalOpen}
        isEdit={editMode === 'openApi'}
        editing={editing}
        classifies={classifies}
        onOk={submitOpenApi}
        enableAvatar
        onAvatarChanged={load}
        onCancel={() => {
          setOpenApiModalOpen(false)
          setEditing(null)
          setEditMode(null)
        }}
      />

      <FunctionListModal
        open={Boolean(functionPlugin)}
        plugin={functionPlugin}
        onCancel={() => setFunctionPlugin(null)}
      />

      <PluginTeamAuthorizationDrawer
        open={Boolean(authPlugin)}
        pluginId={authPlugin?.pluginId ?? null}
        pluginName={authPlugin?.pluginName ?? null}
        onClose={() => setAuthPlugin(null)}
      />
    </>
  )
}
