import { Col, Checkbox, Form, Input, InputNumber, Radio, Row, Select } from 'antd'
import type { FormInstance } from 'antd'
import { useTranslation } from 'react-i18next'
import type { WikiDocumentPartitionOverlapUnit, WikiDocumentPartitionSizeUnit } from '@/api/wiki'
import type { ModelOption, WikiWorkflowFormValues } from './wikiWorkflow'

interface WikiWorkflowFormFieldsProps {
  form: FormInstance<WikiWorkflowFormValues>
  conversationModels: ModelOption[]
  modelsLoading?: boolean
  modelsFailed?: boolean
}

/**
 * 默认工作流三个步骤（切割 / 生成元数据 / 向量化）的勾选与参数表单字段，不包含 <Form> 包装，由调用方提供。
 * 知识库设置页（编辑默认工作流）与文件列表批量处理弹窗共用。
 */
export function WikiWorkflowFormFields({ form, conversationModels, modelsLoading, modelsFailed }: WikiWorkflowFormFieldsProps) {
  const { t } = useTranslation()
  const partitionEnabled = Form.useWatch('partitionEnabled', form) ?? false
  const metadataEnabled = Form.useWatch('metadataEnabled', form) ?? false
  const embeddingEnabled = Form.useWatch('embeddingEnabled', form) ?? false
  const sizeUnit = Form.useWatch('sizeUnit', form)
  const partitionMode = Form.useWatch('partitionMode', form) ?? 'normal'

  return (
    <Row gutter={[16, 0]}>
      <Col span={24}>
        <Form.Item name="partitionEnabled" valuePropName="checked" extra={t('wiki.workflow.stepPartitionHint')} style={{ marginBottom: 8 }}>
          <Checkbox>{t('wiki.workflow.stepPartition')}</Checkbox>
        </Form.Item>
      </Col>
      {partitionEnabled && (
        <>
          <Col span={24}>
            <Form.Item name="partitionMode" label={t('wiki.workflow.partitionMode')} style={{ marginBottom: 8 }}>
              <Radio.Group buttonStyle="solid">
                <Radio.Button value="normal">{t('wiki.workflow.partitionModeNormal')}</Radio.Button>
                <Radio.Button value="ai">{t('wiki.workflow.partitionModeAi')}</Radio.Button>
              </Radio.Group>
            </Form.Item>
          </Col>
          {partitionMode === 'ai' && (
            <>
              <Col xs={24} sm={12}>
                <Form.Item
                  name="aiModelId"
                  label={t('wiki.doc.partitionAiModel')}
                  rules={[{ required: true, message: t('wiki.doc.partitionAiModelPlaceholder') }]}
                >
                  <Select
                    placeholder={t('wiki.doc.partitionAiModelPlaceholder')}
                    loading={modelsLoading}
                    disabled={modelsFailed}
                    showSearch
                    virtual={false}
                    options={conversationModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
                  />
                </Form.Item>
              </Col>
              <Col span={24}>
                <Form.Item
                  name="promptTemplate"
                  label={t('wiki.doc.partitionPrompt')}
                  extra={t('wiki.doc.partitionPromptExtra')}
                >
                  <Input.TextArea rows={3} placeholder={t('wiki.doc.partitionPromptPlaceholder')} />
                </Form.Item>
              </Col>
            </>
          )}
          {partitionMode === 'normal' && (
            <>
          <Col xs={24} sm={12}>
            <Form.Item
              name="splitMode"
              label={t('wiki.doc.partitionSplitMode')}
              rules={[{ required: true, message: t('wiki.doc.partitionSplitModeRequired') }]}
            >
              <Select
                options={[
                  { value: 'markdown', label: t('wiki.doc.partitionSplitModeMarkdown') },
                  { value: 'recursive', label: t('wiki.doc.partitionSplitModeRecursive') },
                  { value: 'fixedSize', label: t('wiki.doc.partitionSplitModeFixedSize') },
                  { value: 'sentence', label: t('wiki.doc.partitionSplitModeSentence') },
                  { value: 'paragraph', label: t('wiki.doc.partitionSplitModeParagraph') },
                ]}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              name="sizeUnit"
              label={t('wiki.doc.partitionSizeUnit')}
              rules={[{ required: true, message: t('wiki.doc.partitionSizeUnitRequired') }]}
            >
              <Select
                options={[
                  { value: 'character', label: t('wiki.doc.partitionSizeUnitCharacter') },
                  { value: 'token', label: t('wiki.doc.partitionSizeUnitToken') },
                ]}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item noStyle shouldUpdate={(prev, next) => prev.sizeUnit !== next.sizeUnit}>
              {({ getFieldValue }) => (
                <Form.Item
                  name="chunkSize"
                  label={getFieldValue('sizeUnit') === 'token' ? t('wiki.doc.partitionChunkSizeToken') : t('wiki.doc.partitionChunkSizeCharacter')}
                  rules={[
                    { required: true, message: t('wiki.doc.partitionChunkSizeRequired') },
                    { type: 'integer', min: 1, max: 8192, message: t('wiki.doc.partitionChunkSizeInvalid') },
                  ]}
                >
                  <InputNumber
                    min={1}
                    max={8192}
                    precision={0}
                    style={{ width: '100%' }}
                    placeholder={getFieldValue('sizeUnit') === 'token' ? t('wiki.doc.partitionChunkSizeTokenPlaceholder') : t('wiki.doc.partitionChunkSizeCharacterPlaceholder')}
                  />
                </Form.Item>
              )}
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item noStyle shouldUpdate={(prev, next) => prev.overlapUnit !== next.overlapUnit || prev.sizeUnit !== next.sizeUnit}>
              {({ getFieldValue }) => {
                const overlapUnit = getFieldValue('overlapUnit') as WikiDocumentPartitionOverlapUnit
                const sizeUnit = getFieldValue('sizeUnit') as WikiDocumentPartitionSizeUnit
                const label = overlapUnit === 'sentence'
                  ? t('wiki.doc.partitionChunkOverlapSentence')
                  : overlapUnit === 'paragraph'
                    ? t('wiki.doc.partitionChunkOverlapParagraph')
                    : t('wiki.doc.partitionChunkOverlapCharacter')
                const placeholder = overlapUnit === 'sentence'
                  ? t('wiki.doc.partitionChunkOverlapSentencePlaceholder')
                  : overlapUnit === 'paragraph'
                    ? t('wiki.doc.partitionChunkOverlapParagraphPlaceholder')
                    : t('wiki.doc.partitionChunkOverlapCharacterPlaceholder')
                const invalidMessage = overlapUnit === 'character' && sizeUnit === 'character'
                  ? t('wiki.doc.partitionChunkOverlapCharacterInvalid')
                  : t('wiki.doc.partitionChunkOverlapUnitInvalid')
                return (
                  <Form.Item
                    name="chunkOverlap"
                    label={label}
                    rules={[
                      { required: true, message: t('wiki.doc.partitionChunkOverlapRequired') },
                      {
                        validator: (_, value) => {
                          const num = Number(value)
                          if (!Number.isInteger(num) || num < 0 || num > 8192) return Promise.reject(new Error(invalidMessage))
                          if (overlapUnit === 'character' && num >= Number(getFieldValue('chunkSize'))) {
                            return Promise.reject(new Error(invalidMessage))
                          }
                          return Promise.resolve()
                        },
                      },
                    ]}
                  >
                    <InputNumber min={0} max={8192} precision={0} style={{ width: '100%' }} placeholder={placeholder} />
                  </Form.Item>
                )
              }}
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              name="overlapUnit"
              label={t('wiki.doc.partitionOverlapUnit')}
              rules={[{ required: true, message: t('wiki.doc.partitionOverlapUnitRequired') }]}
            >
              <Select
                options={[
                  { value: 'character', label: t('wiki.doc.partitionOverlapUnitCharacter') },
                  { value: 'sentence', label: t('wiki.doc.partitionOverlapUnitSentence') },
                  { value: 'paragraph', label: t('wiki.doc.partitionOverlapUnitParagraph') },
                ]}
              />
            </Form.Item>
          </Col>
          {sizeUnit === 'token' && (
            <Col xs={24} sm={12}>
              <Form.Item
                name="tokenEncodingOrModel"
                label={t('wiki.doc.partitionTokenEncoding')}
                rules={[{ max: 64, message: t('wiki.doc.partitionTokenEncodingInvalid') }]}
              >
                <Input placeholder="cl100k_base" maxLength={64} />
              </Form.Item>
            </Col>
          )}
          </>
          )}
        </>
      )}

      <Col span={24}>
        <Form.Item name="metadataEnabled" valuePropName="checked" extra={t('wiki.workflow.stepMetadataHint')} style={{ marginBottom: 8 }}>
          <Checkbox>{t('wiki.workflow.stepMetadata')}</Checkbox>
        </Form.Item>
      </Col>
      {metadataEnabled && (
        <>
          <Col xs={24} sm={12}>
            <Form.Item
              name="metadataModelId"
              label={t('wiki.workflow.metadataModel')}
              rules={[{ required: true, message: t('wiki.doc.metadataGenerateModelRequired') }]}
            >
              <Select
                placeholder={t('wiki.doc.metadataGenerateModelPlaceholder')}
                loading={modelsLoading}
                disabled={modelsFailed}
                showSearch
                virtual={false}
                options={conversationModels.map((model) => ({ value: model.id ?? '', label: model.name ?? model.id ?? '' }))}
              />
            </Form.Item>
          </Col>
          <Col span={24}>
            <Form.Item
              name="strategyTypes"
              label={t('wiki.doc.metadataGenerateStrategy')}
              extra={t('wiki.workflow.strategyTypesExtra')}
            >
              <Select
                mode="multiple"
                allowClear
                placeholder={t('wiki.workflow.strategyAll')}
                options={[
                  { value: 'outlineGeneration', label: t('wiki.doc.metadataStrategyOutline') },
                  { value: 'questionGeneration', label: t('wiki.doc.metadataStrategyQuestion') },
                  { value: 'keywordSummaryFusion', label: t('wiki.doc.metadataStrategyKeywordSummary') },
                  { value: 'semanticAggregation', label: t('wiki.doc.metadataStrategySemantic') },
                ]}
              />
            </Form.Item>
          </Col>
        </>
      )}

      <Col span={24}>
        <Form.Item name="embeddingEnabled" valuePropName="checked" extra={t('wiki.workflow.stepEmbeddingHint')} style={{ marginBottom: 8 }}>
          <Checkbox>{t('wiki.workflow.stepEmbedding')}</Checkbox>
        </Form.Item>
      </Col>
      {embeddingEnabled && (
        <>
          <Col xs={24} sm={12}>
            <Form.Item name="embedSourceText" valuePropName="checked" style={{ marginBottom: 8 }}>
              <Checkbox>{t('wiki.workflow.embedSourceText')}</Checkbox>
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item name="embedMetadata" valuePropName="checked" style={{ marginBottom: 8 }}>
              <Checkbox>{t('wiki.workflow.embedMetadata')}</Checkbox>
            </Form.Item>
          </Col>
        </>
      )}
    </Row>
  )
}
