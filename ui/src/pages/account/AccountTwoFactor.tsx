import { useEffect, useRef } from 'react'
import { useSearchParams } from 'react-router'
import { Alert } from 'antd'
import { useTranslation } from 'react-i18next'
import { Page } from '@/design-system'
import { TwoFactorPanel } from '@/pages/account/TwoFactorPanel'

/** 两步验证（2FA）：独立分区页；强制绑定流程经 /account/twofa?bind2fa=1 落地并顶部提示 */
export function AccountTwoFactor() {
  const { t } = useTranslation()
  const [searchParams] = useSearchParams()
  const panelRef = useRef<HTMLDivElement>(null)
  const forceBind2fa = searchParams.get('bind2fa') === '1'

  useEffect(() => {
    if (forceBind2fa) panelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [forceBind2fa])

  return (
    <Page>
      <div ref={panelRef} style={{ maxWidth: 720, margin: '0 auto', display: 'flex', flexDirection: 'column', gap: 24 }}>
        {forceBind2fa && (
          <Alert
            type="warning"
            showIcon
            message={t('account.twofa.forceBindNotice')}
            description={t('account.twofa.forceBindNoticeDesc')}
          />
        )}
        <TwoFactorPanel />
      </div>
    </Page>
  )
}
