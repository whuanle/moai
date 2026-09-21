import { Alert, Col, Form, Input, InputNumber, Row, Switch, Typography } from 'antd'
import type { FormInstance } from 'antd'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'
import { CRAWLER_INTERVAL_PRESETS } from './wikiSourceCrawler'

const { Text } = Typography

interface WikiSourceCrawlerFormFieldsProps {
  /**
   * 承载爬虫字段的 Form 实例。调用方常使用「超集」表单（同时含飞书字段），
   * 因此此处刻意放宽为宽泛的 FormInstance，仅按字段名读写爬虫相关字段。
   */
  form: FormInstance
}

/**
 * 网页爬虫外部源配置字段（起始地址 / 抓取范围 / 抓取频率 / 正文选择器），不含 <Form> 包装，由调用方提供。
 * 抓取范围与频率均有硬上限且后端二次校验，此处仅做即时提示。
 */
export function WikiSourceCrawlerFormFields({ form }: WikiSourceCrawlerFormFieldsProps) {
  const { t } = useTranslation()

  return (
    <Row gutter={[16, 0]}>
      <Col span={24}>
        <Form.Item
          name="startUrl"
          label={t('wiki.source.crawler.startUrl')}
          extra={t('wiki.source.crawler.startUrlExtra')}
          rules={[
            { required: true, message: t('wiki.source.crawler.startUrlRequired') },
            { max: 1000, message: t('wiki.source.crawler.startUrlInvalid') },
            {
              validator: (_, value) => {
                if (!value) return Promise.resolve()
                try {
                  const url = new URL(String(value))
                  return url.protocol === 'http:' || url.protocol === 'https:'
                    ? Promise.resolve()
                    : Promise.reject(new Error(t('wiki.source.crawler.startUrlInvalid')))
                } catch {
                  return Promise.reject(new Error(t('wiki.source.crawler.startUrlInvalid')))
                }
              },
            },
          ]}
        >
          <Input placeholder="https://example.com/docs/" maxLength={1000} />
        </Form.Item>
      </Col>

      <Col span={24}>
        <Form.Item
          name="pathPrefix"
          label={t('wiki.source.crawler.pathPrefix')}
          extra={t('wiki.source.crawler.pathPrefixExtra')}
          rules={[{ max: 1000, message: t('wiki.source.crawler.pathPrefixInvalid') }]}
        >
          <Input placeholder="/docs/" maxLength={1000} />
        </Form.Item>
      </Col>

      <Col xs={24} sm={12}>
        <Form.Item
          name="maxDepth"
          label={t('wiki.source.crawler.maxDepth')}
          extra={t('wiki.source.crawler.maxDepthExtra')}
          rules={[
            { required: true, message: t('wiki.source.crawler.maxDepthRequired') },
            { type: 'integer', min: 1, max: 10, message: t('wiki.source.crawler.maxDepthInvalid') },
          ]}
        >
          <InputNumber min={1} max={10} precision={0} style={{ width: '100%' }} />
        </Form.Item>
      </Col>

      <Col xs={24} sm={12}>
        <Form.Item
          name="maxPages"
          label={t('wiki.source.crawler.maxPages')}
          extra={t('wiki.source.crawler.maxPagesExtra')}
          rules={[
            { required: true, message: t('wiki.source.crawler.maxPagesRequired') },
            { type: 'integer', min: 1, max: 2000, message: t('wiki.source.crawler.maxPagesInvalid') },
          ]}
        >
          <InputNumber min={1} max={2000} precision={0} style={{ width: '100%' }} />
        </Form.Item>
      </Col>

      <Col span={24}>
        <Alert
          type="info"
          showIcon
          message={t('wiki.source.crawler.rateLimitTitle')}
          description={t('wiki.source.crawler.rateLimitHint')}
          style={{ marginBottom: spacing.md }}
        />
      </Col>

      <Col xs={24} sm={12}>
        <Form.Item
          name="requestIntervalSeconds"
          label={t('wiki.source.crawler.interval')}
          extra={t('wiki.source.crawler.intervalExtra')}
          rules={[
            { required: true, message: t('wiki.source.crawler.intervalRequired') },
            { type: 'integer', min: 1, max: 3600, message: t('wiki.source.crawler.intervalInvalid') },
          ]}
        >
          <InputNumber
            min={1}
            max={3600}
            precision={0}
            addonAfter={t('wiki.source.crawler.intervalUnit')}
            style={{ width: '100%' }}
          />
        </Form.Item>
      </Col>

      <Col span={24} style={{ marginTop: -spacing.sm }}>
        <Text type="secondary" style={{ fontSize: 12, marginRight: spacing.xs }}>
          {t('wiki.source.crawler.intervalPresetLabel')}
        </Text>
        {CRAWLER_INTERVAL_PRESETS.map((preset) => (
          <a
            key={preset.key}
            style={{ fontSize: 12, marginRight: spacing.md }}
            onClick={() => form.setFieldValue('requestIntervalSeconds', preset.value)}
          >
            {t(`wiki.source.crawler.intervalPreset.${preset.key}`, { seconds: preset.value })}
          </a>
        ))}
      </Col>

      <Col xs={24} sm={12}>
        <Form.Item
          name="timeoutSeconds"
          label={t('wiki.source.crawler.timeout')}
          extra={t('wiki.source.crawler.timeoutExtra')}
          rules={[
            { required: true, message: t('wiki.source.crawler.timeoutRequired') },
            { type: 'integer', min: 5, max: 300, message: t('wiki.source.crawler.timeoutInvalid') },
          ]}
        >
          <InputNumber min={5} max={300} precision={0} style={{ width: '100%' }} />
        </Form.Item>
      </Col>

      <Col span={24}>
        <Form.Item
          name="contentSelector"
          label={t('wiki.source.crawler.contentSelector')}
          extra={t('wiki.source.crawler.contentSelectorExtra')}
          rules={[{ max: 255, message: t('wiki.source.crawler.contentSelectorInvalid') }]}
        >
          <Input placeholder="article, .markdown-body, #content" maxLength={255} />
        </Form.Item>
      </Col>

      <Col span={24}>
        <Form.Item
          name="userAgent"
          label={t('wiki.source.crawler.userAgent')}
          extra={t('wiki.source.crawler.userAgentExtra')}
          rules={[{ max: 255, message: t('wiki.source.crawler.userAgentInvalid') }]}
        >
          <Input placeholder="MoAI-Crawler/1.0" maxLength={255} />
        </Form.Item>
      </Col>

      <Col span={24}>
        <Form.Item name="isOverwriteExisting" valuePropName="checked" style={{ marginBottom: 8 }}>
          <Switch
            checkedChildren={t('wiki.source.crawler.overwriteOn')}
            unCheckedChildren={t('wiki.source.crawler.overwriteOff')}
          />
        </Form.Item>
        <Text type="secondary" style={{ fontSize: 12 }}>
          {t('wiki.source.crawler.overwriteHint')}
        </Text>
      </Col>
    </Row>
  )
}
