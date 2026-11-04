import { useCallback, useEffect, useState } from 'react'
import { Button, Input, Space, Switch, Tag, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getServerInfo } from '@/api/auth'
import { getAppFeature, updateAppFeature, type AppFeature } from '@/api/access-app'
import { ResourceKeyCard } from './ResourceKeyCard'

const { Text } = Typography

/** ACP 客户端可调用的 JSON-RPC 方法（session/prompt 为 SSE 流式） */
const ACP_METHODS = ['initialize', 'session/new', 'session/load', 'session/prompt', 'session/cancel']

interface AppAcpSectionProps {
  appId: string
  /** 归属团队 id */
  teamId: number
  /** 团队 Admin+ 才能开启 ACP 与管理资源 key */
  canManage: boolean
}

/**
 * 应用「ACP」页：开启 ACP 接入（开启时后端自动生成/复用该应用唯一的团队资源 key，与 A2A 共用一把），
 * 展示该应用的 ACP 端点地址（POST JSON-RPC，agent-to-agent）与资源 key，供外部 agent 复制接入。
 * 地址取 serverinfo 的 serviceUrl（对外完整 URL），未取到时回退当前页面 origin。
 */
export function AppAcpSection({ appId, canManage }: AppAcpSectionProps) {
  const { t } = useTranslation()
  const [address, setAddress] = useState('')
  const [feature, setFeature] = useState<AppFeature | null>(null)

  const load = useCallback(async () => {
    if (!canManage) return
    try {
      setFeature(await getAppFeature(appId))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [appId, canManage])

  useEffect(() => {
    if (canManage) void load()
  }, [canManage, load])

  useEffect(() => {
    let cancelled = false
    const fallback = `${window.location.origin.replace(/\/$/, '')}/api/teamres/app/${appId}/acp`
    getServerInfo()
      .then((info) => {
        if (cancelled) return
        const base = info?.serviceUrl?.trim()?.replace(/\/$/, '')
        setAddress(base ? `${base}/api/teamres/app/${appId}/acp` : fallback)
      })
      .catch(() => {
        if (cancelled) return
        setAddress(fallback)
      })
    return () => {
      cancelled = true
    }
  }, [appId])

  const handleToggle = async (checked: boolean) => {
    try {
      const next = await updateAppFeature(appId, { isAcpEnabled: checked, isA2aEnabled: feature?.isA2aEnabled ?? false })
      setFeature(next)
      feedback.success(checked ? t('appWorkspace.acpEnabled') : t('appWorkspace.acpDisabled'))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyAddress = async () => {
    try {
      await navigator.clipboard.writeText(address)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  return (
    <DSCard>
      <Space direction="vertical" size={spacing.md} style={{ display: 'flex' }}>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.acpEnable')}</Text>
          <Switch checked={feature?.isAcpEnabled ?? false} onChange={(v) => void handleToggle(v)} style={{ marginLeft: spacing.sm }} />
        </div>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.acpAddress')}</Text>
          <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
            <Input readOnly value={address} placeholder={t('appWorkspace.acpAddressPlaceholder')} />
            <Button icon={<CopyOutlined />} disabled={!address} onClick={() => void copyAddress()} aria-label={t('common.copy')} />
          </Space.Compact>
        </div>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.acpMethods')}</Text>
          <div style={{ marginTop: spacing.xs }}>
            {ACP_METHODS.map((name) => (
              <Tag key={name} style={{ fontFamily: 'monospace' }}>{name}</Tag>
            ))}
          </div>
        </div>
        <ResourceKeyCard
          featureEnabled={feature?.isAcpEnabled ?? false}
          canManage={canManage}
          keyInfo={feature ? { teamResourceKeyId: feature.teamResourceKeyId, key: feature.key, keyPrefix: feature.keyPrefix, status: feature.status } : null}
          onChanged={() => void load()}
          hintKey="teamresKey.hintApp"
        />
        <Text type="secondary" style={{ fontSize: 12 }}>{t('appWorkspace.acpAuthHint')}</Text>
      </Space>
    </DSCard>
  )
}
