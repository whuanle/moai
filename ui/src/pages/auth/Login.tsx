import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button, Card, Form, Input, Space, Tooltip, Typography, theme as antdTheme } from 'antd'
import { LockOutlined, SafetyCertificateOutlined, UserOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useFeedback } from '@/design-system'
import { radius, spacing } from '@/design-system/theme'
import {
  fetchChallengePolicy,
  getOAuthProviders,
  getServerInfo,
  login,
  sendLoginCode,
  verifyLogin,
  isLoginChallenge,
  type ChallengePolicy,
  type LoginChallenge,
  type OAuthProviderItem,
} from '@/api/auth'
import type { UserInfo } from '@/store/app'
import { AppLogo } from '@/layouts/components/AppLogo'
import { useLoginBackgroundSrc, useSystemName } from '@/layouts/useSystemLogo'
import { resolveStorageUrl } from '@/utils/storage'
import { CaptchaField } from './components/CaptchaField'
import { useCaptcha } from './components/useCaptcha'

interface LoginFormValues {
  username: string
  password: string
  captchaCode?: string
  emailCode?: string
}

/** 登录流程步骤：credentials=单表单一体化提交 → totp=认证器动态码内联验证 */
type LoginStep = 'credentials' | 'totp'

const RESEND_COOLDOWN_SECONDS = 60

export function Login() {
  const { t } = useTranslation()
  const feedback = useFeedback()
  const navigate = useNavigate()
  const { token } = antdTheme.useToken()
  const loginBackground = useLoginBackgroundSrc()
  const systemName = useSystemName()
  const [form] = Form.useForm<LoginFormValues>()
  const [loading, setLoading] = useState(false)
  const [providers, setProviders] = useState<OAuthProviderItem[]>([])
  const [resetEnabled, setResetEnabled] = useState(false)
  const [step, setStep] = useState<LoginStep>('credentials')
  const [challenge, setChallenge] = useState<LoginChallenge | null>(null)
  const [policy, setPolicy] = useState<ChallengePolicy>({ captchaRequired: false, emailCodeRequired: false })
  const [totpCode, setTotpCode] = useState('')
  const [verifying, setVerifying] = useState(false)
  const [sending, setSending] = useState(false)
  const [resendCooldown, setResendCooldown] = useState(0)
  const { captcha, refreshCaptcha } = useCaptcha()

  // 登录页在 AppLayout 之外，浏览器标签页标题需要自行同步网站名称
  useEffect(() => {
    document.title = systemName
  }, [systemName])

  const refreshPolicy = useCallback(async (userName?: string) => {
    try {
      setPolicy(await fetchChallengePolicy('login', userName?.trim() || undefined))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [])

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

  // 挂载时拉取验证策略（图形验证码/邮箱验证码是否需要显示）
  useEffect(() => {
    void refreshPolicy()
  }, [refreshPolicy])

  // 图形验证码按需拉取：策略命中且还没有可用图片时
  useEffect(() => {
    if (policy.captchaRequired && !captcha) void refreshCaptcha()
  }, [policy.captchaRequired, captcha, refreshCaptcha])

  // 发送冷却倒计时（秒）
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

  const getCaptchaFields = () => {
    if (!policy.captchaRequired) return undefined
    return {
      captchaId: captcha?.captchaId,
      captchaCode: (form.getFieldValue('captchaCode') as string | undefined) || undefined,
    }
  }

  const handleFinish = async (values: LoginFormValues) => {
    setLoading(true)
    try {
      const result = await login(values.username, values.password, {
        emailCode: values.emailCode || undefined,
        ...getCaptchaFields(),
      })
      if (isLoginChallenge(result)) {
        // 账号绑定了认证器（TOTP）：内联显示动态码输入，凭票据二次验证
        setChallenge(result)
        setTotpCode('')
        setStep('totp')
        return
      }
      navigateAfterLogin(result)
    } catch (error) {
      // 失败后策略可能升级（首次失败后出现图形验证码、连败后出现邮箱验证码），
      // 重新拉取策略并刷新图形验证码（旧码已被消费或作废）
      void refreshPolicy(values.username)
      void refreshCaptcha()
      console.error('Login failed:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleTotpVerify = async () => {
    if (!challenge || totpCode.length !== 6) return
    setVerifying(true)
    try {
      const userInfo = await verifyLogin(challenge.loginTicket, totpCode)
      navigateAfterLogin(userInfo)
    } catch (error) {
      // 错误已由全局请求中间件统一提示
      console.error('Login verify failed:', error)
    } finally {
      setVerifying(false)
    }
  }

  const handleSendEmailCode = async () => {
    // 发码按用户名定位账号邮箱，账号先过表单校验
    try {
      await form.validateFields(['username'])
    } catch {
      return
    }
    const username = (form.getFieldValue('username') as string | undefined)?.trim()
    if (!username) return
    setSending(true)
    try {
      await sendLoginCode(username, getCaptchaFields())
      feedback.success(t('auth.loginCodeSent'))
      setResendCooldown(RESEND_COOLDOWN_SECONDS)
      // 按已输入的账号刷新策略（未绑定邮箱的账号会隐藏邮箱验证码框）
      void refreshPolicy(username)
    } catch (error) {
      // 图形验证码错误时旧图已失效，换一张
      if (policy.captchaRequired) void refreshCaptcha()
      console.error('Send login code failed:', error)
    } finally {
      setSending(false)
    }
  }

  const backToCredentials = () => {
    setStep('credentials')
    setChallenge(null)
    setTotpCode('')
    void refreshPolicy((form.getFieldValue('username') as string | undefined)?.trim())
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
          <Form<LoginFormValues> form={form} layout="vertical" onFinish={handleFinish} autoComplete="off">
            <Form.Item
              name="username"
              label={t('auth.username')}
              rules={[{ required: true, message: t('auth.usernamePlaceholder') }]}
            >
              <Input
                prefix={<UserOutlined />}
                placeholder={t('auth.usernamePlaceholder')}
                size="large"
                onBlur={(e) => {
                  const value = e.target.value?.trim()
                  if (value) void refreshPolicy(value)
                }}
              />
            </Form.Item>
            <Form.Item
              name="password"
              label={t('auth.password')}
              rules={[{ required: true, message: t('auth.passwordPlaceholder') }]}
            >
              <Input.Password prefix={<LockOutlined />} placeholder={t('auth.passwordPlaceholder')} size="large" />
            </Form.Item>
            {policy.captchaRequired && (
              <Form.Item
                name="captchaCode"
                label={t('auth.captcha.label')}
                rules={[{ required: true, message: t('auth.captcha.required') }]}
              >
                <CaptchaField captcha={captcha} onRefresh={() => void refreshCaptcha()} />
              </Form.Item>
            )}
            {policy.emailCodeRequired && (
              <Form.Item label={t('auth.emailCode')} required>
                <Space.Compact style={{ width: '100%' }}>
                  <Form.Item
                    name="emailCode"
                    noStyle
                    rules={[{ required: true, message: t('auth.emailCodeRequired') }]}
                  >
                    <Input
                      prefix={<SafetyCertificateOutlined />}
                      placeholder={t('auth.emailCodePlaceholder')}
                      size="large"
                      maxLength={6}
                      inputMode="numeric"
                      onChange={(e) => {
                        form.setFieldsValue({ emailCode: e.target.value.replace(/\D/g, '') })
                      }}
                    />
                  </Form.Item>
                  <Tooltip title={resendCooldown > 0 ? undefined : t('auth.sendCode')}>
                    <Button size="large" loading={sending} disabled={resendCooldown > 0} onClick={handleSendEmailCode}>
                      {resendCooldown > 0 ? `${resendCooldown}s` : t('auth.sendCode')}
                    </Button>
                  </Tooltip>
                </Space.Compact>
              </Form.Item>
            )}
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

        {step === 'totp' && (
          <div>
            <Typography.Paragraph type="secondary">{t('auth.secondFactor.totpHint')}</Typography.Paragraph>
            <Input
              placeholder={t('auth.secondFactor.codePlaceholder')}
              size="large"
              maxLength={6}
              inputMode="numeric"
              value={totpCode}
              autoFocus
              onChange={(e) => setTotpCode(e.target.value.replace(/\D/g, ''))}
              onPressEnter={handleTotpVerify}
            />
            <Button
              type="primary"
              block
              size="large"
              style={{ marginTop: 12 }}
              loading={verifying}
              disabled={totpCode.length !== 6}
              onClick={handleTotpVerify}
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
