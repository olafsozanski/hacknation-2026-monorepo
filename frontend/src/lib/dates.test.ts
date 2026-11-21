import { describe, expect, it } from 'vitest'
import { formatDate, parseIsoDate, plural, toIsoDate } from './dates'
import { deadlineLabel } from './labels'

describe('dates', () => {
  it('round-trips ISO dates without timezone shifts', () => {
    expect(toIsoDate(parseIsoDate('2026-03-29'))).toBe('2026-03-29')
  })

  it('formats dates the Polish way', () => {
    expect(formatDate('2026-11-05')).toBe('05.11.2026')
    expect(formatDate(null)).toBe('—')
  })

  it('applies Polish plural rules', () => {
    const wnioski = (n: number) => plural(n, 'wniosek', 'wnioski', 'wniosków')
    expect([1, 2, 5, 12, 22, 25].map(wnioski)).toEqual([
      'wniosek',
      'wnioski',
      'wniosków',
      'wniosków',
      'wnioski',
      'wniosków',
    ])
  })
})

describe('deadlineLabel', () => {
  it('describes overdue and upcoming deadlines', () => {
    expect(deadlineLabel('Overdue', -1)).toBe('1 dzień po terminie')
    expect(deadlineLabel('Overdue', -3)).toBe('3 dni po terminie')
    expect(deadlineLabel('DueToday', 0)).toBe('Termin dziś')
    expect(deadlineLabel('OnTrack', 9)).toBe('Za 9 dni')
  })
})
