import { describe, expect, it } from 'vitest'
import { render } from '@testing-library/react'
import { App } from 'antd'
import '@/i18n'
import { FeedbackBridge } from '../FeedbackBridge'
import { getMessageInstance, getNotificationInstance } from '../instances'

describe('FeedbackBridge', () => {
  it('registers antd App message/notification instances', () => {
    render(
      <App>
        <FeedbackBridge />
      </App>,
    )
    expect(getMessageInstance()).toBeDefined()
    expect(getNotificationInstance()).toBeDefined()
  })
})
