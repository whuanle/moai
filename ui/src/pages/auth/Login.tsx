import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button, Card, Form, Input, Space, Tooltip, Typography, theme as antdTheme } from 'antd'
import { LockOutlined, SafetyOutlined, UserOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useFeedback } from '@/design-system'
import { radius, spacing } from '@/design-system/theme'
import {
  getOAuthProviders,
  getServerInfo,
  login,
  sendLoginCode,
  verifyLogin,
  isLoginChallenge,
  type LoginChallenge,
  type OAuthProviderItem,
} from '@/api/auth'
import type { UserInfo } from '@/store/app'
import { AppLogo } from '@/layouts/components/AppLogo'
import { useLoginBackgroundSrc, useSystemName } from '@/layouts/useSystemLogo'
import { resolveStorageUrl } from '@/utils/storage'

interface LoginFormValues {
  username: string
  password: string
}

/** 登录流程步骤：credentials=账密 → totp/emailCode=二次验证 */
type LoginStep = 'credentials' | 'totp' | 'emailCode'

const RESEND_COOLDOWN_SECONDS = 60

export function Login() {
  const { t } = useTranslation()
  const feedback = useFeedback()
  const navigate = useNavigate()
  const { token } = antdTheme.useToken()
  const loginBackground = useLoginBackgroundSrc()
  const systemName = useSystemName()
  const [loading, setLoading] = useState(false)
  const [providers, setProviders] = useState<OAuthProviderItem[]>([])
  const [resetEnabled, setResetEnabled] = useState(false)
  const [step, setStep] = useState<LoginStep>('credentials')
  const [challenge, setChallenge] = useState<LoginChallenge | null>(null)
  const [secondFactorCode, setSecondFactorCode] = useState('')
  const [verifying, setVerifying] = useState(false)
  const [resending, setResending] = useState(false)
  const [resendCooldown, setResendCooldown] = useState(0)

  // 登录页在 AppLayout 之外，浏览器标签页标题需要自行同步网站名称
  useEffect(() => {
    document.title = systemName
  }, [systemName])

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const items = await getOAuthProviders(window.location.origin)
        if (!cancelled) setProviders(items)
      } catch {
        // 错误已由全局请求中间件统一提示
      }
      try {
        const info = await getServerInfo()
        if (!cancelled) setResetEnabled(info.passwordResetEnabled)
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  // 邮箱验证码重发倒计时
  useEffect(() => {
    if (resendCooldown <= 0) return undefined
    const timer = window.setInterval(() => {
      setResendCooldown((value) => (value <= 1 ? 0 : value - 1))
    }, 1000)
    return () => window.clearInterval(timer)
  }, [resendCooldown])

  const navigateAfterLogin = (userInfo: UserInfo | null) => {
    feedback.success(t('auth.loginSuccess'))
    if (userInfo?.mfaPending) {
      // 系统强制两步验证且未绑定：进入个人中心 2FA 绑定分区，绑定前无法使用系统
      navigate('/account/twofa?bind2fa=1', { replace: true })
      return
    }
    // 支持登录后回跳（仅本站相对路径，如 /sso/authorize?client=...），其余回应用广场
    const params = new URLSearchParams(window.location.search)
    const redirect = params.get('redirect')
    const safeRedirect = redirect && redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/apps'
    navigate(safeRedirect, { replace: true })
  }

  const handleFinish = async (values: LoginFormValues) => {
    setLoading(true)
    try {
      const result = await login(values.username, values.password)
      if (isLoginChallenge(result)) {
        setChallenge(result)
        setSecondFactorCode('')
        setStep(result.secondFactor)
        if (result.secondFactor === 'emailCode') {
          // 服务端在密码校验通过后已发送登录验证码
          setResendCooldown(RESEND_COOLDOWN_SECONDS)
          feedback.success(t('auth.secondFactor.emailCodeSent'))
        }
        return
      }
      navigateAfterLogin(result)
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      console.error('Login failed:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleVerify = async () => {
    if (!challenge || secondFactorCode.length !== 6) return
    setVerifying(true)
    try {
      const userInfo = await verifyLogin(challenge.loginTicket, secondFactorCode)
      navigateAfterLogin(userInfo)
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      console.error('Login verify failed:', error)
    } finally {
      setVerifying(false)
    }
  }

  const handleResend = async () => {
    if (!challenge || resendCooldown > 0) return
    setResending(true)
    try {
      await sendLoginCode(challenge.loginTicket)
      setResendCooldown(RESEND_COOLDOWN_SECONDS)
      feedback.success(t('auth.secondFactor.emailCodeSent'))
    } catch (error) {
      console.error('Resend login code failed:', error)
    } finally {
      setResending(false)
    }
  }

  const backToCredentials = () => {
    setStep('credentials')
    setChallenge(null)
    setSecondFactorCode('')
    setResendCooldown(0)
  }

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: spacing.lg,
        background: loginBackground
          ? `url("${loginBackground}") center/cover no-repeat`
          : `linear-gradient(180deg, ${token.colorBgLayout} 0%, ${token.colorPrimaryBg} 100%)`,
      }}
    >
      <Card
        style={{
          width: 400,
          borderRadius: radius.lg,
          boxShadow: token.boxShadowTertiary,
        }}
      >
        <div style={{ textAlign: 'center', marginBottom: spacing.lg }}>
          <AppLogo size={48} alt={systemName} style={{ marginBottom: spacing.sm }} />
          <Typography.Title level={3} style={{ marginBottom: 4 }}>
            {systemName}
          </Typography.Title>
          <Typography.Text type="secondary">
            {step === 'credentials' ? t('auth.loginSubtitle') : t('auth.secondFactor.subtitle')}
          </Typography.Text>
        </div>

        {step === 'credentials' && (
          <Form<LoginFormValues> layout="vertical" onFinish={handleFinish} autoComplete="off">
            <Form.Item
              name="username"
              label={t('auth.username')}
              rules={[{ required: true, message: t('auth.usernamePlaceholder') }]}
            >
              <Input prefix={<UserOutlined />} placeholder={t('auth.usernamePlaceholder')} size="large" />
            </Form.Item>
            <Form.Item
              name="password"
              label={t('auth.password')}
              rules={[{ required: true, message: t('auth.passwordPlaceholder') }]}
            >
              <Input.Password prefix={<LockOutlined />} placeholder={t('auth.passwordPlaceholder')} size="large" />
            </Form.Item>
            {resetEnabled && (
              <Typography.Text type="secondary" style={{ display: 'block', textAlign: 'right', marginTop: -12, marginBottom: 8 }}>
                <Link to="/forgot-password">{t('auth.forgot.title')}</Link>
              </Typography.Text>
            )}
            <Form.Item style={{ marginBottom: 12 }}>
              <Button type="primary" htmlType="submit" block size="large" loading={loading}>
                {t('auth.login')}
              </Button>
            </Form.Item>
          </Form>
        )}

        {step !== 'credentials' && (
          <div>
            <Typography.Paragraph type="secondary">
              {step === 'totp' ? t('auth.secondFactor.totpHint') : t('auth.secondFactor.emailCodeHint')}
            </Typography.Paragraph>
            <Space.Compact style={{ width: '100%', marginBottom: 12 }}>
              <Input
                prefix={<SafetyOutlined />}
                placeholder={t('auth.secondFactor.codePlaceholder')}
                size="large"
                maxLength={6}
                inputMode="numeric"
                value={secondFactorCode}
                autoFocus
                onChange={(e) => setSecondFactorCode(e.target.value.replace(/\D/g, ''))}
                onPressEnter={handleVerify}
              />
              {step === 'emailCode' && (
                <Button size="large" disabled={resendCooldown > 0} loading={resending} onClick={handleResend}>
                  {resendCooldown > 0
                    ? t('auth.secondFactor.resendIn', { seconds: resendCooldown })
                    : t('auth.secondFactor.resend')}
                </Button>
              )}
            </Space.Compact>
            <Button
              type="primary"
              block
              size="large"
              loading={verifying}
              disabled={secondFactorCode.length !== 6}
              onClick={handleVerify}
            >
              {t('auth.secondFactor.verify')}
            </Button>
            <Button type="link" block style={{ marginTop: 8 }} onClick={backToCredentials}>
              {t('auth.secondFactor.back')}
            </Button>
          </div>
        )}

        {step === 'credentials' && providers.length > 0 && (
          <>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                marginBottom: 16,
              }}
            >
              <div style={{ flex: 1, height: 1, background: token.colorBorderSecondary }} />
              <Typography.Text type="secondary" style={{ marginInline: 12, fontSize: 12 }}>
                {t('auth.oauthLogin')}
              </Typography.Text>
              <div style={{ flex: 1, height: 1, background: token.colorBorderSecondary }} />
            </div>
            <Space size="large" style={{ justifyContent: 'center', width: '100%', marginBottom: spacing.md }}>
              {providers.map((item) => (
                <Tooltip key={item.oAuthId} title={item.name} placement="top">
                  <Button
                    type="text"
                    shape="circle"
                    size="large"
                    onClick={() => {
                      if (item.redirectUrl) window.location.href = item.redirectUrl
                    }}
                  >
                    {item.iconUrl ? (
                      <img
                        src={resolveStorageUrl(item.iconUrl)}
                        alt={item.name ?? ''}
                        style={{ width: 28, height: 28 }}
                      />
                    ) : (
                      <span style={{ fontSize: 14 }}>{item.name}</span>
                    )}
                  </Button>
                </Tooltip>
              ))}
            </Space>
          </>
        )}

        {step === 'credentials' && (
          <Typography.Text type="secondary" style={{ display: 'block', textAlign: 'center' }}>
            {t('auth.noAccount')} <Link to="/register">{t('auth.goRegister')}</Link>
          </Typography.Text>
        )}
      </Card>
    </div>
  )
}
