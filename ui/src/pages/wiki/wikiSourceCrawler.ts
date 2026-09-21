/** 爬虫配置表单值（对齐后端 WikiSourceCrawlerConfig） */
export interface WikiSourceCrawlerFormValues {
  startUrl: string
  pathPrefix?: string
  maxDepth: number
  maxPages: number
  requestIntervalSeconds: number
  timeoutSeconds: number
  userAgent?: string
  contentSelector?: string
  isOverwriteExisting: boolean
}

/**
 * 预设抓取频率档位：把「间隔秒数」翻译成业务语义，避免用户直接面对秒数不知填多少合适。
 * 值越小抓得越快、对目标站点压力越大。
 */
export const CRAWLER_INTERVAL_PRESETS = [
  { value: 1, key: 'fast' },
  { value: 3, key: 'normal' },
  { value: 10, key: 'slow' },
  { value: 30, key: 'slowest' },
] as const

/** 爬虫配置默认值（对齐后端默认：深度 2、200 页、1 秒间隔、30 秒超时） */
export const DEFAULT_CRAWLER_VALUES: WikiSourceCrawlerFormValues = {
  startUrl: '',
  pathPrefix: '',
  maxDepth: 2,
  maxPages: 200,
  requestIntervalSeconds: 1,
  timeoutSeconds: 30,
  userAgent: '',
  contentSelector: '',
  isOverwriteExisting: true,
}
