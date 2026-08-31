import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import '@/i18n'
import { FormPage } from '../'

describe('FormPage', () => {
  it('renders title and submit/cancel buttons', () => {
    const { container } = render(
      <FormPage title="新建" onFinish={vi.fn()} onCancel={vi.fn()}>
        <div />
      </FormPage>,
    )
    expect(screen.getByText('新建')).toBeInTheDocument()
    expect(screen.getByText('提交')).toBeInTheDocument()
    expect(screen.getByText('取消')).toBeInTheDocument()
    expect(container.querySelector('form')).toBeInTheDocument()
  })

  it('submits values on finish', async () => {
    const onFinish = vi.fn()
    render(
      <FormPage onFinish={onFinish}>
        <div />
      </FormPage>,
    )
    fireEvent.submit(screen.getByText('提交').closest('form') as HTMLFormElement)
    await waitFor(() => expect(onFinish).toHaveBeenCalled())
  })

  it('calls onCancel when the cancel button is clicked', () => {
    const onCancel = vi.fn()
    render(
      <FormPage onFinish={vi.fn()} onCancel={onCancel}>
        <div />
      </FormPage>,
    )
    fireEvent.click(screen.getByText('取消'))
    expect(onCancel).toHaveBeenCalled()
  })
})
