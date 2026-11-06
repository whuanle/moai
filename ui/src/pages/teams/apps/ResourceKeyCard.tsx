import { useState } from 'react'
import { Button, Popconfirm, Space, Typography, theme } from 'antd'
import { CopyOutlined, EyeInvisibleOutlined, EyeOutlined, KeyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { resetTeamResourceKey } from '@/api/teamres-key'

const { Text } = Typography

/** 资源 key 展示信息（由父级功能配置接口取回：开启功能时后端自动生成/复用；key 无启停状态、不可删除，吊销=重置或关闭功能开关） */
export interface ResourceKeyInfo {
  teamResourceKeyId?: string | null
  key?: string | null
  keyPrefix?: string | null
}

interface ResourceKeyCardProps {
  /** 资源对应对外功能是否已开启（未开启不生成 key，仅展示提示） */
  featureEnabled: boolean
  /** 团队 Admin+ 才能管理 key */
  canManage: boolean
  /** 当前 key 信息（null=尚未生成；父页会在功能开启后自动补齐） */
  keyInfo: ResourceKeyInfo | null
  /** key 变化（补齐/重置）后由父组件重新拉取配置 */
  onChanged: () => void
  /** 提示文案 i18n key（可选；不传不渲染说明行） */
  hintKey?: string
  /** 盒子标题 i18n key（可选；不传则无标题行） */
  titleKey?: string
}

/**
 * 团队资源 key 盒子：一个资源一把 key（应用 ACP/A2A 共用其应用的 key、知识库/知识图谱各一把），
 * key 之间不可混用（绑定资源实例，越资源访问被拒绝）。
 * key 由资源开启对外功能时后端自动生成/复用，此处仅展示（掩码/复制）与重置；不支持删除。
 */
export function ResourceKeyCard({ featureEnabled, canManage, keyInfo, onChanged, hintKey, titleKey }: ResourceKeyCardProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
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

  const copyKey = async () => {
    if (!keyInfo?.key) return
    try {
      await navigator.clipboard.writeText(keyInfo.key)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  const hasTitle = Boolean(titleKey)

  return (
    <div
      style={{
        background: token.colorFillQuaternary,
        border: `1px solid ${token.colorBorderSecondary}`,
        borderRadius: token.borderRadiusLG,
        padding: `${spacing.md}px ${spacing.lg}px`,
        display: 'flex',
        flexDirection: 'column',
        gap: spacing.sm,
      }}
    >
      {hasTitle && (
        <Space size={spacing.sm}>
          <KeyOutlined style={{ color: token.colorPrimary }} />
          <Text strong style={{ fontSize: 13 }}>{t(titleKey!)}</Text>
        </Space>
      )}

      {!canManage ? (
        <Text type="secondary" style={{ fontSize: 12 }}>{t('teamresKey.adminOnly')}</Text>
      ) : !featureEnabled ? (
        <Text type="secondary" style={{ fontSize: 12 }}>{t('teamresKey.featureOffHint')}</Text>
      ) : !keyInfo?.key ? (
        // 功能开启后 key 由父页自动补齐，此为瞬态
        <Text type="secondary" style={{ fontSize: 12 }}>{t('teamresKey.fetchingHint')}</Text>
      ) : (
        <Space size={0} wrap align="center">
          <Text code style={{ cursor: 'pointer', fontSize: 13 }} title={revealed ? t('teamresKey.hideKey') : t('teamresKey.revealKey')} onClick={() => setRevealed((v) => !v)}>
            {revealed ? keyInfo.key : keyMask(keyInfo)}
          </Text>
          <Button
            type="text"
            size="small"
            icon={revealed ? <EyeInvisibleOutlined /> : <EyeOutlined />}
            aria-label={revealed ? t('teamresKey.hideKey') : t('teamresKey.revealKey')}
            onClick={() => setRevealed((v) => !v)}
          />
          <Button type="text" size="small" icon={<CopyOutlined />} aria-label={t('common.copy')} onClick={() => void copyKey()} />
          <Popconfirm title={t('teamresKey.resetConfirm')} onConfirm={() => void handleReset()}>
            <Button type="link" size="small" icon={<KeyOutlined />} disabled={busy}>
              {t('teamresKey.reset')}
            </Button>
          </Popconfirm>
        </Space>
      )}

      {hintKey && <Text type="secondary" style={{ fontSize: 12 }}>{t(hintKey)}</Text>}
    </div>
  )
}

/** key 默认掩码：前缀 + ****（前缀为 moai-tr- + 4 位随机） */
function keyMask(keyInfo: ResourceKeyInfo): string {
  if (!keyInfo.key) return '****'
  return keyInfo.key.length <= 12 ? keyInfo.key : `${keyInfo.keyPrefix ?? keyInfo.key.slice(0, 12)}****`
}
