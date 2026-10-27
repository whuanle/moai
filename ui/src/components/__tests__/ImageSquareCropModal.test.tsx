import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import '@/i18n'
import { clampCropOffset, ImageSquareCropModal } from '../ImageSquareCropModal'

const VIEW = 280

describe('clampCropOffset', () => {
  const LANDSCAPE = { w: 1000, h: 600 } // contain=0.28（高 168 留白 112），cover≈0.4667（高恰好铺满）

  it('宽图在完整显示缩放下：上下可在留白区间拖动，左右恰好贴合锁定', () => {
    const scale = 0.28
    const clamped = clampCropOffset(VIEW, LANDSCAPE.w, LANDSCAPE.h, scale, -40, 999)
    expect(clamped.x).toBe(0)
    expect(clamped.y).toBe(VIEW - LANDSCAPE.h * scale)
    expect(clampCropOffset(VIEW, LANDSCAPE.w, LANDSCAPE.h, scale, 0, -50).y).toBe(0)
  })

  it('放大到铺满后恢复 cover 约束：上下锁定为 0，左右可在溢出区间拖动', () => {
    const scale = VIEW / LANDSCAPE.h
    const clamped = clampCropOffset(VIEW, LANDSCAPE.w, LANDSCAPE.h, scale, -9999, -50)
    expect(clamped.y).toBe(0)
    expect(clamped.x).toBe(VIEW - LANDSCAPE.w * scale)
  })

  it('放大超过视口后两个方向均可拖动', () => {
    const scale = (VIEW / LANDSCAPE.h) * 2
    const clamped = clampCropOffset(VIEW, LANDSCAPE.w, LANDSCAPE.h, scale, -9999, -9999)
    expect(clamped.x).toBe(VIEW - LANDSCAPE.w * scale)
    expect(clamped.y).toBe(VIEW - LANDSCAPE.h * scale)
    expect(clampCropOffset(VIEW, LANDSCAPE.w, LANDSCAPE.h, scale, 9999, 9999)).toEqual({ x: 0, y: 0 })
  })
})

describe('ImageSquareCropModal', () => {
  it('open 时渲染裁剪弹窗标题与方形裁剪提示', () => {
    render(
      <ImageSquareCropModal open src="blob:mock" fileName="logo.png" onConfirm={vi.fn()} onCancel={vi.fn()} />,
    )
    expect(screen.getByText('裁剪图片')).toBeInTheDocument()
    expect(screen.getByText(/按正方形裁剪/)).toBeInTheDocument()
  })

  it('点击取消触发 onCancel 且不产出文件', () => {
    const onConfirm = vi.fn()
    const onCancel = vi.fn()
    render(<ImageSquareCropModal open src="blob:mock" onConfirm={onConfirm} onCancel={onCancel} />)
    fireEvent.click(screen.getByRole('button', { name: /取\s*消/ }))
    expect(onCancel).toHaveBeenCalledTimes(1)
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('关闭（open=false）时不渲染弹窗内容', () => {
    render(
      <ImageSquareCropModal open={false} src="blob:mock" fileName="logo.png" onConfirm={vi.fn()} onCancel={vi.fn()} />,
    )
    expect(screen.queryByText('裁剪图片')).not.toBeInTheDocument()
  })
})
