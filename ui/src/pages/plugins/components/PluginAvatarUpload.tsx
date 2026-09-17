import { useState } from 'react'
import { CameraOutlined } from '@ant-design/icons'
import { Avatar, Upload, theme } from 'antd'
import type { UploadProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { pluginApi } from '@/api/plugin'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'

interface PluginAvatarProps {
  /** 当前头像 ObjectKey，空表示未设置. */
  objectKey?: string | null
  /** 插件标题，无头像时兜底显示首字符. */
  title?: string | null
  /** 尺寸（px）. */
  size?: number
}

/** 插件头像展示：有 objectKey 时拼装 /static 地址，否则显示标题首字符. */
export function PluginAvatar({ objectKey, title, size = 22 }: PluginAvatarProps) {
  return (
    <Avatar size={size} src={objectKey ? resolveStorageUrl(objectKey) : undefined}>
      {(title ?? '?').slice(0, 1).toUpperCase()}
    </Avatar>
  )
}

interface PluginAvatarUploadProps {
  /** 插件记录 id（/ai/plugin/manage/{id}/avatar 登记）. */
  pluginId: string
  /** 当前头像 ObjectKey. */
  objectKey?: string | null
  /** 插件标题，无头像时兜底显示首字符. */
  title?: string | null
  /** 上传登记成功后回调（父级刷新列表）. */
  onChanged?: () => void
}

/** 插件头像上传（仅编辑态）：选图后直传存储并立即登记，5MB 内图片. */
export function PluginAvatarUpload({ pluginId, objectKey, title, onChanged }: PluginAvatarUploadProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [uploading, setUploading] = useState(false)

  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('plugins.avatarTypeError'))
      return Upload.LIST_IGNORE
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('plugins.avatarSizeError'))
      return Upload.LIST_IGNORE
    }
    setUploading(true)
    ;(async () => {
      const { objectKey: key } = await uploadImageWithKey(file)
      await pluginApi.updatePluginAvatar(pluginId, key)
      feedback.success(t('plugins.avatarSuccess'))
      onChanged?.()
    })()
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => setUploading(false))
    return Upload.LIST_IGNORE
  }

  return (
    <Upload beforeUpload={beforeUpload} showUploadList={false} accept="image/*">
      <div style={{ position: 'relative', lineHeight: 0, cursor: 'pointer' }} title={t('plugins.avatar')}>
        <Avatar size={56} src={objectKey ? resolveStorageUrl(objectKey) : undefined} style={{ opacity: uploading ? 0.55 : 1 }}>
          {(title ?? '?').slice(0, 1).toUpperCase()}
        </Avatar>
        <div
          style={{
            position: 'absolute',
            right: -2,
            bottom: -2,
            width: 22,
            height: 22,
            borderRadius: '50%',
            background: token.colorPrimary,
            color: token.colorTextLightSolid,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 11,
            border: `2px solid ${token.colorBgContainer}`,
          }}
        >
          <CameraOutlined />
        </div>
      </div>
    </Upload>
  )
}
