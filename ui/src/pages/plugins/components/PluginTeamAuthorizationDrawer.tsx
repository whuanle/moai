import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Drawer, Popconfirm, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { pluginAuthorizationApi } from '@/api/plugin'
import { getAllTeams, type AllTeamItem } from '@/api/team'
import { DataTable, feedback } from '@/design-system'

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
  const [selectedTeamIds, setSelectedTeamIds] = useState<number[]>([])

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
    setSelectedTeamIds([])
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

  const handleAddTeams = async () => {
    if (!pluginId || selectedTeamIds.length === 0) return
    const nextTeamIds = [...authorizedItems.map((x) => x.teamId), ...selectedTeamIds]
    const ok = await runAction(
      () => pluginAuthorizationApi.updatePluginTeamAuthorization(pluginId, nextTeamIds),
      'plugins.authAddSuccess',
    )
    if (ok) setSelectedTeamIds([])
  }

  const handleRemoveTeam = async (teamId: number) => {
    if (!pluginId) return
    const nextTeamIds = authorizedItems.map((x) => x.teamId).filter((id) => id !== teamId)
    await runAction(
      () => pluginAuthorizationApi.updatePluginTeamAuthorization(pluginId, nextTeamIds),
      'plugins.authRemoveSuccess',
    )
  }

  const teamOptions = useMemo(
    () =>
      teams
        .filter((team) => !authorizedItems.some((item) => item.teamId === Number(team.teamId)))
        .map((team) => ({
          value: Number(team.teamId),
          label: `${team.name}${team.isDisable ? `（${t('models.teamDisabled')}）` : ''}`,
        })),
    [teams, authorizedItems, t],
  )

  const columns: TableColumnsType<(typeof authorizedItems)[number]> = [
    { title: t('plugins.authColTeam'), dataIndex: 'teamName', width: 200 },
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
          <Tooltip title={t('plugins.authRemove')}>
            <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('plugins.authRemove')} />
          </Tooltip>
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
            <>
              {authorizedItems.length === 0 && !loading && (
                <Text type="secondary">{t('plugins.authNoTeams')}</Text>
              )}
              <DataTable<(typeof authorizedItems)[number]>
                rowKey="teamId"
                columns={columns}
                dataSource={authorizedItems}
                loading={loading}
                pagination={false}
                size="small"
                toolbar={
                  <Space size={12} wrap>
                    <Select
                      mode="multiple"
                      style={{ minWidth: 240 }}
                      placeholder={t('plugins.authSelectTeamPlaceholder')}
                      options={teamOptions}
                      value={selectedTeamIds}
                      onChange={(v) => setSelectedTeamIds(v)}
                      allowClear
                    />
                    <Button
                      type="primary"
                      icon={<PlusOutlined />}
                      disabled={selectedTeamIds.length === 0}
                      loading={submitting}
                      onClick={() => void handleAddTeams()}
                    >
                      {t('plugins.authAddTeam')}
                    </Button>
                  </Space>
                }
                onRefresh={loadAuthorization}
                refreshLoading={loading}
              />
            </>
          )}
        </>
      ) : (
        <Text type="secondary">{t('plugins.noPlugin')}</Text>
      )}
    </Drawer>
  )
}
