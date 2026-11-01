import { useEffect, useState } from 'react'
import { Avatar, Button, Card, Popconfirm, Typography, theme } from 'antd'
import { LinkOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Page, feedback } from '@/design-system'
import { resolveStorageUrl } from '@/utils/storage'
import { getOAuthProviders, type OAuthProviderItem } from '@/api/auth'
import { getBoundAccounts, unbindProvider, type AccountBoundItem } from '@/api/account'
import { toBindAuthorizeUrl } from '@/utils/oauth'

const { Text } = Typography

/** 第三方账号：OAuth 连接器绑定/解绑（弹窗授权流） */
export function AccountOauth() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [loading, setLoading] = useState(true)
  const [providers, setProviders] = useState<OAuthProviderItem[]>([])
  const [boundAccounts, setBoundAccounts] = useState<AccountBoundItem[]>([])

  const load = async () => {
    setLoading(true)
    try {
      const [providerItems, bound] = await Promise.all([getOAuthProviders(), getBoundAccounts()])
      setProviders(providerItems)
      setBoundAccounts(bound)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  // 监听绑定弹窗的回调结果：成功后刷新已绑定列表。
  useEffect(() => {
    const onMessage = (e: MessageEvent) => {
      if (e.origin !== window.location.origin) return
      const data = e.data as { type?: string } | null
      if (data?.type === 'oauth_bind_success') {
        feedback.success(t('account.bindSuccess'))
        void load()
      } else if (data?.type === 'oauth_bind_error') {
        const message = (data as { message?: string | null }).message
        feedback.error(message ?? t('account.bindError'))
      }
    }
    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [t])

  const handleUnbind = async (oAuthId: string) => {
    try {
      await unbindProvider(oAuthId)
      feedback.success(t('account.unbindSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const isBound = (oAuthId?: string | null) => boundAccounts.some((b) => b.oAuthId === oAuthId)

  const handleBind = (provider: OAuthProviderItem) => {
    const bindUrl = toBindAuthorizeUrl(provider.redirectUrl)
    if (!bindUrl) return
    window.open(bindUrl, 'oauth_bind', 'width=600,height=750,menubar=no,toolbar=no,location=no,status=no,scrollbars=no')
  }

  const providerList = (providers ?? []).map((provider) => {
    const bound = isBound(provider.oAuthId)
    return { provider, bound }
  })

  return (
    <Page>
      <div style={{ maxWidth: 720, margin: '0 auto', display: 'flex', flexDirection: 'column', gap: 24 }}>
        <Card title={t('account.oauthTitle')} loading={loading}>
          {providerList.length === 0 ? (
            <Text type="secondary">{t('account.oauthEmpty')}</Text>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {providerList.map(({ provider, bound }) => (
                <div
                  key={provider.oAuthId}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 12,
                    padding: '12px 16px',
                    border: `1px solid ${token.colorBorderSecondary}`,
                    borderRadius: 8,
                  }}
                >
                  {provider.iconUrl ? (
                    <img
                      src={resolveStorageUrl(provider.iconUrl)}
                      alt={provider.name ?? ''}
                      style={{ width: 28, height: 28, objectFit: 'contain' }}
                    />
                  ) : (
                    <Avatar size={28} icon={<LinkOutlined />} />
                  )}
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <Text strong style={{ display: 'block' }}>
                      {provider.name ?? provider.provider}
                    </Text>
                    <Text type="secondary" style={{ fontSize: 12 }}>
                      {bound ? t('account.oauthBound') : t('account.oauthNotBound')}
                    </Text>
                  </div>
                  {bound ? (
                    <Popconfirm
                      title={t('account.unbindConfirm')}
                      onConfirm={() => provider.oAuthId && handleUnbind(provider.oAuthId)}
                    >
                      <Button danger>{t('account.unbind')}</Button>
                    </Popconfirm>
                  ) : (
                    <Button
                      type="primary"
                      icon={<LinkOutlined />}
                      onClick={() => handleBind(provider)}
                    >
                      {t('account.bind')}
                    </Button>
                  )}
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
    </Page>
  )
}
