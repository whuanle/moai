import { DownloadOutlined } from '@ant-design/icons'
import { Button, List, Modal, Space, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { downloadSkillFiles, type SkillDetail } from '@/api/skills'
import { ReactMarkdownPreview } from '@/pages/prompts/PromptDetailModal'

const { Paragraph, Text } = Typography

/** 技能详情弹窗：描述 + Markdown 渲染使用说明 + 技能包文件下载（市场/个人/团队分区共用） */
export function SkillDetailModal({
  open,
  detail,
  onClose,
}: {
  open: boolean
  detail: SkillDetail | null
  onClose: () => void
}) {
  const { t } = useTranslation()

  const canDownload = !!detail && !detail.isSystem

  const handleDownload = async () => {
    if (!detail?.id) return
    try {
      await downloadSkillFiles(detail.id)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  return (
    <Modal
      open={open}
      title={detail ? `${detail.name ?? ''}（${detail.key ?? ''}）` : t('skills.detail')}
      footer={
        canDownload ? (
          <Space>
            <Button type="primary" icon={<DownloadOutlined />} onClick={() => void handleDownload()}>
              {t('skills.download')}
            </Button>
            <Button onClick={onClose}>{t('skills.close')}</Button>
          </Space>
        ) : null
      }
      onCancel={onClose}
      width={720}
      maskClosable={false}
      destroyOnHidden
    >
      {detail && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
          <div>
            <Text strong>{t('skills.formDescription')}</Text>
            <Paragraph type="secondary" style={{ marginBottom: 0 }}>
              {detail.description || '-'}
            </Paragraph>
          </div>
          <div>
            <Text strong>{t('skills.formInstructions')}</Text>
            <div
              style={{
                border: '1px solid rgba(128,128,128,0.25)',
                borderRadius: 8,
                padding: '8px 16px',
                maxHeight: 360,
                overflow: 'auto',
                marginTop: spacing.xs,
              }}
            >
              {detail.instructions ? (
                <ReactMarkdownPreview content={detail.instructions} />
              ) : (
                <Text type="secondary">-</Text>
              )}
            </div>
          </div>
          <div>
            <Text strong>{t('skills.formFiles')}</Text>
            {detail.files && detail.files.length > 0 ? (
              <List
                size="small"
                dataSource={detail.files}
                renderItem={(file) => (
                  <List.Item
                    actions={
                      canDownload
                        ? [
                            <Button
                              key="download"
                              type="text"
                              size="small"
                              icon={<DownloadOutlined />}
                              aria-label={`${t('skills.download')}-${file.path ?? ''}`}
                              onClick={() => {
                                if (!detail.id) return
                                void downloadSkillFiles(detail.id)
                              }}
                            >
                              {t('skills.download')}
                            </Button>,
                          ]
                        : undefined
                    }
                  >
                    <Text code>{file.path}</Text>
                  </List.Item>
                )}
              />
            ) : (
              <Paragraph type="secondary" style={{ marginBottom: 0 }}>
                -
              </Paragraph>
            )}
          </div>
          <Space size={spacing.sm} wrap>
            {detail.isSystem ? <Tag color="geekblue">{t('skills.isSystem')}</Tag> : null}
            {detail.isPublic ? <Tag color="success">{t('skills.statusPublic')}</Tag> : null}
            <Text type="secondary">
              {t('skills.colUpdateTime')}: {formatDateTime(detail.updateTime)}
            </Text>
          </Space>
        </div>
      )}
    </Modal>
  )
}
