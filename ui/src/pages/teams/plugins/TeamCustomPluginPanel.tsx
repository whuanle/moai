import { useMemo, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  MoreOutlined,
  ReloadOutlined,
  SearchOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import { Button, Dropdown, Input, Popconfirm, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { classifyLabel, type PluginClassify } from '@/api/classify'
import type { CustomPlugin, CustomPluginDetail, CustomPluginFunction, CustomPluginType } from '@/api/plugin'
import {
  deleteTeamPlugin,
  getTeamPluginDetail,
  getTeamPluginFunctions,
  preUploadTeamOpenApiFile,
  refreshTeamMcp,
  saveTeamMcpPlugin,
  saveTeamOpenApiPlugin,
  type TeamCustomPluginItem,
} from '@/api/team-plugin'
import { AuditCell, DataTable, feedback, PageToolbar } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { uploadOpenApiFileWith } from '@/utils/pluginFile'
import { McpPluginModal, type McpFormValues } from '@/pages/plugins/components/McpPluginModal'
import { OpenApiModal, type OpenApiFormValues } from '@/pages/plugins/components/OpenApiModal'
import { FunctionListModal } from '@/pages/plugins/components/FunctionListModal'
import { PluginAvatar } from '@/pages/plugins/components/PluginAvatarUpload'
import {
  PluginCard,
  PluginCardEmpty,
  PluginCardGrid,
  PluginClassifyTag,
  PluginViewToggle,
  pluginTypeColor,
  type PluginView,
} from '@/pages/plugins/components/PluginCardView'

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

/** 将 OpenAPI 表单 header/query 转为后端键值对数组. */
function toOpenApiKeyValue(items: OpenApiFormValues['header']): KeyValueItem[] {
  return (items ?? []).filter((item) => item.key && item.value)
}

/** 团队插件项映射为管理员自定义插件类型，供复用弹窗使用. */
function toCustomPlugin(item: TeamCustomPluginItem): CustomPlugin {
  return {
    pluginId: item.pluginId ?? null,
    pluginName: item.pluginName ?? '',
    title: item.title ?? '',
    description: item.description ?? '',
    server: item.server ?? '',
    type: item.type ?? undefined,
    openapiFileId: Number(item.openapiFileId ?? 0),
    openapiFileName: item.openapiFileName ?? '',
    classifyId: item.classifyId ?? 0,
    isPublic: item.isPublic ?? true,
    counter: Number(item.counter ?? 0),
    createTime: item.createTime ?? undefined,
    createUserId: Number(item.createUserId ?? 0),
    createUserName: item.createUserName ?? '',
    updateTime: item.updateTime ?? undefined,
    updateUserId: Number(item.updateUserId ?? 0),
    updateUserName: item.updateUserName ?? '',
  } as unknown as CustomPlugin
}

/** 归属标签：团队自有 / 系统（团队页以此替代系统页的可见性标签）. */
function TeamOwnedTag({ isTeamOwned, t }: { isTeamOwned: boolean | null | undefined; t: (k: string) => string }) {
  return isTeamOwned ? (
    <Tag color="green">{t('plugins.teamOwned')}</Tag>
  ) : (
    <Tag color="geekblue">{t('plugins.colIsSystem')}</Tag>
  )
}

interface TeamCustomPluginPanelProps {
  teamId: number
  items: TeamCustomPluginItem[]
  loading: boolean
  canManage: boolean
  classifies: PluginClassify[]
  reload: () => void
}

export function TeamCustomPluginPanel({
  teamId,
  items,
  loading,
  canManage,
  classifies,
  reload,
}: TeamCustomPluginPanelProps) {
  const { t } = useTranslation()

  const [searchText, setSearchText] = useState('')
  const [filterType, setFilterType] = useState<CustomPluginType | undefined>(undefined)
  const [classifyFilter, setClassifyFilter] = useState('all')
  const [view, setView] = useState<PluginView>('card')

  const [mcpModalOpen, setMcpModalOpen] = useState(false)
  const [openApiModalOpen, setOpenApiModalOpen] = useState(false)
  const [editing, setEditing] = useState<TeamCustomPluginItem | null>(null)
  const [editMode, setEditMode] = useState<'mcp' | 'openApi' | null>(null)
  const [functionPlugin, setFunctionPlugin] = useState<TeamCustomPluginItem | null>(null)

  const loadDetail = (pluginId: string): Promise<CustomPluginDetail | null> => getTeamPluginDetail(teamId, pluginId)
  const loadFunctions = (pluginId: string): Promise<CustomPluginFunction[]> => getTeamPluginFunctions(teamId, pluginId)
  const uploadFile = (file: File, pluginName: string) =>
    uploadOpenApiFileWith(file, pluginName, (payload) =>
      preUploadTeamOpenApiFile({
        teamId,
        pluginName: payload.pluginName,
        fileName: payload.fileName,
        contentType: payload.contentType,
        fileSize: payload.fileSize,
        sha256: payload.shA256,
      }),
    )

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

  const openEdit = (record: TeamCustomPluginItem) => {
    setEditing(record)
    const isMcp = record.type === 'mcp'
    setEditMode(isMcp ? 'mcp' : 'openApi')
    if (isMcp) setMcpModalOpen(true)
    else setOpenApiModalOpen(true)
  }

  const submitMcp = async (values: McpFormValues) => {
    const pluginId = editing?.pluginId ?? null
    await saveTeamMcpPlugin({
      teamId,
      pluginId,
      name: values.name,
      title: values.title,
      description: values.description ?? '',
      serverUrl: values.serverUrl,
      header: toMcpKeyValue(values, 'header'),
      query: toMcpKeyValue(values, 'query'),
      classifyId: values.classifyId,
    })
    feedback.success(pluginId ? t('plugins.updatePluginSuccess') : t('plugins.importMcpSuccess'))
    setMcpModalOpen(false)
    setEditing(null)
    reload()
  }

  const submitOpenApi = async (
    values: OpenApiFormValues,
    file: File | null,
    fileId?: string,
  ) => {
    const pluginId = editing?.pluginId ?? null
    if (!pluginId && !fileId) {
      feedback.warning(t('plugins.formUploadOpenApiRequired'))
      return
    }
    await saveTeamOpenApiPlugin({
      teamId,
      pluginId,
      fileId: Number(fileId ?? 0),
      fileName: file?.name ?? '',
      name: values.name,
      title: values.title,
      description: values.description ?? '',
      header: toOpenApiKeyValue(values.header),
      query: toOpenApiKeyValue(values.query),
      classifyId: values.classifyId,
    })
    feedback.success(pluginId ? t('plugins.updatePluginSuccess') : t('plugins.importOpenApiSuccess'))
    setOpenApiModalOpen(false)
    setEditing(null)
    reload()
  }

  const handleDelete = async (record: TeamCustomPluginItem) => {
    if (!record.pluginId) return
    await deleteTeamPlugin(teamId, String(record.pluginId))
    feedback.success(t('plugins.deletePluginSuccess'))
    if (editing?.pluginId === record.pluginId) setEditing(null)
    reload()
  }

  const handleRefreshMcp = async (record: TeamCustomPluginItem) => {
    if (!record.pluginId) return
    await refreshTeamMcp(teamId, String(record.pluginId))
    feedback.success(t('plugins.refreshMcpSuccess'))
    reload()
  }

  /** 编辑/删除/刷新仅限团队自有插件且具备管理权限，系统授权插件只读. */
  const canOperate = (record: TeamCustomPluginItem) => canManage && Boolean(record.isTeamOwned)

  const filtered = useMemo(() => {
    const keyword = searchText.trim().toLowerCase()
    return items.filter((item) => {
      if (keyword && !`${item.title ?? ''}${item.pluginName ?? ''}`.toLowerCase().includes(keyword)) return false
      if (filterType && item.type !== filterType) return false
      if (classifyFilter === 'uncategorized' && (item.classifyId ?? 0) !== 0) return false
      if (classifyFilter !== 'all' && classifyFilter !== 'uncategorized' && item.classifyId !== Number(classifyFilter)) {
        return false
      }
      return true
    })
  }, [items, searchText, filterType, classifyFilter])

  const classifyTags = useMemo(
    () => [
      { value: 'all', label: t('plugins.classifyAll') },
      { value: 'uncategorized', label: t('plugins.classifyUncategorized') },
      ...classifies.map((c) => ({ value: String(c.classifyId), label: classifyLabel(c) })),
    ],
    [classifies, t],
  )

  /** 行/卡片操作区：查看函数、编辑内联，刷新 MCP 收进更多菜单（仅团队自有可操作），删除走 Popconfirm；卡片底栏用图标+Tooltip 紧凑形态. */
  const renderActions = (record: TeamCustomPluginItem, iconOnly = false) => {
    const manageable = canOperate(record)
    const moreItems = [
      ...(manageable && record.type === 'mcp'
        ? [{ key: 'refresh', icon: <SyncOutlined />, label: t('plugins.refreshMcp') }]
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
        {manageable && btn('edit', <EditOutlined />, t('plugins.editPlugin'), () => openEdit(record))}
        {moreItems.length > 0 && (
          <Dropdown
            key="more"
            trigger={['click']}
            menu={{
              items: moreItems,
              onClick: ({ key }) => {
                if (key === 'refresh') void handleRefreshMcp(record)
              },
            }}
          >
            <Button type="text" size="small" icon={<MoreOutlined />} aria-label={t('plugins.moreActions')} />
          </Dropdown>
        )}
        {manageable && (
          <Popconfirm
            title={t('plugins.deletePlugin')}
            description={t('plugins.deletePluginConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleDelete(record)}
          >
            {btn('del', <DeleteOutlined />, t('plugins.deletePlugin'), () => undefined, true)}
          </Popconfirm>
        )}
      </Space>
    )
  }

  const columns: TableColumnsType<TeamCustomPluginItem> = [
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
      render: (v: number | null | undefined, record) => (
        <PluginClassifyTag classifies={classifies} classifyId={v} classifyName={record.classifyName} />
      ),
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isTeamOwned',
      key: 'isTeamOwned',
      width: 90,
      render: (v: boolean | null) => <TeamOwnedTag isTeamOwned={v} t={t} />,
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
      width: 320,
      fixed: 'right',
      render: (_, record) => renderActions(record),
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
          </>
        }
        queries={
          <Button icon={<ReloadOutlined />} onClick={reload} loading={loading}>
            {t('plugins.refresh')}
          </Button>
        }
        actions={
          <>
            <PluginViewToggle value={view} onChange={setView} />
            {canManage && (
              <>
                <Button onClick={openImportOpenApi}>{t('plugins.importOpenApi')}</Button>
                <Button type="primary" onClick={openImportMcp}>{t('plugins.importMcp')}</Button>
              </>
            )}
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
        filtered.length ? (
          <PluginCardGrid>
            {filtered.map((record) => (
              <PluginCard
                key={String(record.pluginId ?? record.pluginName ?? '')}
                avatarPath={record.avatarPath}
                title={record.title}
                name={record.pluginName ?? '-'}
                server={record.server}
                typeLabel={typeLabel(t, record.type)}
                typeColor={pluginTypeColor(record.type)}
                description={record.description}
                tags={
                  <>
                    <TeamOwnedTag isTeamOwned={record.isTeamOwned} t={t} />
                    <PluginClassifyTag classifies={classifies} classifyId={record.classifyId} classifyName={record.classifyName} />
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
        <DataTable<TeamCustomPluginItem>
          rowKey={(r) => String(r.pluginId ?? r.pluginName ?? '')}
          columns={columns}
          dataSource={filtered}
          loading={loading}
          sticky
          scroll={{ x: 1560 }}
          pagination={false}
        />
      )}

      <McpPluginModal
        open={mcpModalOpen}
        isEdit={editMode === 'mcp'}
        editing={editing ? toCustomPlugin(editing) : null}
        classifies={classifies}
        allowIsPublic={false}
        loadDetail={loadDetail}
        onOk={submitMcp}
        onCancel={() => {
          setMcpModalOpen(false)
          setEditing(null)
          setEditMode(null)
        }}
      />

      <OpenApiModal
        open={openApiModalOpen}
        isEdit={editMode === 'openApi'}
        editing={editing ? toCustomPlugin(editing) : null}
        classifies={classifies}
        showIsPublic={false}
        loadDetail={loadDetail}
        uploadFile={uploadFile}
        onOk={submitOpenApi}
        onCancel={() => {
          setOpenApiModalOpen(false)
          setEditing(null)
          setEditMode(null)
        }}
      />

      <FunctionListModal
        open={Boolean(functionPlugin)}
        plugin={functionPlugin ? toCustomPlugin(functionPlugin) : null}
        loadFunctions={loadFunctions}
        onCancel={() => setFunctionPlugin(null)}
      />
    </>
  )
}
