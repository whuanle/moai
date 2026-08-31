import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { StatCard } from '../'

describe('StatCard', () => {
  it('renders title and value', () => {
    render(<StatCard title="用户数" value={128} />)
    expect(screen.getByText('用户数')).toBeInTheDocument()
    expect(screen.getByText('128')).toBeInTheDocument()
  })
})
