import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { Alert, Button, Card, Space, Spin, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { radius, spacing } from '@/design-system/theme'
import { subsystemSsoAuthorizeCode } from '@/api/subsystem'
import { useAppStore } from '@/store/app'

const { Text, Title } = Typography

/**
 * 组装回跳地址：code 与可选 state 追加到子系统注册的回调 URL 上.
 */
export function buildCallbackRedirect(callbackUrl: string, code: string, state?: string): string {
  const callback = new URL(callbackUrl)
  callback.searchParams.set('code', code)
  if (state) callback.searchParams.set('state', state)
  return callback.toString()
}

type SsoState =
  | { phase: 'loading' }
  | { phase: 'redirecting'; name?: string | null }
  | { phase: 'error'; message: string }

/**
 * 子系统委托 SSO 跳转页（公开路由）：子系统以 `/sso/authorize?client=<appid>&state=<可选>` 引导用户来此，
 * MoAI 登录态下自动签发一次性委托码并回跳子系统注册的回调地址（回跳地址取注册值，防开放重定向）。
 */
export function SsoAuthorize() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const accessToken = useAppStore((state) => state.userInfo?.accessToken)
  const [state, setState] = useState<SsoState>({ phase: 'loading' })
  const startedRef = useRef(false)

  const client = searchParams.get('client')?.trim() ?? ''
  const passState = searchParams.get('state') ?? ''

  const redirect = useCallback(
    async (subsystemKey: string, passthrough: string) => {
      setState({ phase: 'loading' })
      try {
        const res = await subsystemSsoAuthorizeCode(subsystemKey, passthrough || undefined)
        if (!res.code || !res.callbackUrl) {
          setState({ phase: 'error', message: t('sso.invalidResponse') })
          return
        }

        setState({ phase: 'redirecting', name: res.name })
        window.location.replace(buildCallbackRedirect(res.callbackUrl, res.code, passthrough || undefined))
      } catch {
        // 错误已由全局请求中间件统一提示（子系统不存在/未开放/团队未授权等）
        setState({ phase: 'error', message: t('sso.authorizeFailed') })
      }
    },
    [t],
  )

  useEffect(() => {
    if (startedRef.current) return
    startedRef.current = true
    if (!client) {
      setState({ phase: 'error', message: t('sso.missingClient') })
      return
    }

    if (!accessToken) {
      // 未登录：去登录页，登录成功后带着原地址回来
      const current = `${window.location.pathname}${window.location.search}`
      navigate(`/login?redirect=${encodeURIComponent(current)}`, { replace: true })
      return
    }

    void redirect(client, passState)
  }, [accessToken, client, passState, navigate, redirect, t])

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: spacing.lg,
      }}
    >
      <Card style={{ width: 420, borderRadius: radius.lg }}>
        <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
          <Title level={4} style={{ marginBottom: 0 }}>
            {t('sso.title')}
          </Title>
          {state.phase === 'error' ? (
            <>
              <Alert type="error" showIcon message={t('sso.errorTitle')} description={state.message} />
              <Button type="primary" block onClick={() => navigate('/apps', { replace: true })}>
                {t('sso.backToApps')}
              </Button>
            </>
          ) : (
            <Space>
              <Spin />
              <Text>
                {state.phase === 'redirecting'
                  ? t('sso.redirecting', { name: state.name ?? client })
                  : t('sso.preparing', { name: client })}
              </Text>
            </Space>
          )}
        </Space>
      </Card>
    </div>
  )
}
