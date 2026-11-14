import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Modal, Select, Space, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { aichannelApi, type AIChannelItem } from '@/api/aichannel'
import { getAllTeams, type AllTeamItem } from '@/api/team'
import { feedback } from '@/design-system'

const { Text } = Typography

interface ChannelAccessModalProps {
  channel: AIChannelItem | null
  open: boolean
  onClose: () => void
  /** 操作成功后通知父级刷新模型列表. */
  onSaved: () => void
}

/**
 * 渠道级批量授权弹窗：授权关系在模型上，渠道只是批量操作入口.
 * 勾选团队后点「授权」= 为渠道下所有私有模型补齐授权（已授权忽略）；
 * 点「撤销」= 移除这些团队的授权（未授权跳过）。两个方向均幂等增量，不影响未勾选的团队.
 */
export function ChannelAccessModal({ channel, open, onClose, onSaved }: ChannelAccessModalProps) {
  const { t } = useTranslation()
  const [teams, setTeams] = useState<AllTeamItem[]>([])
  const [teamsLoading, setTeamsLoading] = useState(false)
  const [selectedTeamIds, setSelectedTeamIds] = useState<number[]>([])
  const [granting, setGranting] = useState(false)
  const [revoking, setRevoking] = useState(false)

  useEffect(() => {
    if (!open) return
    setSelectedTeamIds([])
    setTeamsLoading(true)
    getAllTeams()
      .then(setTeams)
      .catch(() => setTeams([]))
      .finally(() => setTeamsLoading(false))
  }, [open])

  const teamOptions = useMemo(
    () =>
      teams
        .filter((team) => !team.isDisable)
        .map((team) => ({ value: Number(team.teamId), label: `${team.name}` })),
    [teams],
  )

  const hasSelection = selectedTeamIds.length > 0

  const runAction = async (action: () => Promise<void>, successKey: string, setBusy: (v: boolean) => void) => {
    setBusy(true)
    try {
      await action()
      feedback.success(t(successKey))
      // 操作后清空勾选，留在弹窗便于继续做下一批授权/撤销
      setSelectedTeamIds([])
      onSaved()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBusy(false)
    }
  }

  const handleGrant = () => {
    if (!channel?.id || !hasSelection) return
    const channelId = channel.id
    void runAction(
      () => aichannelApi.updateChannelAuthorization(channelId, { grantTeamIds: [...selectedTeamIds], revokeTeamIds: [] }),
      'models.channelAuthGrantSuccess',
      setGranting,
    )
  }

  const handleRevoke = () => {
    if (!channel?.id || !hasSelection) return
    const channelId = channel.id
    void runAction(
      () => aichannelApi.updateChannelAuthorization(channelId, { grantTeamIds: [], revokeTeamIds: [...selectedTeamIds] }),
      'models.channelAuthRevokeSuccess',
      setRevoking,
    )
  }

  return (
    <Modal
      open={open}
      title={
        <Space size={8} wrap>
          <span>{t('models.channelAuthTitle')}</span>
          <Text type="secondary" style={{ fontSize: 13, fontWeight: 400 }}>{channel?.name}</Text>
        </Space>
      }
      footer={
        <Space size={8} style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Button onClick={onClose}>{t('common.cancel')}</Button>
          <Button danger disabled={!hasSelection} loading={revoking} onClick={handleRevoke}>
            {t('models.channelAuthRevoke')}
          </Button>
          <Button type="primary" disabled={!hasSelection} loading={granting} onClick={handleGrant}>
            {t('models.channelAuthGrant')}
          </Button>
        </Space>
      }
      onCancel={onClose}
      destroyOnClose
      maskClosable={false}
      width={560}
    >
      <Select
        mode="multiple"
        style={{ width: '100%' }}
        placeholder={t('models.authSelectTeamPlaceholder')}
        options={teamOptions}
        value={selectedTeamIds}
        loading={teamsLoading}
        onChange={(v) => setSelectedTeamIds(v)}
        allowClear
      />
      <Space size={8} wrap style={{ marginTop: 8 }}>
        <Button
          type="primary"
          ghost
          size="small"
          icon={<PlusOutlined />}
          disabled={teamOptions.length === 0}
          onClick={() => setSelectedTeamIds(teamOptions.map((o) => o.value))}
        >
          {t('models.channelAuthSelectAll')}
        </Button>
        <Button size="small" disabled={!hasSelection} onClick={() => setSelectedTeamIds([])}>
          {t('models.channelAuthClear')}
        </Button>
      </Space>
    </Modal>
  )
}
