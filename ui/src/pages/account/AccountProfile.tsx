import { useEffect, useState } from 'react'
import { Avatar, Button, Form, Input, Typography, Upload } from 'antd'
import { UploadOutlined } from '@ant-design/icons'
import type { UploadProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card, Page, feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { refreshUserProfile } from '@/api/auth'
import { updateUserInfo, uploadAvatar } from '@/api/account'

const { Text, Title } = Typography

interface BasicFormValues {
  nickName?: string
  phone?: string
}

/** 基本信息：头像维护 + 昵称/手机号 */
export function AccountProfile() {
  const { t } = useTranslation()
  const userInfo = useAppStore((state) => state.userInfo)
  const [basicForm] = Form.useForm<BasicFormValues>()
  const [basicLoading, setBasicLoading] = useState(false)
  const [avatarLoading, setAvatarLoading] = useState(false)

  const avatar = resolveStorageUrl(userInfo?.avatar)
  const displayName = userInfo?.nickName ?? userInfo?.userName

  useEffect(() => {
    basicForm.setFieldsValue({
      nickName: userInfo?.nickName ?? undefined,
      phone: userInfo?.phone ?? undefined,
    })
  }, [userInfo?.nickName, userInfo?.phone, basicForm])

  const handleBasicSubmit = async (values: BasicFormValues) => {
    setBasicLoading(true)
    try {
      await updateUserInfo({ nickName: values.nickName, phone: values.phone })
      await refreshUserProfile()
      feedback.success(t('account.saveSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setBasicLoading(false)
    }
  }

  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    const isImage = file.type.startsWith('image/')
    if (!isImage) {
      feedback.error(t('account.avatarTypeError'))
      return Upload.LIST_IGNORE
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('account.avatarSizeError'))
      return Upload.LIST_IGNORE
    }
    return true
  }

  const handleAvatarUpload: UploadProps['customRequest'] = async ({ file }) => {
    if (!(file instanceof File)) return
    setAvatarLoading(true)
    try {
      await uploadAvatar(file)
      await refreshUserProfile()
      feedback.success(t('account.avatarSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setAvatarLoading(false)
    }
  }

  return (
    <Page>
      <div style={{ maxWidth: 720, display: 'flex', flexDirection: 'column', gap: 24 }}>
        <Card>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Avatar size={72} src={avatar || undefined} icon={<UploadOutlined />}>
              {displayName?.charAt(0) ?? 'U'}
            </Avatar>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Title level={5} style={{ margin: 0 }}>
                {displayName}
              </Title>
              <Text type="secondary">{userInfo?.email ?? userInfo?.userName}</Text>
            </div>
            <Upload
              accept="image/*"
              showUploadList={false}
              beforeUpload={beforeUpload}
              customRequest={handleAvatarUpload}
            >
              <Button icon={<UploadOutlined />} loading={avatarLoading}>
                {t('account.uploadAvatar')}
              </Button>
            </Upload>
          </div>
        </Card>

        <Card title={t('account.basicTitle')}>
          <Form<BasicFormValues> form={basicForm} layout="vertical" onFinish={handleBasicSubmit}>
            <Form.Item
              name="nickName"
              label={t('auth.nickName')}
              rules={[{ required: true, message: t('auth.nickNamePlaceholder') }]}
            >
              <Input maxLength={50} />
            </Form.Item>
            <Form.Item
              name="phone"
              label={t('auth.phone')}
              rules={[
                {
                  validator: (_, value) =>
                    !value || /^[\d+\-()\s]{5,20}$/.test(value)
                      ? Promise.resolve()
                      : Promise.reject(new Error(t('account.phoneInvalid'))),
                },
              ]}
            >
              <Input maxLength={20} />
            </Form.Item>
            <Form.Item style={{ marginBottom: 0 }}>
              <Button type="primary" htmlType="submit" loading={basicLoading}>
                {t('account.save')}
              </Button>
            </Form.Item>
          </Form>
        </Card>
      </div>
    </Page>
  )
}
