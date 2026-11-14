import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { BackLink } from '../'

describe('BackLink', () => {
  it('渲染箭头图标与返回文案，链接指向目标路由', () => {
    render(
      <MemoryRouter>
        <BackLink to="/team/1/prompts">返回提示词</BackLink>
      </MemoryRouter>,
    )
    const link = screen.getByRole('link', { name: /返回提示词/ })
    expect(link).toHaveAttribute('href', '/team/1/prompts')
    expect(link.querySelector('.anticon-arrow-left')).not.toBeNull()
  })
})
