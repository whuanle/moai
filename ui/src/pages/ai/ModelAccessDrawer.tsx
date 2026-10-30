import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Drawer, Popconfirm, Select, Space, Switch, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { aichannelApi, type AIModelItem, type ModelAuthorization } from '@/api/aichannel'
import { getAllTeams, type AllTeamItem } from '@/api/team'
import { DataTable, feedback } from '@/design-system'

interface ModelAccessDrawerProps {
  model: AIModelItem | null
  open: boolean
  onClose: () => void
  /** 可见性变更后通知父级刷新模型列表. */
  onVisibilityChanged: () => void
}

/** 模型授权抽屉：私有模型管理授权团队集合；公开模型所有团队可用. */
export function ModelAccessDrawer({ model, open, onClose, onVisibilityChanged }: ModelAccessDrawerProps) {
  const { t } = useTranslation()
  const [authorization, setAuthorization] = useState<ModelAuthorization | null>(null)
  const [loading, setLoading] = useState(false)
  const [teams, setTeams] = useState<AllTeamItem[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [selectedTeamIds, setSelectedTeamIds] = useState<number[]>([])

  const isPublic = authorization?.isPublic ?? model?.isPublic ?? true
  const modelId = model?.id ?? null
  /** 归一化后的授权团队行（后端字段可空，统一收敛为稳定类型）. */
  const authorizedItems = useMemo(
    () =>
      (authorization?.items ?? []).map((x) => ({
        teamId: Number(x.teamId ?? 0),
        teamName: String(x.teamName ?? ''),
      })),
    [authorization],
  )

  const loadAuthorization = useCallback(async () => {
    if (!model?.id) return
    setLoading(true)
    try {
      setAuthorization(await aichannelApi.getModelAuthorization(model.id))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [model?.id])

  useEffect(() => {
    if (!open) return
    void loadAuthorization()
  }, [open, loadAuthorization])

  useEffect(() => {
    if (!open || isPublic) return
    // 私有模型才需要候选团队列表
    getAllTeams()
      .then(setTeams)
      .catch(() => setTeams([]))
  }, [open, isPublic])

  const runAction = async (action: () => Promise<void>, successKey: string) => {
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

  const handleVisibilityChange = async (checked: boolean) => {
    if (!modelId) return
    await runAction(() => aichannelApi.updateModelVisibility(modelId, checked), 'models.saveSuccess')
    onVisibilityChanged()
  }

  const handleAddTeams = async () => {
    if (!modelId || selectedTeamIds.length === 0) return
    const nextTeamIds = [...authorizedItems.map((x) => x.teamId), ...selectedTeamIds]
    const ok = await runAction(
      () => aichannelApi.updateModelAuthorization(modelId, nextTeamIds),
      'models.authAddSuccess',
    )
    if (ok) setSelectedTeamIds([])
  }

  const handleRemoveTeam = async (teamId: number) => {
    if (!modelId) return
    const nextTeamIds = authorizedItems.map((x) => x.teamId).filter((id) => id !== teamId)
    await runAction(() => aichannelApi.updateModelAuthorization(modelId, nextTeamIds), 'models.authRemoveSuccess')
  }

  const teamOptions = useMemo(
    () =>
      teams
        .filter((team) => !team.isDisable)
        .filter((team) => !authorizedItems.some((item) => item.teamId === Number(team.teamId)))
        .map((team) => ({
          value: Number(team.teamId),
          label: `${team.name}`,
        })),
    [teams, authorizedItems],
  )

  const columns: TableColumnsType<(typeof authorizedItems)[number]> = [
    { title: t('models.authColTeam'), dataIndex: 'teamName', width: 220 },
    {
      title: t('models.colActions'),
      key: 'actions',
      width: 90,
      render: (_, record) => (
        <Popconfirm
          title={t('models.authRemoveConfirm')}
          okButtonProps={{ danger: true }}
          onConfirm={() => void handleRemoveTeam(record.teamId)}
        >
          <Button
            type="text"
            size="small"
            danger
            icon={<DeleteOutlined />}
            aria-label={t('models.authRemove')}
          />
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
          <span>{model?.name}</span>
          <Tag color="geekblue">{t('models.accessTitle')}</Tag>
        </Space>
      }
      width={640}
      destroyOnClose
    >
      {modelId && model && (
        <>
          <Space size={12} wrap style={{ marginBottom: 16 }}>
            <Typography.Text strong>{t('models.isPublic')}</Typography.Text>
            <Switch checked={isPublic} loading={submitting} onChange={handleVisibilityChange} />
            <Typography.Text type="secondary">{t('models.visibilityHint')}</Typography.Text>
          </Space>

          {isPublic ? (
            <Typography.Paragraph type="secondary">{t('models.publicAllTeamsHint')}</Typography.Paragraph>
          ) : (
            <>
              {authorizedItems.length === 0 && !loading && (
                <Typography.Paragraph type="secondary">{t('models.authNoTeams')}</Typography.Paragraph>
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
                      style={{ minWidth: 260 }}
                      placeholder={t('models.authSelectTeamPlaceholder')}
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
                      {t('models.authAddTeam')}
                    </Button>
                  </Space>
                }
                onRefresh={loadAuthorization}
                refreshLoading={loading}
              />
            </>
          )}
        </>
      )}
    </Drawer>
  )
}
