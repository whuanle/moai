import type { Locale as AntdLocale } from 'antd/es/locale'
import enUS from 'antd/locale/en_US'
import zhCN from 'antd/locale/zh_CN'
import type { Locale } from '@/store/app'

const antdLocales: Record<Locale, AntdLocale> = {
  'zh-CN': zhCN,
  'en-US': enUS,
}

export function getAntdLocale(locale: Locale): AntdLocale {
  return antdLocales[locale]
}
