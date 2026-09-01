import { describe, expect, it, vi } from 'vitest'
import '@/i18n'
import { createFeedback } from '../feedback'
import { getHttpStatus, isNetworkError, extractErrorMessage, resolveErrorMessage, parseApiErrorResponse } from '../error'
import type { MessageInstance } from 'antd/es/message/interface'
import type { NotificationInstance } from 'antd/es/notification/interface'

function makeConnector() {
  const message: Record<string, ReturnType<typeof vi.fn>> = {
    open: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
    loading: vi.fn(),
    destroy: vi.fn(),
  }

  const notification: Record<string, ReturnType<typeof vi.fn>> = {
    open: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
    destroy: vi.fn(),
  }

  const connector = {
    message: message as unknown as MessageInstance,
    notification: notification as unknown as NotificationInstance,
  }

  return { connector, message, notification }
}

describe('feedback error classification', () => {
  it('extracts http status from various shapes', () => {
    expect(getHttpStatus({ status: 400 })).toBe(400)
    expect(getHttpStatus({ responseStatusCode: 500 })).toBe(500)
    expect(getHttpStatus(new Response(null, { status: 503 }))).toBe(503)
    expect(getHttpStatus(new Error('boom'))).toBeUndefined()
  })

  it('detects network errors', () => {
    expect(isNetworkError(new TypeError('Failed to fetch'))).toBe(true)
    expect(isNetworkError({ status: 400 })).toBe(false)
    expect(isNetworkError(new Error('the server returned an unexpected status code 500'))).toBe(false)
  })

  it('extracts server message while ignoring kiota generic text', () => {
    expect(extractErrorMessage({ message: '用户名或密码错误' })).toBe('用户名或密码错误')
    expect(extractErrorMessage(new Error('the server returned an unexpected status code 500'))).toBeUndefined()
    expect(extractErrorMessage(new TypeError('Failed to fetch'))).toBeUndefined()
  })

  it('resolves business detail with priority over message', () => {
    expect(resolveErrorMessage({ detail: '用户名或密码错误', message: 'generic' })).toBe('用户名或密码错误')
    expect(resolveErrorMessage({ message: '后端返回的业务信息' })).toBe('后端返回的业务信息')
    expect(resolveErrorMessage(new TypeError('Failed to fetch'))).toBeUndefined()
  })

  it('resolves the first field-level validation error', () => {
    const err = { errors: [{ name: 'userName', errors: ['用户名不能为空'] }, { name: 'email', errors: ['格式错误'] }] }
    expect(resolveErrorMessage(err)).toBe('用户名不能为空')
  })
})

describe('parseApiErrorResponse', () => {
  it('normalizes a json error body into an error with status and detail', async () => {
    const response = new Response(JSON.stringify({ detail: '手机号已被注册', code: 'xx' }), {
      status: 400,
      headers: { 'content-type': 'application/json' },
    })
    const error = await parseApiErrorResponse(response)
    expect(error.status).toBe(400)
    expect(error.detail).toBe('手机号已被注册')
    expect(error.message).toBe('手机号已被注册')
  })

  it('keeps field errors and falls back to message', async () => {
    const response = new Response(
      JSON.stringify({ errors: [{ name: 'userName', errors: ['用户名不能为空'] }] }),
      { status: 400, headers: { 'content-type': 'application/json' } },
    )
    const error = await parseApiErrorResponse(response)
    expect(error.status).toBe(400)
    expect(error.errors).toHaveLength(1)
    expect(resolveErrorMessage(error)).toBe('用户名不能为空')
  })

  it('handles non-json / empty bodies', async () => {
    const response = new Response(null, { status: 500 })
    const error = await parseApiErrorResponse(response)
    expect(error.status).toBe(500)
    expect(resolveErrorMessage(error)).toBeUndefined()
  })
})

describe('feedback routing', () => {
  it('routes business 4xx to message.error', () => {
    const { connector, message, notification } = makeConnector()
    const feedback = createFeedback(connector)
    feedback.handleError({ status: 400, message: '用户名或密码错误' })
    expect(message.error).toHaveBeenCalledWith('用户名或密码错误')
    expect(notification.error).not.toHaveBeenCalled()
  })

  it('routes server 5xx to notification.error', () => {
    const { connector, message, notification } = makeConnector()
    const feedback = createFeedback(connector)
    feedback.handleError({ status: 500 })
    expect(notification.error).toHaveBeenCalled()
    expect(notification.error.mock.calls[0][0]).toMatchObject({ message: expect.any(String) })
    expect(message.error).not.toHaveBeenCalled()
  })

  it('routes network errors to notification.error', () => {
    const { connector, notification } = makeConnector()
    const feedback = createFeedback(connector)
    feedback.handleError(new TypeError('Failed to fetch'))
    expect(notification.error).toHaveBeenCalled()
    expect(notification.error.mock.calls[0][0]).toMatchObject({ message: expect.any(String) })
  })

  it('routes unknown errors to notification.error', () => {
    const { connector, notification } = makeConnector()
    const feedback = createFeedback(connector)
    feedback.handleError(new Error('unexpected'))
    expect(notification.error).toHaveBeenCalled()
  })

  it('routes success to message channel', () => {
    const { connector, message } = makeConnector()
    const feedback = createFeedback(connector)
    feedback.success('已保存')
    expect(message.success).toHaveBeenCalledWith('已保存', undefined, undefined)
  })

  it('routes system notify to notification channel', () => {
    const { connector, notification } = makeConnector()
    const feedback = createFeedback(connector)
    feedback.notifyWarning('磁盘即将耗尽', { description: '请及时清理' })
    expect(notification.warning).toHaveBeenCalledWith({ message: '磁盘即将耗尽', description: '请及时清理' })
  })
})
