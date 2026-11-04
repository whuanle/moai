import { useState } from 'react';
import { Avatar } from 'antd';
import { useTranslation } from 'react-i18next';
import { AvatarUpload, feedback } from '@/design-system';
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage';

/** 校验并直传头像图片，返回 ObjectKey；登记由调用方随保存命令提交（不在本组件内落库）. */
async function uploadAvatarImage(file: File): Promise<string> {
  if (!file.type.startsWith('image/')) {
    throw new Error('avatarTypeError');
  }
  if (file.size > 5 * 1024 * 1024) {
    throw new Error('avatarSizeError');
  }
  const { objectKey } = await uploadImageWithKey(file);
  return objectKey;
}

interface AccessAppAvatarProps {
  /** 当前头像 ObjectKey，空表示未设置. */
  objectKey?: string | null
  /** 接入名称，无头像时兜底显示首字符. */
  name?: string | null
  /** 尺寸（px）. */
  size?: number
}

/** 接入头像展示：有 objectKey 时拼装 /static 地址，否则显示名称首字符. */
export function AccessAppAvatar({ objectKey, name, size = 36 }: AccessAppAvatarProps) {
  return (
    <Avatar size={size} src={objectKey ? resolveStorageUrl(objectKey) : undefined}>
      {(name ?? '?').slice(0, 1).toUpperCase()}
    </Avatar>
  );
}

interface AccessAppAvatarUploadProps {
  /** 已选头像 ObjectKey，空表示未设置. */
  objectKey?: string | null
  /** 接入名称，无头像时兜底显示首字符. */
  name?: string | null
  /** 选图直传成功后回传 ObjectKey（随保存命令提交，不立即登记）. */
  onObjectKeyChange: (objectKey: string) => void
}

/** 接入头像上传（新建/编辑弹窗内）：点击头像选图，直传存储暂存 ObjectKey，保存时随命令提交. */
export function AccessAppAvatarUpload({ objectKey, name, onObjectKeyChange }: AccessAppAvatarUploadProps) {
  const { t } = useTranslation();
  const [uploading, setUploading] = useState(false);

  const handleFile = (file: File) => {
    setUploading(true);
    ;(async () => {
      const key = await uploadAvatarImage(file);
      onObjectKeyChange(key);
      feedback.success(t('adminAccessApps.avatarSuccess'));
    })()
      .catch((err) => {
        if (err instanceof Error && (err.message === 'avatarTypeError' || err.message === 'avatarSizeError')) {
          feedback.error(t(`adminAccessApps.${err.message}`));
          return;
        }
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => setUploading(false));
  };

  return (
    <AvatarUpload
      src={objectKey ? resolveStorageUrl(objectKey) : undefined}
      fallback={(name ?? '?').slice(0, 1).toUpperCase()}
      size={56}
      uploading={uploading}
      onSelect={handleFile}
    />
  );
}
