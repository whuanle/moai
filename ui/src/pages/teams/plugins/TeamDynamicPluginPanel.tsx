import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  AppstoreOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  TagsOutlined,
} from '@ant-design/icons'
import { Button, Input, Popconfirm, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { classifyLabel, type PluginClassify } from '@/api/classify'
import type { DynamicPluginTemplate, PluginToolItem } from '@/api/plugin'
import {
  deleteTeamPlugin,
  getTeamDynamicTemplates,
  runTeamPlugin,
  type TeamDynamicPluginItem,
} from '@/api/team-plugin'
import { AuditCell, DataTable, feedback, PageToolbar } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { DynamicPluginInstanceModal } from '@/pages/plugins/components/DynamicPluginInstanceModal'
import { PluginRunDrawer } from '@/pages/plugins/components/PluginRunDrawer'
import { PluginSemanticModal } from '@/pages/plugins/components/PluginSemanticModal'
import { PluginAvatar } from '@/pages/plugins/components/PluginAvatarUpload'
import {
  PluginCard,
  PluginCardEmpty,
  PluginCardGrid,
  PluginClassifyTag,
  PluginViewToggle,
  type PluginView,
} from '@/pages/plugins/components/PluginCardView'

const { Text } = Typography

/** 归属标签：团队自有 / 系统（团队页以此替代系统页的可见性标签）. */
function TeamOwnedTag({ isTeamOwned, t }: { isTeamOwned: boolean | null | undefined; t: (k: string) => string }) {
  return isTeamOwned ? (
    <Tag color="green">{t('plugins.teamOwned')}</Tag>
  ) : (
    <Tag color="geekblue">{t('plugins.colIsSystem')}</Tag>
  )
}

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
  const navigate = useNavigate()
  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TeamDynamicPluginItem | null>(null)
  const [drawerTarget, setDrawerTarget] = useState<TeamDynamicPluginItem | null>(null)
  const [runTools, setRunTools] = useState<PluginToolItem[] | null>(null)
  const [semanticTarget, setSemanticTarget] = useState<TeamDynamicPluginItem | null>(null)
  const [filter, setFilter] = useState('all')
  const [searchText, setSearchText] = useState('')
  const [view, setView] = useState<PluginView>('card')

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
    const keyword = searchText.trim().toLowerCase()
    return items.filter((i) => {
      if (keyword && !`${i.title ?? ''}${i.pluginName ?? ''}`.toLowerCase().includes(keyword)) return false
      if (filter === 'all') return true
      if (filter === 'uncategorized') return (i.classifyId ?? 0) === 0
      return i.classifyId === Number(filter)
    })
  }, [items, filter, searchText])

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

  /** 打开运行抽屉：从已加载的团队模板清单取工具清单（团队门禁端点，含 tools 字段），多工具插件可在抽屉内切换. */
  const openRun = (record: TeamDynamicPluginItem) => {
    setDrawerTarget(record)
    const templateKey = record.templeteKey
    const template = templateKey ? templates.find((tpl) => tpl.key === templateKey) : null
    // 主客户端生成的 QueryPluginToolItem 字段可空，归一为运行抽屉的形态
    setRunTools(
      template?.tools?.length
        ? template.tools.map((x) => ({
            key: String(x.key ?? 'run'),
            title: String(x.title ?? ''),
            description: String(x.description ?? ''),
            paramsExample: x.paramsExample ?? null,
          }))
        : null,
    )
  }

  /** 编辑/删除仅限团队自有插件且具备管理权限，系统授权插件只读；运行对所有成员可用. */
  const canOperate = (record: TeamDynamicPluginItem) => canManage && Boolean(record.isTeamOwned)

  /** 行/卡片操作区：运行、编辑内联（模板下线时禁用），删除走 Popconfirm；卡片底栏用图标+Tooltip 紧凑形态. */
  const renderActions = (record: TeamDynamicPluginItem, iconOnly = false) => {
    const missing = isTemplateMissing(record)
    const manageable = canOperate(record)
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
        {btn('run', <PlayCircleOutlined />, t('plugins.run'), () => void openRun(record), missing)}
        {manageable && btn('edit', <EditOutlined />, t('plugins.editPlugin'), () => openEdit(record), missing)}
        {manageable && btn('semantic', <TagsOutlined />, t('plugins.semanticConfig'), () => setSemanticTarget(record))}
        {manageable && (
          <Popconfirm
            title={t('plugins.deletePluginConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleDelete(record)}
          >
            {btn('del', <DeleteOutlined />, t('plugins.deletePlugin'), () => undefined, false, true)}
          </Popconfirm>
        )}
      </Space>
    )
  }

  const renderTemplateTag = (record: TeamDynamicPluginItem) => {
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

  const columns: TableColumnsType<TeamDynamicPluginItem> = [
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
      dataIndex: 'classifyId',
      key: 'classifyId',
      width: 120,
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
      render: (_, record) => renderActions(record),
    },
  ]

  return (
    <>
      <PageToolbar
        filters={
          <Input
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder={t('plugins.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={50}
            style={{ width: 240 }}
          />
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
                <Button icon={<AppstoreOutlined />} onClick={() => navigate(`/team/${teamId}/plugin/templates`)}>
                  {t('plugins.templateList')}
                </Button>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                  {t('plugins.createDynamicInstance')}
                </Button>
              </>
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
                key={String(record.pluginId ?? record.instanceKey ?? record.pluginName ?? '')}
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
                    <PluginClassifyTag classifies={classifies} classifyId={record.classifyId} classifyName={record.classifyName} />
                    <TeamOwnedTag isTeamOwned={record.isTeamOwned} t={t} />
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
        <DataTable<TeamDynamicPluginItem>
          sticky
          scroll={{ x: 'max-content' }}
          rowKey={(r) => String(r.pluginId ?? r.instanceKey ?? r.pluginName ?? '')}
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
          pluginKey={drawerTarget.pluginName ?? drawerTarget.instanceKey}
          paramsExample={drawerTarget.paramsExample}
          tools={runTools}
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

      <PluginSemanticModal
        open={Boolean(semanticTarget)}
        pluginId={semanticTarget?.pluginId ?? null}
        pluginTitle={semanticTarget?.title ?? null}
        teamId={teamId}
        onClose={() => setSemanticTarget(null)}
        onSaved={reload}
      />
    </>
  )
}
