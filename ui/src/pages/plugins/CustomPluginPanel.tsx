import { Fragment, useCallback, useEffect, useMemo, useState } from 'react'
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
import {
  customPluginApi,
  type CustomPlugin,
  type CustomPluginSort,
  type CustomPluginType,
} from '@/api/plugin'
import { DataTable, feedback, QueryBar } from '@/design-system'
import { McpPluginModal, type McpFormValues } from './components/McpPluginModal'
import { OpenApiModal, type OpenApiFormValues } from './components/OpenApiModal'
import { FunctionListModal } from './components/FunctionListModal'

const { Text } = Typography

const TYPE_COLOR: Record<string, string> = {
  mcp: 'green',
  openApi: 'orange',
}

function typeLabel(t: (k: string) => string, type: string | null): string {
  if (type === 'mcp') return t('plugins.typeMcp')
  if (type === 'openApi') return t('plugins.typeOpenapi')
  return String(type ?? '-')
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
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
  const [searchName, setSearchName] = useState<string | undefined>(undefined)
  const [filterType, setFilterType] = useState<CustomPluginType | undefined>(undefined)
  const [sort, setSort] = useState<CustomPluginSort>({ field: null, order: null })
  const [classifyFilter, setClassifyFilter] = useState('all')

  const [mcpModalOpen, setMcpModalOpen] = useState(false)
  const [openApiModalOpen, setOpenApiModalOpen] = useState(false)
  const [editing, setEditing] = useState<CustomPlugin | null>(null)
  const [editMode, setEditMode] = useState<'mcp' | 'openApi' | null>(null)
  const [functionPlugin, setFunctionPlugin] = useState<CustomPlugin | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const classifyId = classifyFilter === 'all' ? undefined : Number(classifyFilter)
      const data = await customPluginApi.getCustomPlugins({
        name: searchName,
        type: filterType,
        classifyId,
        sort,
      })
      setItems(data)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [searchName, filterType, classifyFilter, sort])

  useEffect(() => {
    void load()
  }, [load])

  const handleSearch = (values: Record<string, unknown>) => {
    setSearchName(typeof values.name === 'string' ? values.name.trim() || undefined : undefined)
    setFilterType((values.type as CustomPluginType | undefined) ?? undefined)
  }

  const handleReset = () => {
    setSearchName(undefined)
    setFilterType(undefined)
    setClassifyFilter('all')
    setSort({ field: null, order: null })
  }

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

  const columns: TableColumnsType<CustomPlugin> = [
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
      title: t('plugins.colClassify'),
      dataIndex: 'classifyId',
      key: 'classifyId',
      width: 110,
      render: (v: number | null | undefined) => {
        if (!v) return <Text type="secondary">-</Text>
        const classify = classifies.find((item) => item.classifyId === v)
        return classify ? <Tag color="blue">{classify.name}</Tag> : '-'
      },
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
    {
      title: t('plugins.colIsPublic'),
      dataIndex: 'isPublic',
      key: 'isPublic',
      width: 80,
      render: (v: boolean | null) => (
        <Tag color={v ? 'success' : 'warning'}>{v ? t('plugins.isPublic') : t('plugins.notPublic')}</Tag>
      ),
    },
    {
      title: t('plugins.colCounter'),
      dataIndex: 'counter',
      key: 'counter',
      width: 90,
      render: (v: number | null) => v ?? 0,
    },
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
          {record.type === 'mcp' && (
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
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                aria-label={t('plugins.deletePlugin')}
              />
            </Tooltip>
          </Popconfirm>
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
      <QueryBar onSearch={handleSearch} onReset={handleReset} loading={loading}>
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
        <Form.Item>
          <Space>
            <Button type="primary" onClick={openImportMcp}>
              {t('plugins.importMcp')}
            </Button>
            <Button onClick={openImportOpenApi}>{t('plugins.importOpenApi')}</Button>
          </Space>
        </Form.Item>
      </QueryBar>

      <DataTable<CustomPlugin>
        rowKey="pluginId"
        columns={columns}
        dataSource={items}
        loading={loading}
        sticky
        scroll={{ x: 1200 }}
        toolbar={
          <Space size={4}>
            {classifyTags.map((item, index) => (
              <Fragment key={item.value}>
                {index > 0 && <Text type="secondary">|</Text>}
                <Tag.CheckableTag
                  checked={classifyFilter === item.value}
                  onChange={() => setClassifyFilter(item.value)}
                >
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
        editing={editing}
        classifies={classifies}
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
        editing={editing}
        classifies={classifies}
        onOk={submitOpenApi}
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
    </>
  )
}
