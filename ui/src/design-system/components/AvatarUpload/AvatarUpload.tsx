import { useState, type ReactNode } from 'react'
import { UploadOutlined } from '@ant-design/icons'
import { Avatar, Space, Spin, Upload, theme } from 'antd'
import { useTranslation } from 'react-i18next'

export interface AvatarUploadProps {
  /** 头像图片地址；空时显示 fallback. */
  src?: string | null
  /** 无头像时的兜底内容（首字符或图标）. */
  fallback?: ReactNode
  /** 形状，默认圆形. */
  shape?: 'circle' | 'square'
  /** 尺寸（px），默认 96；小于 80 时悬停遮罩只显示图标. */
  size?: number
  /** 上传中：遮罩显示 Spin 并禁止再次点击. */
  uploading?: boolean
  /** 禁用上传（只读态）. */
  disabled?: boolean
  /** 选中图片文件回调；类型/大小校验与上传登记由调用方处理. */
  onSelect: (file: File) => void
}

/**
 * 头像即上传入口：点击头像选图，悬停显示「点击上传头像」遮罩，上传中显示加载遮罩。
 * src 变化时自动重挂载 Avatar，避免浏览器缓存旧图。
 */
export function AvatarUpload({ src, fallback, shape = 'circle', size = 96, uploading = false, disabled = false, onSelect }: AvatarUploadProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [hovering, setHovering] = useState(false)
  const showMask = !disabled && (hovering || uploading)
  return (
    <Upload
      beforeUpload={(file) => {
        onSelect(file)
        return Upload.LIST_IGNORE
      }}
      showUploadList={false}
      accept="image/*"
      disabled={disabled || uploading}
    >
      <div
        title={t('common.clickToUploadAvatar')}
        onMouseEnter={() => setHovering(true)}
        onMouseLeave={() => setHovering(false)}
        style={{
          position: 'relative',
          width: size,
          height: size,
          borderRadius: shape === 'circle' ? '50%' : token.borderRadius,
          overflow: 'hidden',
          cursor: disabled || uploading ? 'not-allowed' : 'pointer',
          flexShrink: 0,
          lineHeight: 0,
        }}
      >
        {/* key：src 变化时强制重挂载，避免 Avatar 缓存旧图 */}
        <Avatar key={src ?? 'default'} shape={shape} size={size} src={src || undefined}>
          {fallback}
        </Avatar>
        {showMask && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              background: token.colorBgMask,
              color: token.colorTextLightSolid,
              fontSize: 12,
            }}
          >
            {uploading ? (
              <Spin size="small" />
            ) : size >= 80 ? (
              <Space size={2} direction="vertical" style={{ alignItems: 'center' }}>
                <UploadOutlined />
                <span>{t('common.clickToUploadAvatar')}</span>
              </Space>
            ) : (
              <UploadOutlined style={{ fontSize: 14 }} />
            )}
          </div>
        )}
      </div>
    </Upload>
  )
}
