import { useState } from 'react'
import { Button, Popconfirm, Space, Switch, Typography } from 'antd'
import { CopyOutlined, DeleteOutlined, EyeInvisibleOutlined, EyeOutlined, KeyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  deleteTeamResourceKey,
  resetTeamResourceKey,
  updateTeamResourceKeyStatus,
} from '@/api/teamres-key'

const { Text } = Typography

/** 资源 key 展示信息（由父级功能配置接口取回：开启功能时后端自动生成/复用） */
export interface ResourceKeyInfo {
  teamResourceKeyId?: string | null
  key?: string | null
  keyPrefix?: string | null
  status?: 'enabled' | 'disabled' | null
}

interface ResourceKeyCardProps {
  /** 资源对应对外功能是否已开启（未开启不生成 key，仅展示提示） */
  featureEnabled: boolean
  /** 团队 Admin+ 才能管理 key */
  canManage: boolean
  /** 当前 key 信息（null=尚未生成） */
  keyInfo: ResourceKeyInfo | null
  /** key 变化（重置/启停/删除）后由父组件重新拉取配置 */
  onChanged: () => void
  /** 提示文案 i18n key（说明该 key 能访问哪些端点） */
  hintKey: string
}

/**
 * 团队资源 key 卡片：一个资源一把 key（应用 ACP/A2A 共用其应用的 key、知识库/知识图谱各一把）。
 * key 由资源开启对外功能时后端自动生成/复用，此处仅展示（掩码/复制）、重置、启停与删除。
 */
export function ResourceKeyCard({ featureEnabled, canManage, keyInfo, onChanged, hintKey }: ResourceKeyCardProps) {
  const { t } = useTranslation()
  const [revealed, setRevealed] = useState(false)
  const [busy, setBusy] = useState(false)

  const handleReset = async () => {
    if (!keyInfo?.teamResourceKeyId) return
    setBusy(true)
    try {
      await resetTeamResourceKey(String(keyInfo.teamResourceKeyId))
      setRevealed(true)
      onChanged()
      feedback.success(t('teamresKey.resetSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBusy(false)
    }
  }

  const handleToggle = async (checked: boolean) => {
    if (!keyInfo?.teamResourceKeyId) return
    try {
      await updateTeamResourceKeyStatus(String(keyInfo.teamResourceKeyId), checked ? 'enabled' : 'disabled')
      onChanged()
      feedback.success(checked ? t('teamresKey.enableSuccess') : t('teamresKey.disableSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async () => {
    if (!keyInfo?.teamResourceKeyId) return
    try {
      await deleteTeamResourceKey(String(keyInfo.teamResourceKeyId))
      onChanged()
      feedback.success(t('teamresKey.deleteSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyKey = async () => {
    if (!keyInfo?.key) return
    try {
      await navigator.clipboard.writeText(keyInfo.key)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  if (!canManage) {
    return <Text type="secondary" style={{ fontSize: 12 }}>{t('teamresKey.adminOnly')}</Text>
  }

  if (!featureEnabled) {
    return (
      <Text type="secondary" style={{ fontSize: 12 }}>{t('teamresKey.featureOffHint')}</Text>
    )
  }

  if (!keyInfo?.key) {
    return (
      <Space direction="vertical" size={spacing.sm} style={{ display: 'flex' }}>
        <Text type="secondary" style={{ fontSize: 12 }}>{t('teamresKey.generatingHint')}</Text>
        <Text type="secondary" style={{ fontSize: 12 }}>{t(hintKey)}</Text>
      </Space>
    )
  }

  const masked = keyItemMask(keyInfo)
  const enabled = keyInfo.status !== 'disabled'

  return (
    <Space direction="vertical" size={spacing.sm} style={{ display: 'flex' }}>
      <Space size={0} wrap>
        <Text code style={{ cursor: 'pointer' }} title={revealed ? t('teamresKey.hideKey') : t('teamresKey.revealKey')} onClick={() => setRevealed((v) => !v)}>
          {revealed ? keyInfo.key : masked}
        </Text>
        <Button
          type="text"
          size="small"
          icon={revealed ? <EyeInvisibleOutlined /> : <EyeOutlined />}
          aria-label={revealed ? t('teamresKey.hideKey') : t('teamresKey.revealKey')}
          onClick={() => setRevealed((v) => !v)}
        />
        <Button type="text" size="small" icon={<CopyOutlined />} aria-label={t('common.copy')} onClick={() => void copyKey()} />
        <Switch checked={enabled} onChange={(v) => void handleToggle(v)} size="small" />
        <Popconfirm title={t('teamresKey.resetConfirm')} onConfirm={() => void handleReset()}>
          <Button type="link" size="small" icon={<KeyOutlined />} disabled={busy}>
            {t('teamresKey.reset')}
          </Button>
        </Popconfirm>
        <Popconfirm title={t('teamresKey.deleteConfirm')} onConfirm={() => void handleDelete()}>
          <Button type="link" size="small" danger icon={<DeleteOutlined />}>
            {t('teamresKey.delete')}
          </Button>
        </Popconfirm>
      </Space>
      <Text type="secondary" style={{ fontSize: 12 }}>{t(hintKey)}</Text>
    </Space>
  )
}

/** key 默认掩码：前缀 + ****（前缀为 moai-tr- + 4 位随机） */
function keyItemMask(keyInfo: ResourceKeyInfo): string {
  if (!keyInfo.key) return '****'
  return keyInfo.key.length <= 12 ? keyInfo.key : `${keyInfo.keyPrefix ?? keyInfo.key.slice(0, 12)}****`
}
