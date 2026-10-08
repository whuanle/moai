import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  AppstoreOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import { Button, Popconfirm, Space, Tag, Tooltip, theme } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { classifyLabel, type PluginClassify } from '@/api/classify'
import type { DynamicPluginTemplate } from '@/api/plugin'
import {
  deleteTeamPlugin,
  getTeamDynamicTemplates,
  runTeamPlugin,
  type TeamDynamicPluginItem,
} from '@/api/team-plugin'
import { DataTable, feedback } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import { DynamicPluginInstanceModal } from '@/pages/plugins/components/DynamicPluginInstanceModal'
import { PluginRunDrawer } from '@/pages/plugins/components/PluginRunDrawer'

interface TeamDynamicPluginPanelProps {
  teamId: number
  items: TeamDynamicPluginItem[]
  loading: boolean
  canManage: boolean
  classifies: PluginClassify[]
  reload: () => void
}

export function TeamDynamicPluginPanel({
  teamId,
  items,
  loading,
  canManage,
  classifies,
  reload,
}: TeamDynamicPluginPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TeamDynamicPluginItem | null>(null)
  const [drawerTarget, setDrawerTarget] = useState<TeamDynamicPluginItem | null>(null)
  const [filter, setFilter] = useState('all')

  const loadTemplates = useCallback(async () => {
    try {
      const res = await getTeamDynamicTemplates(teamId)
      setTemplates((res.items ?? []).filter((i) => i.isDynamic === true))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [teamId])

  useEffect(() => {
    void loadTemplates()
  }, [loadTemplates])

  const openCreate = () => {
    setEditing(null)
    setModalOpen(true)
  }

  const openEdit = (record: TeamDynamicPluginItem) => {
    setEditing(record)
    setModalOpen(true)
  }

  const closeModal = () => {
    setEditing(null)
    setModalOpen(false)
  }

  const handleDelete = async (record: TeamDynamicPluginItem) => {
    if (!record.pluginId) return
    await deleteTeamPlugin(teamId, String(record.pluginId))
    feedback.success(t('plugins.deletePluginSuccess'))
    if (editing?.pluginId === record.pluginId) setEditing(null)
    reload()
  }

  const filtered = useMemo(() => {
    if (filter === 'all') return items
    if (filter === 'uncategorized') return items.filter((i) => (i.classifyId ?? 0) === 0)
    const id = Number(filter)
    return items.filter((i) => i.classifyId === id)
  }, [items, filter])

  const filterTags = useMemo(
    () => [
      { value: 'all', label: t('plugins.classifyAll') },
      { value: 'uncategorized', label: t('plugins.classifyUncategorized') },
      ...classifies.map((c) => ({ value: String(c.classifyId), label: classifyLabel(c) })),
    ],
    [classifies, t],
  )

  /** 实例引用的模板已不在注册表（模板被下线）：运行与编辑都不可用，仅保留删除. */
  const isTemplateMissing = useCallback(
    (record: TeamDynamicPluginItem) =>
      Boolean(record.templeteKey) && !templates.some((tpl) => tpl.key === record.templeteKey),
    [templates],
  )

  const columns: TableColumnsType<TeamDynamicPluginItem> = [
    { title: t('plugins.colPluginName'), dataIndex: 'pluginName', width: 180 },
    { title: t('plugins.colTitle'), dataIndex: 'title', width: 150, ellipsis: true },
    {
      title: t('plugins.dynamicTemplate'),
      dataIndex: 'templeteKey',
      width: 200,
      render: (v: string | null, record) => {
        if (!v) return '-'
        return isTemplateMissing(record) ? (
          <Space size={4} wrap>
            <Tag>{v}</Tag>
            <Tag color="red">{t('plugins.templateMissing')}</Tag>
          </Space>
        ) : (
          <Tag color="blue">{v}</Tag>
        )
      },
    },
    {
      title: t('plugins.colIsSystem'),
      dataIndex: 'isTeamOwned',
      width: 100,
      render: (v: boolean | null) =>
        v ? <Tag color="green">{t('plugins.teamOwned')}</Tag> : <Tag color="geekblue">{t('plugins.isSystem')}</Tag>,
    },
    {
      title: t('plugins.colClassify'),
      dataIndex: 'classifyName',
      width: 130,
      render: (v: string | null, record) => {
        const classify = classifies.find((c) => c.classifyId === record.classifyId)
        if (classify) return <Tag>{classifyLabel(classify)}</Tag>
        return v ? <Tag>{v}</Tag> : <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>
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
      render: (_, record) => (
        <Space size={0}>
          <Tooltip title={isTemplateMissing(record) ? t('plugins.templateMissing') : t('plugins.run')}>
            <Button
              type="text"
              size="small"
              icon={<PlayCircleOutlined />}
              aria-label={t('plugins.run')}
              disabled={isTemplateMissing(record)}
              onClick={() => setDrawerTarget(record)}
            />
          </Tooltip>
          {canManage && record.isTeamOwned && (
            <>
              <Tooltip title={t('plugins.editPlugin')}>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={t('plugins.editPlugin')}
                  disabled={isTemplateMissing(record)}
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
            </>
          )}
        </Space>
      ),
    },
  ]

  return (
    <>
      <DataTable<TeamDynamicPluginItem>
        sticky
        rowKey={(r) => String(r.pluginId ?? r.pluginName ?? '')}
        columns={columns}
        dataSource={filtered}
        loading={loading}
        pagination={false}
        scroll={{ x: 1300 }}
        toolbar={
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
            {canManage && (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('plugins.createDynamicInstance')}
              </Button>
            )}
            <Button icon={<ReloadOutlined />} onClick={reload} loading={loading}>
              {t('plugins.refresh')}
            </Button>
            {canManage && (
              <Button
                icon={<AppstoreOutlined />}
                onClick={() => navigate(`/plugin/templates?teamId=${teamId}`)}
              >
                {t('plugins.templateList')}
              </Button>
            )}
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
          pluginKey={drawerTarget.pluginName ?? drawerTarget.instanceKey}
          paramsExample={drawerTarget.paramsExample}
          runPlugin={(payload) => runTeamPlugin({ teamId, ...payload })}
        />
      )}

      <DynamicPluginInstanceModal
        open={modalOpen}
        scope="team"
        teamId={teamId}
        templates={templates}
        classifies={classifies}
        existingKeys={items.map((i) => (i.instanceKey ?? i.pluginName) ?? '').filter(Boolean)}
        editing={editing}
        onSaved={reload}
        onClose={closeModal}
      />
    </>
  )
}
