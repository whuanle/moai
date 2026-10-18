import { describe, expect, it } from 'vitest'
import { chartFileName } from '../chat/visChartExport'

describe('chartFileName（图表下载文件名净化）', () => {
  it('保留正常标题', () => {
    expect(chartFileName('近21个月月度GMV趋势（2025-01 ~ 2026-09）')).toBe(
      '近21个月月度GMV趋势（2025-01 ~ 2026-09）.png',
    )
  })

  it('替换文件系统非法字符', () => {
    expect(chartFileName('月度GMV环比:增速*报告')).toBe('月度GMV环比 增速 报告.png')
  })

  it('空标题回退 chart', () => {
    expect(chartFileName('')).toBe('chart.png')
    expect(chartFileName('///')).toBe('chart.png')
  })
})
