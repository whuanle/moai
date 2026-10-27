import { useCallback, useEffect, useRef, useState } from 'react'
import { Modal, Slider, theme } from 'antd'
import { useTranslation } from 'react-i18next'

/** 裁剪视口边长（px），视口即正方形裁剪框. */
const VIEW_SIZE = 280
/** 导出图片的最大边长（px），按源图分辨率导出时向下封顶. */
const MAX_OUTPUT = 512
/** 最大缩放（相对"铺满视口"的比例），留出放大裁剪的余量. */
const MAX_ZOOM_RATIO = 5

export interface ImageSquareCropModalProps {
  open: boolean
  /** 待裁剪的本地图片地址（调用方负责创建与释放 object URL）. */
  src?: string | null
  /** 裁剪输出文件名（不含扩展名时自动补 .png）. */
  fileName?: string
  /** 确认裁剪，返回裁剪后的正方形 PNG 文件. */
  onConfirm: (file: File) => void
  onCancel: () => void
}

interface Offset {
  x: number
  y: number
}

/**
 * 约束图片在视口内的偏移：任一方向有余量即可拖动——
 * 图片该边超过视口时约束为 [视口-边长, 0]（cover 式贴边）；
 * 不足视口时（缩小到完整可见）允许在 0 与留白区间内自由移动，留白以透明保留。
 */
export function clampCropOffset(
  viewSize: number,
  imageWidth: number,
  imageHeight: number,
  scale: number,
  x: number,
  y: number,
): Offset {
  const clampAxis = (size: number, value: number) => {
    const lo = Math.min(0, viewSize - size)
    const hi = Math.max(0, viewSize - size)
    return Math.min(hi, Math.max(lo, value))
  }
  return {
    x: clampAxis(imageWidth * scale, x),
    y: clampAxis(imageHeight * scale, y),
  }
}

/**
 * 正方形图片裁剪弹窗：图片初始完整显示并居中（宽图/长图立即可以上下或左右拖动），
 * 支持拖动定位、滑块/滚轮缩放（放大可铺满方形视口做紧致裁剪），确认后按视口内容
 * 经 Canvas 裁剪导出正方形 PNG，未铺满处的留白保留为透明。
 */
export function ImageSquareCropModal({ open, src, fileName, onConfirm, onCancel }: ImageSquareCropModalProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [ready, setReady] = useState(false)
  const [natural, setNatural] = useState({ width: 0, height: 0 })
  const [scale, setScale] = useState(1)
  const [minScale, setMinScale] = useState(1)
  const [maxScale, setMaxScale] = useState(1)
  const [offset, setOffset] = useState<Offset>({ x: 0, y: 0 })
  const [dragging, setDragging] = useState(false)

  const imageRef = useRef<HTMLImageElement | null>(null)
  const viewportRef = useRef<HTMLDivElement | null>(null)
  const scaleRef = useRef(1)
  const minScaleRef = useRef(1)
  const maxScaleRef = useRef(1)
  const offsetRef = useRef<Offset>({ x: 0, y: 0 })
  const dragRef = useRef<{ pointerId: number; startX: number; startY: number; baseX: number; baseY: number } | null>(null)

  const clampOffset = useCallback((nextScale: number, x: number, y: number): Offset => {
    const image = imageRef.current
    if (!image) return { x: 0, y: 0 }
    return clampCropOffset(VIEW_SIZE, image.naturalWidth, image.naturalHeight, nextScale, x, y)
  }, [])

  const applyOffset = useCallback((next: Offset) => {
    offsetRef.current = next
    setOffset(next)
  }, [])

  /** 以 anchor（视口坐标）为锚点缩放到目标倍率，锚点对应的图像内容保持不动. */
  const applyZoom = useCallback(
    (target: number, anchor: Offset) => {
      const prev = scaleRef.current
      const clamped = Math.min(maxScaleRef.current, Math.max(minScaleRef.current, target))
      if (!Number.isFinite(clamped) || clamped === prev) return
      const ratio = clamped / prev
      scaleRef.current = clamped
      setScale(clamped)
      const o = offsetRef.current
      applyOffset(clampOffset(clamped, anchor.x - (anchor.x - o.x) * ratio, anchor.y - (anchor.y - o.y) * ratio))
    },
    [applyOffset, clampOffset],
  )

  // 打开时加载图片：初始完整显示（contain）并居中，任一非贴齐方向立即可拖动
  useEffect(() => {
    if (!open || !src) return
    setReady(false)
    let revoked = false
    const image = new Image()
    image.onload = () => {
      if (revoked) return
      imageRef.current = image
      const contain = Math.min(VIEW_SIZE / image.naturalWidth, VIEW_SIZE / image.naturalHeight)
      const cover = Math.max(VIEW_SIZE / image.naturalWidth, VIEW_SIZE / image.naturalHeight)
      minScaleRef.current = contain
      maxScaleRef.current = cover * MAX_ZOOM_RATIO
      scaleRef.current = contain
      setNatural({ width: image.naturalWidth, height: image.naturalHeight })
      setMinScale(contain)
      setMaxScale(maxScaleRef.current)
      const initial = {
        x: (VIEW_SIZE - image.naturalWidth * contain) / 2,
        y: (VIEW_SIZE - image.naturalHeight * contain) / 2,
      }
      offsetRef.current = initial
      setOffset(initial)
      setScale(contain)
      setReady(true)
    }
    image.src = src
    return () => {
      revoked = true
      image.onload = null
    }
  }, [open, src])

  // 滚轮缩放以鼠标位置为锚点（原生监听，避免 React 合成事件 passive 限制）
  useEffect(() => {
    const el = viewportRef.current
    if (!el || !open || !ready) return
    const onWheel = (e: WheelEvent) => {
      e.preventDefault()
      const rect = el.getBoundingClientRect()
      applyZoom(scaleRef.current * (e.deltaY > 0 ? 0.9 : 1.1), {
        x: e.clientX - rect.left,
        y: e.clientY - rect.top,
      })
    }
    el.addEventListener('wheel', onWheel, { passive: false })
    return () => el.removeEventListener('wheel', onWheel)
  }, [open, ready, applyZoom])

  const handlePointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    viewportRef.current?.setPointerCapture(e.pointerId)
    dragRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      baseX: offsetRef.current.x,
      baseY: offsetRef.current.y,
    }
    setDragging(true)
  }

  const handlePointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current
    if (!drag || drag.pointerId !== e.pointerId) return
    applyOffset(clampOffset(scaleRef.current, drag.baseX + (e.clientX - drag.startX), drag.baseY + (e.clientY - drag.startY)))
  }

  const endDrag = (e: React.PointerEvent<HTMLDivElement>) => {
    if (dragRef.current?.pointerId === e.pointerId) {
      dragRef.current = null
      setDragging(false)
    }
  }

  const handleOk = () => {
    const image = imageRef.current
    if (!image) return
    const sourceSide = VIEW_SIZE / scaleRef.current
    const sx = -offsetRef.current.x / scaleRef.current
    const sy = -offsetRef.current.y / scaleRef.current
    const outSide = Math.min(MAX_OUTPUT, Math.max(1, Math.round(sourceSide)))
    const canvas = document.createElement('canvas')
    canvas.width = outSide
    canvas.height = outSide
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.drawImage(image, sx, sy, sourceSide, sourceSide, 0, 0, outSide, outSide)
    canvas.toBlob((blob) => {
      if (!blob) return
      const base = (fileName ?? 'icon').replace(/\.[^.]+$/, '')
      onConfirm(new File([blob], `${base}.png`, { type: 'image/png' }))
    }, 'image/png')
  }

  return (
    <Modal
      open={open}
      title={t('common.imageCropTitle')}
      okText={t('common.ok')}
      cancelText={t('common.cancel')}
      onOk={handleOk}
      onCancel={onCancel}
      maskClosable={false}
      destroyOnClose
      width={344}
    >
      <div
        ref={viewportRef}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        style={{
          position: 'relative',
          width: VIEW_SIZE,
          height: VIEW_SIZE,
          margin: '0 auto',
          overflow: 'hidden',
          borderRadius: token.borderRadius,
          border: `1px solid ${token.colorBorderSecondary}`,
          background: token.colorFillTertiary,
          cursor: dragging ? 'grabbing' : 'grab',
          touchAction: 'none',
          userSelect: 'none',
        }}
      >
        {ready && (
          <img
            src={src ?? undefined}
            alt=""
            draggable={false}
            style={{
              position: 'absolute',
              left: 0,
              top: 0,
              width: natural.width,
              height: natural.height,
              transform: `translate(${offset.x}px, ${offset.y}px) scale(${scale})`,
              transformOrigin: '0 0',
              pointerEvents: 'none',
            }}
          />
        )}
        {/* 十字辅助线，提示正方形裁剪范围 */}
        <div
          style={{
            position: 'absolute',
            inset: 0,
            pointerEvents: 'none',
            background:
              'linear-gradient(to right, transparent calc(50% - 0.5px), rgba(255,255,255,0.6) 50%, transparent calc(50% + 0.5px)), linear-gradient(to bottom, transparent calc(50% - 0.5px), rgba(255,255,255,0.6) 50%, transparent calc(50% + 0.5px))',
          }}
        />
      </div>
      <Slider
        min={minScale}
        max={maxScale}
        step={(maxScale - minScale) / 100 || 0.01}
        value={scale}
        tooltip={{ formatter: (v) => `${Math.round((v ?? 1) * 100)}%` }}
        onChange={(v) => applyZoom(v, { x: VIEW_SIZE / 2, y: VIEW_SIZE / 2 })}
        style={{ width: VIEW_SIZE, margin: '12px auto 0' }}
        disabled={!ready}
      />
      <div style={{ marginTop: 8, fontSize: 12, color: token.colorTextSecondary, textAlign: 'center' }}>
        {t('common.imageCropHint')}
      </div>
    </Modal>
  )
}
