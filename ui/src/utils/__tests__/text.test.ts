import { describe, expect, it } from 'vitest'
import { splitNumberedSteps } from '../text'

describe('splitNumberedSteps（序号指引文案拆步）', () => {
  it('中文文案按「N. 」序号拆分并去掉序号与段尾分号', () => {
    const steps = splitNumberedSteps('1. 在飞书开放平台创建应用并添加机器人能力；2. 开通 im:message 权限并订阅「接收消息 im.message.receive_v1」；3. 填入 AppID 与 AppSecret 完成接入。')
    expect(steps).toEqual([
      '在飞书开放平台创建应用并添加机器人能力',
      '开通 im:message 权限并订阅「接收消息 im.message.receive_v1」',
      '填入 AppID 与 AppSecret 完成接入。',
    ])
  })

  it('英文文案同样按序号拆分（序号前是空格）', () => {
    const steps = splitNumberedSteps('1. Create a custom app; 2. Enable the im:message permission, then publish; 3. Fill in the AppID and AppSecret.')
    expect(steps).toHaveLength(3)
    expect(steps[0]).toBe('Create a custom app')
    expect(steps[2]).toBe('Fill in the AppID and AppSecret.')
  })

  it('无序号文案整体返回为单条', () => {
    expect(splitNumberedSteps('直接一段话')).toEqual(['直接一段话'])
    expect(splitNumberedSteps('')).toEqual([])
  })
})
