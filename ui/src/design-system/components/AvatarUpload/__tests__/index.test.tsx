import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import '@/i18n'
import { AvatarUpload } from '../'

describe('AvatarUpload', () => {
  it('渲染头像兜底内容，默认无遮罩', () => {
    render(<AvatarUpload fallback="A" onSelect={vi.fn()} />)
    expect(screen.getByText('A')).toBeInTheDocument()
    expect(screen.queryByText('点击上传头像')).not.toBeInTheDocument()
  })

  it('悬停显示上传提示，移开消失（@manual 大小小于 80 时仅显示图标）', () => {
    const onSelect = vi.fn()
    render(<AvatarUpload fallback="A" size={96} onSelect={onSelect} />)
    const trigger = screen.getByTitle('点击上传头像')
    fireEvent.mouseEnter(trigger)
    expect(screen.getByText('点击上传头像')).toBeInTheDocument()
    fireEvent.mouseLeave(trigger)
    expect(screen.queryByText('点击上传头像')).not.toBeInTheDocument()
  })

  it('上传中显示加载遮罩且隐藏悬停文案', () => {
    render(<AvatarUpload fallback="A" uploading onSelect={vi.fn()} />)
    expect(document.querySelector('.ant-spin')).toBeInTheDocument()
    expect(screen.queryByText('点击上传头像')).not.toBeInTheDocument()
  })

  it('禁用时不显示悬停遮罩', () => {
    render(<AvatarUpload fallback="A" disabled onSelect={vi.fn()} />)
    const trigger = screen.getByTitle('点击上传头像')
    fireEvent.mouseEnter(trigger)
    expect(screen.queryByText('点击上传头像')).not.toBeInTheDocument()
  })
})
