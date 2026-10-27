import { Collapse, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'
import { splitNumberedSteps } from '@/utils/text'

interface AppChannelGuideProps {
  /** i18n 命名空间，如 appChannels；取 {ns}.hintTitle 与 {ns}.hint */
  ns: string
}

/**
 * 应用外部渠道「接入指引」：默认收缩避免长文案撑开页面，点击展开后按序号逐条分行展示。
 * 飞书/钉钉/企业微信三个渠道分区共用。
 */
export function AppChannelGuide({ ns }: AppChannelGuideProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const steps = splitNumberedSteps(t(`${ns}.hint`))

  return (
    <Collapse
      ghost
      size="small"
      style={{ marginBottom: spacing.sm }}
      items={[
        {
          key: 'guide',
          label: t(`${ns}.hintTitle`),
          children: (
            <ol style={{ margin: 0, paddingLeft: 20, color: token.colorTextSecondary }}>
              {steps.map((step) => (
                <li key={step} style={{ lineHeight: 1.9 }}>
                  {step}
                </li>
              ))}
            </ol>
          ),
        },
      ]}
    />
  )
}
