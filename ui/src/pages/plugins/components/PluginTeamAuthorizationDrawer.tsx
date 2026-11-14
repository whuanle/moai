import { useCallback, useEffect, useMemo, useState } from 'react'
import { Avatar, Button, Drawer, Popconfirm, Select, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { DeleteOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { pluginAuthorizationApi } from '@/api/plugin'
import { getAllTeams, type AllTeamItem } from '@/api/team'
import { DataTable, feedback } from '@/design-system'
import { resolveStorageUrl } from '@/utils/storage'

const { Text } = Typography

interface PluginTeamAuthorizationDrawerProps {
  pluginId: string | null
  pluginName: string | null
  open: boolean
  onClose: () => void
}

/** 私有系统插件的团队授权抽屉：加载、添加、移除授权团队（公开插件返回空列表并提示）. */
export function PluginTeamAuthorizationDrawer({
  pluginId,
  pluginName,
  open,
  onClose,
}: PluginTeamAuthorizationDrawerProps) {
  const { t } = useTranslation()

  const [authorization, setAuthorization] = useState<{
    isPublic: boolean | null
    items: { teamId: number; teamName: string }[]
  } | null>(null)
  const [loading, setLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [teams, setTeams] = useState<AllTeamItem[]>([])
  /** 写入中的团队 id：立即从下拉剔除并乐观显示在表格，刷新到达后清除. */
  const [pendingIds, setPendingIds] = useState<number[]>([])

  const loadAuthorization = useCallback(async () => {
    if (!pluginId) return
    setLoading(true)
    try {
      setAuthorization(await pluginAuthorizationApi.getPluginTeamAuthorization(pluginId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [pluginId])

  useEffect(() => {
    if (!open) return
    setPendingIds([])
    void loadAuthorization()
  }, [open, loadAuthorization])

  useEffect(() => {
    if (!open) return
    getAllTeams()
      .then(setTeams)
      .catch(() => setTeams([]))
  }, [open])

  const authorizedItems = useMemo(
    () => (authorization?.items ?? []).map((x) => ({ teamId: Number(x.teamId ?? 0), teamName: String(x.teamName ?? '') })),
    [authorization],
  )

  const teamMap = useMemo(() => new Map(teams.map((team) => [Number(team.teamId), team])), [teams])

  /** 表格行 = 已授权 + 写入中（乐观显示，刷新到达后由真实数据替换）. */
  const tableRows = useMemo(() => {
    const map = new Map(authorizedItems.map((x) => [x.teamId, x]))
    pendingIds.forEach((id) => {
      if (!map.has(id)) map.set(id, { teamId: id, teamName: teamMap.get(id)?.name ?? '' })
    })
    return Array.from(map.values()).sort((a, b) => a.teamId - b.teamId)
  }, [authorizedItems, pendingIds, teamMap])

  const isPublic = authorization?.isPublic === true

  const runAction = async (action: () => Promise<void>, successKey: string): Promise<boolean> => {
    if (!pluginId) return false
    setSubmitting(true)
    try {
      await action()
      feedback.success(t(successKey))
      await loadAuthorization()
      return true
    } catch {
      // 错误已由全局请求中间件统一提示
      return false
    } finally {
      setSubmitting(false)
    }
  }

  /** 下拉勾选即写入授权（整体覆盖，去重合并）：勾选瞬间从下拉剔除并乐观上表，刷新到达后对账. */
  const handleAddTeams = async (selected: number[]) => {
    if (!pluginId || selected.length === 0) return
    const known = new Set([...authorizedItems.map((x) => x.teamId), ...pendingIds])
    const newIds = Array.from(new Set(selected)).filter((id) => !known.has(id))
    if (newIds.length === 0) return
    const merged = Array.from(new Set([...authorizedItems.map((x) => x.teamId), ...pendingIds, ...newIds]))
    setPendingIds((prev) => [...prev, ...newIds])
    try {
      await runAction(
        () => pluginAuthorizationApi.updatePluginTeamAuthorization(pluginId, merged),
        'plugins.authAddSuccess',
      )
    } finally {
      setPendingIds((prev) => prev.filter((id) => !newIds.includes(id)))
    }
  }

  const handleRemoveTeam = async (teamId: number) => {
    if (!pluginId) return
    const nextTeamIds = authorizedItems.map((x) => x.teamId).filter((id) => id !== teamId)
    await runAction(
      () => pluginAuthorizationApi.updatePluginTeamAuthorization(pluginId, nextTeamIds),
      'plugins.authRemoveSuccess',
    )
  }

  /** 下拉选项 = 全部启用团队 − 已授权 − 写入中. */
  const teamOptions = useMemo(() => {
    const excluded = new Set<number>([
      ...authorizedItems.map((x) => x.teamId),
      ...pendingIds,
    ])
    return teams
      .filter((team) => !team.isDisable)
      .filter((team) => !excluded.has(Number(team.teamId)))
      .map((team) => ({
        value: Number(team.teamId),
        label: `${team.name}`,
      }))
  }, [teams, authorizedItems, pendingIds])

  /** 团队头像 + 名称（授权清单里可能存在已删除的团队，找不到时回退首字母）. */
  const renderTeam = (teamId: number, teamName: string) => {
    const team = teamMap.get(teamId)
    const name = team?.name || teamName || '-'
    return (
      <Space size={8}>
        <Avatar size={22} src={team?.avatar ? resolveStorageUrl(team.avatar) : undefined}>
          {name.slice(0, 1).toUpperCase()}
        </Avatar>
        <span>{name}</span>
      </Space>
    )
  }

  const columns: TableColumnsType<(typeof authorizedItems)[number]> = [
    {
      title: t('plugins.authColTeam'),
      dataIndex: 'teamName',
      width: 200,
      render: (_, record) => renderTeam(record.teamId, record.teamName),
    },
    {
      title: t('plugins.colActions'),
      key: 'actions',
      width: 120,
      render: (_, record) => (
        <Popconfirm
          title={t('plugins.authRemoveConfirm')}
          okButtonProps={{ danger: true }}
          onConfirm={() => void handleRemoveTeam(record.teamId)}
        >
          <Button type="link" size="small" danger icon={<DeleteOutlined />} aria-label={t('plugins.authRemove')}>
            {t('plugins.authRemove')}
          </Button>
        </Popconfirm>
      ),
    },
  ]

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={
        <Space size={8} wrap>
          <span>{pluginName}</span>
          <Tag color="geekblue">{t('plugins.authorizationTitle')}</Tag>
        </Space>
      }
      width={520}
      destroyOnClose
    >
      {pluginId ? (
        <>
          {isPublic ? (
            <Text type="secondary">{t('plugins.authIsPublic')}</Text>
          ) : (
            <DataTable<(typeof authorizedItems)[number]>
              flat
              rowKey="teamId"
              columns={columns}
              dataSource={tableRows}
              loading={loading}
              pagination={false}
              size="small"
              toolbar={
                <Select
                  mode="multiple"
                  style={{ minWidth: 240, maxWidth: '100%' }}
                  placeholder={t('plugins.authSelectTeamPlaceholder')}
                  options={teamOptions}
                  value={[]}
                  onChange={(v) => void handleAddTeams(v as number[])}
                  disabled={submitting}
                  optionRender={(option) => {
                    const team = teamMap.get(Number(option.value))
                    const name = String(option.label ?? '')
                    return (
                      <Space size={6}>
                        <Avatar size={18} src={team?.avatar ? resolveStorageUrl(team.avatar) : undefined}>
                          {name.slice(0, 1).toUpperCase()}
                        </Avatar>
                        <span>{name}</span>
                      </Space>
                    )
                  }}
                />
              }
              onRefresh={loadAuthorization}
              refreshLoading={loading}
            />
          )}
        </>
      ) : (
        <Text type="secondary">{t('plugins.noPlugin')}</Text>
      )}
    </Drawer>
  )
}
