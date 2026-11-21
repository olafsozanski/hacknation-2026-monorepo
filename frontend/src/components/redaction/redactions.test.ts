import { describe, expect, it } from 'vitest'
import type { Redaction } from '@/api/types'
import { addManual, addPhrase, approveAllSuggested, buildSegments, countByStatus, splitPlaceholders } from './redactions'

const content = 'Pan Jan Kowalski, ul. Długa 5, tel. 600 100 200.'
const jan: Redaction = { start: 4, length: 12, text: 'Jan Kowalski', category: 'PersonName', status: 'Suggested', source: 'Ai' }
const phone: Redaction = { start: 36, length: 11, text: '600 100 200', category: 'Phone', status: 'Approved', source: 'Rule' }

describe('buildSegments', () => {
  it('splits content into text and redaction segments that cover everything', () => {
    const segments = buildSegments(content, [jan, phone])
    expect(segments.map((s) => s.text).join('')).toBe(content)
    expect(segments.filter((s) => s.kind === 'redaction')).toHaveLength(2)
  })

  it('ignores redactions that no longer match the content', () => {
    const stale = { ...jan, text: 'Anna Nowak' }
    expect(buildSegments(content, [stale]).every((s) => s.kind === 'text')).toBe(true)
  })
})

describe('editing redactions', () => {
  it('approves all suggestions', () => {
    expect(countByStatus(approveAllSuggested([jan, phone]))).toEqual({ suggested: 0, approved: 2, rejected: 0 })
  })

  it('adds a trimmed manual redaction and replaces overlapping ones', () => {
    const result = addManual([jan, phone], content, 3, 17)
    const manual = result.find((r) => r.source === 'Manual')!
    expect(manual.text).toBe('Jan Kowalski')
    expect(result).toHaveLength(2)
  })

  it('hides every occurrence of a phrase', () => {
    const text = 'Kowalski napisał. Kowalski prosi.'
    const { redactions, added } = addPhrase([], text, 'Kowalski')
    expect(added).toBe(2)
    expect(redactions.map((r) => r.start)).toEqual([0, 18])
  })
})

describe('splitPlaceholders', () => {
  it('marks fill-in placeholders', () => {
    const pieces = splitPlaceholders('Kwota: [DO UZUPEŁNIENIA: kwota] zł', 10)
    expect(pieces).toEqual([
      { start: 10, text: 'Kwota: ', placeholder: false },
      { start: 17, text: '[DO UZUPEŁNIENIA: kwota]', placeholder: true },
      { start: 41, text: ' zł', placeholder: false },
    ])
  })
})
