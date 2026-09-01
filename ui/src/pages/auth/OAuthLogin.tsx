import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { Button, Card, Spin, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { resolveErrorMessage, useFeedback } from '@/design-system'
import { applyLoginResponse, oauthLogin, oauthRegister } from '@/api/auth'
import { oauthBindByCode } from '@/api/account'
import { notifyOAuthBindResult, parseOAuthState } from '@/utils/oauth'

interface PendingBind {
  tempOAuthBindId: string
  name?: string | null
}

export function OAuthLogin() {
  const { t } = useTranslation()
  const feedback = useFeedback()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [loading, setLoading] = useState(true)
  const [registering, setRegistering] = useState(false)
  const [pendingBind, setPendingBind] = useState<PendingBind | null>(null)
  const started = useRef(false)

  const code = searchParams.get('code')
  const rawState = searchParams.get('state')
  const { oAuthId, bind: isBindIntent } = parseOAuthState(rawState)

  // 绑定弹窗模式：由账号设置页通过 window.open 打开，state 已附加 `:bind`。
  // 此时通过独立的绑定接口直接绑定当前登录账号，成功后通知主页面并关闭自身。
  const isPopup = window.opener != null && window.opener.origin === window.location.origin
  const bindMode = isPopup && isBindIntent

  useEffect(() => {
    // 授权 code 是一次性的，防止 StrictMode 下重复触发
    if (started.current) return
    started.current = true

    const handleLogin = async () => {
      // 绑定弹窗模式：调用独立绑定接口，绑定当前登录账号，完成后关闭弹窗
      if (bindMode) {
        if (!code || !oAuthId) {
          notifyOAuthBindResult({ type: 'oauth_bind_cancel', oAuthId })
          window.close()
          return
        }
        try {
          await oauthBindByCode({ code, oAuthId })
          notifyOAuthBindResult({ type: 'oauth_bind_success', oAuthId })
        } catch (error) {
          // 错误由全局请求中间件在弹窗内提示，但弹窗随即关闭，此处把后端返回的错误信息传给主界面展示。
          notifyOAuthBindResult({
            type: 'oauth_bind_error',
            oAuthId,
            message: resolveErrorMessage(error) ?? t('account.bindError'),
          })
        } finally {
          window.close()
        }
        return
      }

      // 正常登录流程（顶层页面）
      if (!code || !oAuthId) {
        navigate('/login', { replace: true })
        return
      }
      try {
        const res = await oauthLogin({ code, oAuthId })
        if (res?.isBindUser && res.loginCommandResponse) {
          applyLoginResponse(res.loginCommandResponse)
          feedback.success(t('auth.loginSuccess'))
          navigate('/dashboard', { replace: true })
          return
        }
        if (res?.tempOAuthBindId) {
          // 未绑定过，提示用户确认注册
          setPendingBind({ tempOAuthBindId: res.tempOAuthBindId, name: res.name })
        } else {
          navigate('/login', { replace: true })
        }
      } catch {
        // 错误已由全局请求中间件统一提示
        navigate('/login', { replace: true })
      } finally {
        setLoading(false)
      }
    }

    void handleLogin()
  }, [code, oAuthId, bindMode, navigate, feedback, t])

  const handleRegister = async () => {
    if (!pendingBind) return
    setRegistering(true)
    try {
      await oauthRegister(pendingBind.tempOAuthBindId)
      feedback.success(t('auth.loginSuccess'))
      navigate('/dashboard', { replace: true })
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setRegistering(false)
    }
  }

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Card style={{ width: 400, textAlign: 'center' }}>
        {loading ? (
          <>
            <Spin size="large" />
            <Typography.Paragraph style={{ marginTop: 16, marginBottom: 0 }}>
              {t('auth.oauthCallback')}
            </Typography.Paragraph>
          </>
        ) : pendingBind ? (
          <>
            <Typography.Title level={4}>{t('auth.oauthBindTitle')}</Typography.Title>
            <Typography.Paragraph type="secondary">
              {t('auth.oauthBindDesc', { name: pendingBind.name ?? '' })}
            </Typography.Paragraph>
            <Button type="primary" block size="large" loading={registering} onClick={handleRegister}>
              {t('auth.oauthBindConfirm')}
            </Button>
          </>
        ) : null}
      </Card>
    </div>
  )
}
