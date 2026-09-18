import { useEffect, useMemo, useState } from 'react'
import { Button, Form, Input, Modal, Select, Typography, Upload } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import {
  createSkill,
  getSkill,
  updateSkill,
  uploadSkillFile,
  type SkillDetail,
  type SkillFileItem,
} from '@/api/skills'

const { Text } = Typography

const keyPattern = /^[a-z][a-z0-9_]{0,29}$/

export interface SkillEditModalProps {
  open: boolean
  /** 编辑目标技能 id，null 表示创建 */
  skillId: string | null
  /** 创建时的归属团队 id，0=个人技能；编辑时不生效（归属不可变） */
  teamId: number
  onSaved: () => void
  onCancel: () => void
}

interface SkillFormValues {
  key: string
  name: string
  description?: string
  instructions?: string
  classifyId?: number
}

/** 技能编辑弹窗：创建/更新共用（标识创建后不可改）；市场/个人/团队分区复用 */
export function SkillEditModal({ open, skillId, teamId, onSaved, onCancel }: SkillEditModalProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<SkillFormValues>()
  const [files, setFiles] = useState<(SkillFileItem & { uid: string })[]>([])
  const [loading, setLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [classifies, setClassifies] = useState<Classify[]>([])

  const classifyOptions = useMemo(
    () => classifies.map((c) => ({ value: Number(c.classifyId), label: classifyLabel(c) })),
    [classifies],
  )

  useEffect(() => {
    classifyApi
      .getClassifies(ClassifyType.Skill)
      .then(setClassifies)
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [])

  useEffect(() => {
    if (!open) return
    form.resetFields()
    setFiles([])
    setDetail(null)
    if (skillId) {
      setLoading(true)
      getSkill(skillId)
        .then((res) => {
          setDetail(res)
          form.setFieldsValue({
            key: res.key ?? '',
            name: res.name ?? '',
            description: res.description ?? '',
            instructions: res.instructions ?? '',
            classifyId: res.classifyId || undefined,
          })
          setFiles(
            (res.files ?? []).map((f, index) => ({
              uid: `loaded-${index}`,
              path: f.path ?? '',
              fileId: f.fileId ?? 0,
              fileName: f.fileName ?? '',
            })),
          )
        })
        .catch(() => {
          // 错误已由全局请求中间件统一提示
        })
        .finally(() => setLoading(false))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, skillId])

  const handleUpload = async (file: File) => {
    setUploading(true)
    try {
      const fileId = await uploadSkillFile(file)
      setFiles((prev) => [...prev, { uid: `f-${fileId}`, path: file.name, fileId, fileName: file.name }])
    } catch {
      feedback.error(t('skills.uploadFailed'))
    } finally {
      setUploading(false)
    }
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      const payload = {
        name: values.name,
        description: values.description ?? '',
        instructions: values.instructions ?? '',
        files: files.map((f) => ({ path: f.path ?? '', fileId: f.fileId ?? 0, fileName: f.fileName ?? '' })),
        classifyId: values.classifyId ?? 0,
      }
      if (skillId) {
        await updateSkill(skillId, payload)
        feedback.success(t('skills.updateSuccess'))
      } else {
        await createSkill({ teamId, key: values.key, ...payload })
        feedback.success(t('skills.createSuccess'))
      }
      onSaved()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const uploadFiles = files.map((f) => ({
    uid: f.uid,
    name: f.path ?? f.fileName ?? f.uid,
    status: 'done' as const,
  }))

  return (
    <Modal
      open={open}
      title={skillId ? t('skills.edit') : t('skills.create')}
      onCancel={onCancel}
      onOk={() => void handleSubmit()}
      okText={t('skills.save')}
      cancelText={t('skills.cancel')}
      confirmLoading={submitting}
      okButtonProps={{ loading: submitting }}
      maskClosable={false}
      destroyOnHidden
      width={640}
    >
      <Form form={form} layout="vertical" disabled={loading}>
        <Form.Item
          name="key"
          label={t('skills.formKey')}
          rules={[
            { required: true, message: t('skills.keyRequired') },
            { pattern: keyPattern, message: t('skills.keyPattern') },
          ]}
          extra={t('skills.keyExtra')}
        >
          <Input maxLength={30} disabled={Boolean(skillId)} />
        </Form.Item>
        <Form.Item
          name="name"
          label={t('skills.formName')}
          rules={[{ required: true, message: t('skills.nameRequired') }]}
        >
          <Input maxLength={50} />
        </Form.Item>
        <Form.Item name="description" label={t('skills.formDescription')} rules={[{ max: 255, message: t('skills.descMaxLength') }]}>
          <Input.TextArea rows={2} maxLength={255} />
        </Form.Item>
        <Form.Item name="classifyId" label={t('skills.formClassify')}>
          <Select allowClear placeholder={t('skills.classifyAll')} options={classifyOptions} />
        </Form.Item>
        <Form.Item name="instructions" label={t('skills.formInstructions')}>
          <Input.TextArea rows={6} placeholder={t('skills.instructionsPlaceholder')} />
        </Form.Item>
        {!(detail?.isSystem ?? false) && (
          <Form.Item label={t('skills.formFiles')} extra={t('skills.filesExtra')}>
            <Upload
              multiple
              fileList={uploadFiles}
              beforeUpload={(file) => {
                void handleUpload(file)
                return false
              }}
              onRemove={(file) => {
                setFiles((prev) => prev.filter((f) => f.uid !== file.uid))
              }}
              accept=".py,.md,.json,.txt,.csv,.yaml,.yml,.j2,.html,.css,.js"
            >
              <Button loading={uploading}>{t('skills.addFile')}</Button>
            </Upload>
          </Form.Item>
        )}
      </Form>
      {detail?.isSystem && <Text type="secondary">{t('skills.systemReadonlyHint')}</Text>}
    </Modal>
  )
}
