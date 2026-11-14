import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router'
import { Alert, Button, Input, Modal, Space, Spin, Tooltip, Typography, theme } from 'antd'
import { SafetyCertificateOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import QRCode from 'qrcode'
import { Card, feedback } from '@/design-system'
import {
  disableTwoFactor,
  enableTwoFactor,
  getTwoFactorStatus,
  refreshUserProfile,
  setupTwoFactor,
  type TwoFactorSetup,
  type TwoFactorStatus,
} from '@/api/auth'
import { useAppStore } from '@/store/app'

const { Text } = Typography

/** 绑定流程步骤标题：数字圆点 + 标题 */
function StepHeader({ index, title }: { index: number; title: string }) {
  const { token } = theme.useToken()
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: 20,
          height: 20,
          borderRadius: '50%',
          background: token.colorPrimary,
          color: token.colorTextLightSolid,
          fontSize: 12,
          fontWeight: 600,
          flexShrink: 0,
        }}
      >
        {index}
      </span>
      <Text strong>{title}</Text>
    </div>
  )
}

/**
 * 两步验证（2FA/TOTP）管理面板：绑定（扫码/手输密钥 + 动态码激活）与关闭。
 * 独立组件，供个人设置页与后续个人中心 2FA 菜单复用.
 */
export function TwoFactorPanel() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { token } = theme.useToken()

  const [loading, setLoading] = useState(true)
  const [status, setStatus] = useState<TwoFactorStatus | null>(null)

  const [setup, setSetup] = useState<TwoFactorSetup | null>(null)
  const [setupQr, setSetupQr] = useState('')
  const [bindCode, setBindCode] = useState('')
  const [binding, setBinding] = useState(false)

  const [disableOpen, setDisableOpen] = useState(false)
  const [disableCode, setDisableCode] = useState('')
  const [disabling, setDisabling] = useState(false)

  const loadStatus = useCallback(async () => {
    setLoading(true)
    try {
      const result = await getTwoFactorStatus()
      setStatus(result)
      if (!result.enabled) return result
      setSetup(null)
      setBindCode('')
      return result
    } catch {
      // 错误已由全局请求中间件统一提示
      return null
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadStatus()
  }, [loadStatus])

  const handleStartBind = async () => {
    setLoading(true)
    try {
      const result = await setupTwoFactor()
      setSetup(result)
      setBindCode('')
      setSetupQr(await QRCode.toDataURL(result.otpauthUri, { width: 200, margin: 1 }))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }

  const handleConfirmBind = async () => {
    if (!setup || bindCode.length !== 6) return
    setBinding(true)
    try {
      await enableTwoFactor(bindCode)
      feedback.success(t('account.twofa.bindSuccess'))
      setSetup(null)
      setBindCode('')
      await loadStatus()
      await refreshUserProfile()
      // 强制绑定流程（登录后被拦截到本页）绑定完成即放行，直接进入应用
      if (useAppStore.getState().userInfo?.mfaPending) {
        navigate('/apps', { replace: true })
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBinding(false)
    }
  }

  const handleDisable = async () => {
    if (disableCode.length !== 6) return
    setDisabling(true)
    try {
      await disableTwoFactor(disableCode)
      feedback.success(t('account.twofa.disableSuccess'))
      setDisableOpen(false)
      setDisableCode('')
      await loadStatus()
      await refreshUserProfile()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setDisabling(false)
    }
  }

  return (
    <Card title={t('account.twofa.title')}>
      {loading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : (
        <>
          {status?.forced && (
            <Alert
              style={{ marginBottom: 16 }}
              type={status.enabled ? 'info' : 'warning'}
              showIcon
              message={t('account.twofa.forcedNotice')}
            />
          )}

          {status?.enabled ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <SafetyCertificateOutlined style={{ fontSize: 28, color: token.colorSuccess }} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <Text strong style={{ display: 'block' }}>
                  {t('account.twofa.enabled')}
                </Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {t('account.twofa.enabledDesc')}
                </Text>
              </div>
              <Tooltip title={status.forced ? t('account.twofa.disableForcedTooltip') : ''}>
                <Button danger disabled={status.forced} onClick={() => setDisableOpen(true)}>
                  {t('account.twofa.disable')}
                </Button>
              </Tooltip>
            </div>
          ) : setup ? (
            <div style={{ display: 'flex', gap: 32, flexWrap: 'wrap' }}>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, flexShrink: 0 }}>
                <div
                  style={{
                    padding: 12,
                    lineHeight: 0,
                    borderRadius: token.borderRadiusLG,
                    border: `1px solid ${token.colorBorderSecondary}`,
                  }}
                >
                  {setupQr && (
                    <img
                      src={setupQr}
                      alt={t('account.twofa.qrAlt')}
                      style={{ width: 168, height: 168, display: 'block' }}
                    />
                  )}
                </div>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {t('account.twofa.bindStep1Desc')}
                </Text>
              </div>
              <div style={{ flex: 1, minWidth: 260, display: 'flex', flexDirection: 'column', gap: 20 }}>
                <div>
                  <StepHeader index={1} title={t('account.twofa.bindStep1')} />
                  <Text type="secondary" style={{ display: 'block', fontSize: 12, marginBottom: 8 }}>
                    {t('account.twofa.secretHint')}
                  </Text>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                      padding: '8px 12px',
                      borderRadius: token.borderRadius,
                      border: `1px solid ${token.colorBorderSecondary}`,
                      background: token.colorFillQuaternary,
                    }}
                  >
                    <Text
                      copyable={{ text: setup.secret }}
                      style={{ fontFamily: token.fontFamilyCode, fontSize: 13, wordBreak: 'break-all', flex: 1 }}
                    >
                      {setup.secret}
                    </Text>
                  </div>
                </div>
                <div>
                  <StepHeader index={2} title={t('account.twofa.bindStep2')} />
                  <div style={{ display: 'flex', gap: 8 }}>
                    <Input
                      size="large"
                      placeholder={t('account.twofa.codePlaceholder')}
                      maxLength={6}
                      inputMode="numeric"
                      style={{ width: 200, textAlign: 'center', fontWeight: 600 }}
                      value={bindCode}
                      onChange={(e) => setBindCode(e.target.value.replace(/\D/g, ''))}
                      onPressEnter={handleConfirmBind}
                    />
                    <Button
                      size="large"
                      type="primary"
                      loading={binding}
                      disabled={bindCode.length !== 6}
                      onClick={handleConfirmBind}
                    >
                      {t('account.twofa.confirmBind')}
                    </Button>
                  </div>
                  <Button type="text" style={{ marginTop: 8, paddingLeft: 0 }} onClick={() => setSetup(null)}>
                    {t('account.twofa.cancelBind')}
                  </Button>
                </div>
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <SafetyCertificateOutlined style={{ fontSize: 28, color: token.colorTextSecondary }} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <Text strong style={{ display: 'block' }}>
                  {t('account.twofa.notEnabled')}
                </Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {t('account.twofa.notEnabledDesc')}
                </Text>
              </div>
              <Button type="primary" onClick={handleStartBind}>
                {t('account.twofa.startBind')}
              </Button>
            </div>
          )}
        </>
      )}

      <Modal
        title={t('account.twofa.disable')}
        open={disableOpen}
        maskClosable={false}
        onCancel={() => {
          setDisableOpen(false)
          setDisableCode('')
        }}
        onOk={handleDisable}
        okText={t('account.twofa.disable')}
        okButtonProps={{ danger: true, loading: disabling, disabled: disableCode.length !== 6 }}
        cancelText={t('account.twofa.cancelText')}
        destroyOnHidden
      >
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <Text type="secondary">{t('account.twofa.disableConfirmHint')}</Text>
          <Input
            placeholder={t('account.twofa.codePlaceholder')}
            maxLength={6}
            inputMode="numeric"
            value={disableCode}
            onChange={(e) => setDisableCode(e.target.value.replace(/\D/g, ''))}
          />
        </Space>
      </Modal>
    </Card>
  )
}
