import { Fragment, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  SearchOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import { Button, Form, Input, Popconfirm, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import type { PluginClassify } from '@/api/classify'
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
import { DataTable, feedback, QueryBar } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import { uploadOpenApiFileWith } from '@/utils/pluginFile'
import { McpPluginModal, type McpFormValues } from '@/pages/plugins/components/McpPluginModal'
import { OpenApiModal, type OpenApiFormValues } from '@/pages/plugins/components/OpenApiModal'
import { FunctionListModal } from '@/pages/plugins/components/FunctionListModal'

const { Text } = Typography

const TYPE_COLOR: Record<string, string> = {
  mcp: 'green',
  openApi: 'orange',
}

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

  const [searchName, setSearchName] = useState<string | undefined>(undefined)
  const [filterType, setFilterType] = useState<CustomPluginType | undefined>(undefined)
  const [classifyFilter, setClassifyFilter] = useState('all')

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
    reload()
  }

  const handleRefreshMcp = async (record: TeamCustomPluginItem) => {
    if (!record.pluginId) return
    await refreshTeamMcp(teamId, String(record.pluginId))
    feedback.success(t('plugins.refreshMcpSuccess'))
    reload()
  }

  const filtered = useMemo(() => {
    const keyword = searchName?.trim().toLowerCase()
    return items.filter((item) => {
      if (keyword && !(item.pluginName ?? '').toLowerCase().includes(keyword)) return false
      if (filterType && item.type !== filterType) return false
      if (classifyFilter === 'uncategorized' && (item.classifyId ?? 0) !== 0) return false
      if (classifyFilter !== 'all' && classifyFilter !== 'uncategorized' && item.classifyId !== Number(classifyFilter)) {
        return false
      }
      return true
    })
  }, [items, searchName, filterType, classifyFilter])

  const columns: TableColumnsType<TeamCustomPluginItem> = [
    {
      title: t('plugins.colPluginName'),
      dataIndex: 'pluginName',
      key: 'pluginName',
      width: 160,
      render: (v: string | null) => <Text strong>{v || '-'}</Text>,
    },
    { title: t('plugins.colTitle'), dataIndex: 'title', key: 'title', width: 140, ellipsis: true, render: (v: string | null) => v || '-' },
    {
      title: t('plugins.colType'),
      dataIndex: 'type',
      key: 'type',
      width: 100,
      render: (v: string | null) => <Tag color={TYPE_COLOR[v ?? '']}>{typeLabel(t, v)}</Tag>,
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isTeamOwned',
      key: 'isTeamOwned',
      width: 100,
      render: (v: boolean | null) =>
        v ? <Tag color="green">{t('plugins.teamOwned')}</Tag> : <Tag color="geekblue">{t('plugins.isSystem')}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      key: 'classifyName',
      width: 110,
      render: (v: string | null) => (v ? <Tag color="blue">{v}</Tag> : <Text type="secondary">-</Text>),
    },
    {
      title: t('plugins.colServer'),
      dataIndex: 'server',
      key: 'server',
      width: 180,
      ellipsis: true,
      render: (v: string | null) => (
        <Text type="secondary" style={{ fontFamily: 'monospace', fontSize: 12 }}>
          {v || '-'}
        </Text>
      ),
    },
    {
      title: t('plugins.formDescription'),
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
      render: (v: string | null) => <Text type="secondary">{v || '-'}</Text>,
    },
    { title: t('plugins.colCounter'), dataIndex: 'counter', key: 'counter', width: 90, render: (v: string | null) => v ?? 0 },
    {
      title: t('plugins.colCreateUser'),
      dataIndex: 'createUserName',
      key: 'createUserName',
      width: 110,
      ellipsis: true,
      render: (v: string | null) => v || '-',
    },
    {
      title: t('plugins.colAttachTime'),
      dataIndex: 'createTime',
      key: 'createTime',
      width: 160,
      render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
    },
    {
      title: t('plugins.colUpdateUser'),
      dataIndex: 'updateUserName',
      key: 'updateUserName',
      width: 110,
      ellipsis: true,
      render: (v: string | null) => v || '-',
    },
    {
      title: t('plugins.colUpdateTime'),
      dataIndex: 'updateTime',
      key: 'updateTime',
      width: 160,
      render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
    },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 150,
      fixed: 'right',
      render: (_, record) => (
        <Space size={0}>
          <Tooltip title={t('plugins.viewFunctions')}>
            <Button
              type="text"
              size="small"
              icon={<EyeOutlined />}
              aria-label={t('plugins.viewFunctions')}
              onClick={() => setFunctionPlugin(record)}
            />
          </Tooltip>
          {canManage && record.isTeamOwned && record.type === 'mcp' && (
            <Tooltip title={t('plugins.refreshMcp')}>
              <Button
                type="text"
                size="small"
                icon={<SyncOutlined />}
                aria-label={t('plugins.refreshMcp')}
                onClick={() => void handleRefreshMcp(record)}
              />
            </Tooltip>
          )}
          {canManage && record.isTeamOwned && (
            <>
              <Tooltip title={t('plugins.editPlugin')}>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={t('plugins.editPlugin')}
                  onClick={() => openEdit(record)}
                />
              </Tooltip>
              <Popconfirm
                title={t('plugins.deletePlugin')}
                description={t('plugins.deletePluginConfirm')}
                okButtonProps={{ danger: true }}
                onConfirm={() => void handleDelete(record)}
              >
                <Tooltip title={t('plugins.deletePlugin')}>
                  <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('plugins.deletePlugin')} />
                </Tooltip>
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ]

  const classifyTags = useMemo(
    () => [
      { value: 'all', label: t('plugins.classifyAll') },
      { value: 'uncategorized', label: t('plugins.classifyUncategorized') },
      ...classifies.map((c) => ({ value: String(c.classifyId), label: c.name })),
    ],
    [classifies, t],
  )

  return (
    <>
      <QueryBar
        onSearch={(values) => {
          setSearchName(typeof values.name === 'string' ? values.name.trim() || undefined : undefined)
          setFilterType((values.type as CustomPluginType | undefined) ?? undefined)
        }}
        onReset={() => {
          setSearchName(undefined)
          setFilterType(undefined)
          setClassifyFilter('all')
        }}
        loading={loading}
      >
        <Form.Item name="name">
          <Input
            placeholder={t('plugins.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={50}
            style={{ width: 240 }}
          />
        </Form.Item>
        <Form.Item name="type">
          <Select
            placeholder={t('plugins.filterType')}
            allowClear
            style={{ width: 140 }}
            options={[
              { value: 'mcp', label: t('plugins.typeMcp') },
              { value: 'openApi', label: t('plugins.typeOpenapi') },
            ]}
          />
        </Form.Item>
        {canManage && (
          <Form.Item>
            <Space>
              <Button type="primary" onClick={openImportMcp}>
                {t('plugins.importMcp')}
              </Button>
              <Button onClick={openImportOpenApi}>{t('plugins.importOpenApi')}</Button>
            </Space>
          </Form.Item>
        )}
      </QueryBar>

      <DataTable<TeamCustomPluginItem>
        rowKey={(r) => String(r.pluginId ?? r.pluginName ?? '')}
        columns={columns}
        dataSource={filtered}
        loading={loading}
        sticky
        scroll={{ x: 1400 }}
        toolbar={
          <Space size={4}>
            {classifyTags.map((item, index) => (
              <Fragment key={item.value}>
                {index > 0 && <Text type="secondary">|</Text>}
                <Tag.CheckableTag checked={classifyFilter === item.value} onChange={() => setClassifyFilter(item.value)}>
                  {item.label}
                </Tag.CheckableTag>
              </Fragment>
            ))}
          </Space>
        }
      />

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
