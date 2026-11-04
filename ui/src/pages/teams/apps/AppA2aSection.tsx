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

/** A2A 客户端可调用的 JSON-RPC 方法（message/stream 为 SSE 流式；GET {地址}/agent.json 为 Agent Card） */
const A2A_METHODS = ['message/send', 'message/stream', 'tasks/get', 'tasks/cancel']

interface AppA2aSectionProps {
  appId: string
  /** 归属团队 id */
  teamId: number
  /** 团队 Admin+ 才能开启 A2A 与管理资源 key */
  canManage: boolean
}

/**
 * 应用「A2A」页：开启 A2A 接入（开启时后端自动生成/复用该应用唯一的团队资源 key，与 ACP 共用一把），
 * 展示该应用的 A2A 端点地址（Google Agent2Agent，POST JSON-RPC）与 Agent Card 地址、资源 key，供外部 agent 复制接入。
 * 地址取 serverinfo 的 serviceUrl（对外完整 URL），未取到时回退当前页面 origin。
 */
export function AppA2aSection({ appId, canManage }: AppA2aSectionProps) {
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
    const fallback = `${window.location.origin.replace(/\/$/, '')}/api/teamres/app/${appId}/a2a`
    getServerInfo()
      .then((info) => {
        if (cancelled) return
        const base = info?.serviceUrl?.trim()?.replace(/\/$/, '')
        setAddress(base ? `${base}/api/teamres/app/${appId}/a2a` : fallback)
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
      const next = await updateAppFeature(appId, { isAcpEnabled: feature?.isAcpEnabled ?? false, isA2aEnabled: checked })
      setFeature(next)
      feedback.success(checked ? t('appWorkspace.a2aEnabled') : t('appWorkspace.a2aDisabled'))
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
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.a2aEnable')}</Text>
          <Switch checked={feature?.isA2aEnabled ?? false} onChange={(v) => void handleToggle(v)} style={{ marginLeft: spacing.sm }} />
        </div>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.a2aAddress')}</Text>
          <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
            <Input readOnly value={address} placeholder={t('appWorkspace.a2aAddressPlaceholder')} />
            <Button icon={<CopyOutlined />} disabled={!address} onClick={() => void copyAddress()} aria-label={t('common.copy')} />
          </Space.Compact>
          <Text type="secondary" style={{ fontSize: 12 }}>{address ? `${address}/agent.json` : ''}</Text>
        </div>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.a2aMethods')}</Text>
          <div style={{ marginTop: spacing.xs }}>
            {A2A_METHODS.map((name) => (
              <Tag key={name} style={{ fontFamily: 'monospace' }}>{name}</Tag>
            ))}
          </div>
        </div>
        <ResourceKeyCard
          featureEnabled={feature?.isA2aEnabled ?? false}
          canManage={canManage}
          keyInfo={feature ? { teamResourceKeyId: feature.teamResourceKeyId, key: feature.key, keyPrefix: feature.keyPrefix, status: feature.status } : null}
          onChanged={() => void load()}
          hintKey="teamresKey.hintApp"
        />
        <Text type="secondary" style={{ fontSize: 12 }}>{t('appWorkspace.a2aAuthHint')}</Text>
      </Space>
    </DSCard>
  )
}
