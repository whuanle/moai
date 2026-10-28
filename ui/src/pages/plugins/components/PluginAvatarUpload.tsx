import { useState } from 'react'
import { Avatar } from 'antd'
import { useTranslation } from 'react-i18next'
import { AvatarUpload, feedback } from '@/design-system'
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

/** 校验并直传头像图片，返回 ObjectKey；登记由调用方负责（编辑态即登记、新建态随保存命令提交）. */
async function uploadAvatarImage(file: File): Promise<string> {
  if (!file.type.startsWith('image/')) {
    throw new Error('avatarTypeError')
  }
  if (file.size > 5 * 1024 * 1024) {
    throw new Error('avatarSizeError')
  }
  const { objectKey } = await uploadImageWithKey(file)
  return objectKey
}

/** 插件头像上传（仅编辑态）：点击头像选图，悬停显示上传提示，选图后直传存储并立即登记，5MB 内图片. */
export function PluginAvatarUpload({ pluginId, objectKey, title, onChanged }: PluginAvatarUploadProps) {
  const { t } = useTranslation()
  const [uploading, setUploading] = useState(false)

  const handleFile = (file: File) => {
    setUploading(true)
    ;(async () => {
      const key = await uploadAvatarImage(file)
      await pluginApi.updatePluginAvatar(pluginId, key)
      feedback.success(t('plugins.avatarSuccess'))
      onChanged?.()
    })()
      .catch((err) => {
        if (err instanceof Error && (err.message === 'avatarTypeError' || err.message === 'avatarSizeError')) {
          feedback.error(t(`plugins.${err.message}`))
          return
        }
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => setUploading(false))
  }

  return (
    <AvatarUpload
      src={objectKey ? resolveStorageUrl(objectKey) : undefined}
      fallback={(title ?? '?').slice(0, 1).toUpperCase()}
      size={56}
      uploading={uploading}
      onSelect={handleFile}
    />
  )
}

interface PluginAvatarPendingUploadProps {
  /** 已选头像 ObjectKey，空表示未设置. */
  objectKey?: string | null
  /** 标题，无头像时兜底显示首字符. */
  title?: string | null
  /** 选图直传成功后回传 ObjectKey（随保存命令提交，不立即登记）. */
  onObjectKeyChange: (objectKey: string) => void
}

/** 插件头像上传（新建态）：插件记录尚未生成，仅直传存储并暂存 ObjectKey，保存时随命令提交. */
export function PluginAvatarPendingUpload({ objectKey, title, onObjectKeyChange }: PluginAvatarPendingUploadProps) {
  const { t } = useTranslation()
  const [uploading, setUploading] = useState(false)

  const handleFile = (file: File) => {
    setUploading(true)
    ;(async () => {
      const key = await uploadAvatarImage(file)
      onObjectKeyChange(key)
      feedback.success(t('plugins.avatarSuccess'))
    })()
      .catch((err) => {
        if (err instanceof Error && (err.message === 'avatarTypeError' || err.message === 'avatarSizeError')) {
          feedback.error(t(`plugins.${err.message}`))
          return
        }
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => setUploading(false))
  }

  return (
    <AvatarUpload
      src={objectKey ? resolveStorageUrl(objectKey) : undefined}
      fallback={(title ?? '?').slice(0, 1).toUpperCase()}
      size={56}
      uploading={uploading}
      onSelect={handleFile}
    />
  )
}
