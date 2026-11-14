import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import '@/i18n'
import { CronExpressionInput } from '../CronExpressionInput'

const NOW = new Date('2026-10-30T00:00:00Z')

let onChangeSpy: (value: string) => void

/** 受控闭环 Harness：把 onChange 回灌为 value，模拟 Form.Item 行为 */
function Harness({ value }: { value?: string }) {
  const [v, setV] = useState(value)
  return (
    <CronExpressionInput
      value={v}
      onChange={(x) => {
        setV(x)
        onChangeSpy(x)
      }}
      displayOffsetMinutes={480}
      now={NOW}
    />
  )
}

function renderInput(initial?: string) {
  onChangeSpy = vi.fn()
  const utils = render(<Harness value={initial} />)
  return { onChange: onChangeSpy, ...utils }
}

/** 打开 antd Select 下拉并点选选项 */
function selectOption(container: HTMLElement, selectIndex: number, optionText: string) {
  const selectors = container.querySelectorAll('.ant-select-selector')
  fireEvent.mouseDown(selectors[selectIndex])
  const option = [...document.querySelectorAll('.ant-select-item-option .ant-select-item-option-content')].find(
    (el) => el.textContent === optionText,
  )
  expect(option).toBeTruthy()
  fireEvent.click(option!)
}

describe('CronExpressionInput', () => {
  it('常用模式回显：UTC 表达式反解为北京时间并给出描述与下次执行', () => {
    renderInput('0 1 * * *')
    // 0 1 * * * = 北京每天 09:00
    expect(screen.getByText('每天 09:00 执行')).toBeInTheDocument()
    expect(screen.getByText('0 1 * * *')).toBeInTheDocument()
    // NOW 之后北京 09:00 = UTC 01:00，2026-10-30 为周五
    expect(screen.getByText(/下次执行：2026-10-30 09:00（UTC\+8）/)).toBeInTheDocument()
  })

  it('切换频率到每周：按默认周一 9 点生成 UTC 表达式', () => {
    const { onChange, container } = renderInput('0 1 * * *')
    selectOption(container, 0, '每周')
    expect(onChange).toHaveBeenLastCalledWith('0 1 * * 1')
    expect(screen.getByText('每周一 09:00 执行')).toBeInTheDocument()
  })

  it('每月 1 日 + 北京凌晨借位跨月界：日期 1 动态禁用', () => {
    const { container } = renderInput('0 1 * * *')
    selectOption(container, 0, '每月')
    // 把小时改成 3（北京 3 点，西向借位）后，日期 1 的选项应禁用
    const hourInput = container.querySelectorAll('input.ant-input-number-input')[0] as HTMLInputElement
    fireEvent.change(hourInput, { target: { value: '3' } })
    const dateSelectors = container.querySelectorAll('.ant-select-selector')
    fireEvent.mouseDown(dateSelectors[1])
    const day1 = [...document.querySelectorAll('.ant-select-item-option')].find((el) => el.textContent === '1')
    expect(day1?.className).toContain('ant-select-item-option-disabled')
  })

  it('自定义 tab：按字段编辑，合法即外发，非法外发空串并提示', () => {
    const { onChange, container } = renderInput('0 1 * * *')
    fireEvent.click(screen.getByText('自定义'))
    const inputs = container.querySelectorAll('input.ant-input') as NodeListOf<HTMLInputElement>
    expect(inputs.length).toBe(5)
    expect(Array.from(inputs).map((i) => i.value)).toEqual(['0', '1', '*', '*', '*'])

    fireEvent.change(inputs[0], { target: { value: '*/5' } })
    expect(onChange).toHaveBeenLastCalledWith('*/5 1 * * *')

    fireEvent.change(inputs[1], { target: { value: '25' } })
    expect(onChange).toHaveBeenLastCalledWith('')
    expect(screen.getByText('表达式不合法，仅支持数字与 , - / * 的组合')).toBeInTheDocument()
  })

  it('初始值非常用形态时直接落在自定义 tab', () => {
    renderInput('5-10 * * * *')
    expect(screen.getByText('按自定义表达式执行')).toBeInTheDocument()
    expect(screen.getByText('5-10 * * * *')).toBeInTheDocument()
  })

  it('未传初始值时按默认常用模式（每天 9 点）展示', () => {
    renderInput()
    expect(screen.getByText('每天 09:00 执行')).toBeInTheDocument()
  })
})
