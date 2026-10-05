import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  AppstoreOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  ShareAltOutlined,
} from '@ant-design/icons'
import { Button, Popconfirm, Space, Tag, Tooltip, theme } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { classifyLabel, type PluginClassify } from '@/api/classify'
import {
  pluginApi,
  type ClassifyFilter,
  type DynamicPluginManageItem,
  type DynamicPluginTemplate,
} from '@/api/plugin'
import { DataTable, feedback } from '@/design-system'
import { PluginAvatar } from './components/PluginAvatarUpload'
import { DynamicPluginInstanceModal } from './components/DynamicPluginInstanceModal'
import { PluginRunDrawer } from './components/PluginRunDrawer'
import { PluginTeamAuthorizationDrawer } from './components/PluginTeamAuthorizationDrawer'
import { formatDateTime } from '@/utils/datetime'


interface DynamicPluginPanelProps {
  classifies: PluginClassify[]
}

export function DynamicPluginPanel({ classifies }: DynamicPluginPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const [items, setItems] = useState<DynamicPluginManageItem[]>([])
  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<DynamicPluginManageItem | null>(null)
  const [drawerTarget, setDrawerTarget] = useState<DynamicPluginManageItem | null>(null)
  const [authPlugin, setAuthPlugin] = useState<DynamicPluginManageItem | null>(null)
  const [filter, setFilter] = useState<ClassifyFilter>('all')

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
    if (filter === 'all') return items
    if (filter === 'uncategorized') return items.filter((i) => i.classifyId === 0 || !i.classifyId)
    const id = Number(filter)
    return items.filter((i) => i.classifyId === id)
  }, [items, filter])

  const filterTags = useMemo(
    () => [
      { value: 'all' as const, label: t('plugins.classifyAll') },
      { value: 'uncategorized' as const, label: t('plugins.classifyUncategorized') },
      ...classifies.map((c) => ({ value: String(c.classifyId), label: classifyLabel(c) })),
    ],
    [classifies, t],
  )

  const columns: TableColumnsType<DynamicPluginManageItem> = [
    {
      title: t('plugins.colPluginName'),
      dataIndex: 'pluginName',
      width: 180,
      render: (v: string | null, record) => (
        <Space size={8}>
          <PluginAvatar objectKey={record.avatarPath} title={record.title ?? v} />
          <span>{v || '-'}</span>
        </Space>
      ),
    },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 150, ellipsis: true },
    {
      title: t('plugins.dynamicTemplate'),
      dataIndex: 'templeteKey',
      width: 160,
      render: (v: string | null) => (v ? <Tag color="blue">{v}</Tag> : '-'),
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 130,
      render: (v: string | null, record: DynamicPluginManageItem) => {
        const classify = classifies.find((c) => c.classifyId === record.classifyId)
        return classify ? (
          <Tag>{classifyLabel(classify)}</Tag>
        ) : v ? (
          <Tag>{v}</Tag>
        ) : (
          <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>
        )
      },
    },
    { title: t('plugins.colCreateUser'), dataIndex: 'createUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colCreateTime'), dataIndex: 'createTime', width: 160, render: (v: string | null) => (
      <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>
    ) },
    { title: t('plugins.colUpdateUser'), dataIndex: 'updateUserName', width: 110, ellipsis: true, render: (v: string | null) => v || '-' },
    { title: t('plugins.colUpdateTime'), dataIndex: 'updateTime', width: 160, render: (v: string | null) => (
      <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>
    ) },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 150,
      fixed: 'right',
      render: (_: unknown, record: DynamicPluginManageItem) => (
        <Space size={0}>
          <Tooltip title={t('plugins.run')}>
            <Button
              type="text"
              size="small"
              icon={<PlayCircleOutlined />}
              aria-label={t('plugins.run')}
              onClick={() => setDrawerTarget(record)}
            />
          </Tooltip>
          {record.isPublic === false && (
            <Tooltip title={t('plugins.authorization')}>
              <Button
                type="text"
                size="small"
                icon={<ShareAltOutlined />}
                aria-label={t('plugins.authorization')}
                onClick={() => setAuthPlugin(record)}
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
            title={t('plugins.deletePluginConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleDelete(record)}
          >
            <Tooltip title={t('plugins.deletePlugin')}>
              <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('plugins.deletePlugin')} />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      <DataTable<DynamicPluginManageItem>
        sticky
        scroll={{ x: 'max-content' }}
        rowKey="id"
        columns={columns}
        dataSource={filtered}
        loading={loading}
        pagination={false}
        toolbar={
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('plugins.createDynamicInstance')}
            </Button>
            <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
              {t('plugins.refresh')}
            </Button>
            <Button icon={<AppstoreOutlined />} onClick={() => navigate('/plugin/templates')}>
              {t('plugins.templateList')}
            </Button>
            {filterTags.map((item, index) => (
              <span key={item.value} style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                {index > 0 && (
                  <span className="classify-divider" style={{ color: token.colorTextQuaternary }}>
                    |
                  </span>
                )}
                <Tag.CheckableTag checked={filter === item.value} onChange={() => setFilter(item.value)}>
                  {item.label}
                </Tag.CheckableTag>
              </span>
            ))}
          </div>
        }
      />

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
