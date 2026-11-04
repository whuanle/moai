import { Button, Input, Space, Tooltip } from 'antd'
import { ReloadOutlined, SafetyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { CaptchaImage } from '@/api/auth'

interface CaptchaFieldProps {
  /** antd Form 受控值（用户输入的验证码） */
  value?: string
  onChange?: (value: string) => void
  /** 当前图形验证码（null 表示加载中） */
  captcha: CaptchaImage | null
  /** 点击图片换一张 */
  onRefresh: () => void
  size?: 'large' | 'middle'
}

/** 图形验证码输入行（登录/注册/找回密码共用）：输入框 + 图片按钮（点击换一张） */
export function CaptchaField({ value, onChange, captcha, onRefresh, size = 'large' }: CaptchaFieldProps) {
  const { t } = useTranslation()
  return (
    <Space.Compact style={{ width: '100%' }}>
      <Input
        prefix={<SafetyOutlined />}
        placeholder={t('auth.captcha.placeholder')}
        size={size}
        maxLength={4}
        value={value}
        onChange={(e) => onChange?.(e.target.value.replace(/\s/g, ''))}
      />
      <Tooltip title={t('auth.captcha.refresh')}>
        <Button size={size} style={{ width: 120, padding: 0, overflow: 'hidden' }} onClick={onRefresh}>
          {captcha ? (
            <img
              src={`data:${captcha.imageMimeType};base64,${captcha.image}`}
              alt={t('auth.captcha.refresh')}
              style={{ width: '100%', height: '100%', display: 'block' }}
            />
          ) : (
            <ReloadOutlined spin />
          )}
        </Button>
      </Tooltip>
    </Space.Compact>
  )
}
