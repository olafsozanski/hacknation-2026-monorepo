import type { Redaction, RedactionStatus } from '@/api/types'

export type Segment =
  | { kind: 'text'; start: number; text: string }
  | { kind: 'redaction'; start: number; text: string; index: number; redaction: Redaction }

const trimmable = /[\s,.;:]/

export const placeholderPattern = /\[DO UZUPEŁNIENIA[^\]]*\]/g

export function isValid(redaction: Redaction, content: string) {
  return (
    redaction.length > 0 &&
    redaction.start >= 0 &&
    redaction.start + redaction.length <= content.length &&
    content.slice(redaction.start, redaction.start + redaction.length) === redaction.text
  )
}

export function sortRedactions(redactions: Redaction[]) {
  return [...redactions].sort((a, b) => a.start - b.start || b.length - a.length)
}

export function buildSegments(content: string, redactions: Redaction[]): Segment[] {
  const segments: Segment[] = []
  let cursor = 0
  redactions.forEach((redaction, index) => {
    if (!isValid(redaction, content) || redaction.start < cursor) return
    if (redaction.start > cursor) segments.push({ kind: 'text', start: cursor, text: content.slice(cursor, redaction.start) })
    segments.push({ kind: 'redaction', start: redaction.start, text: redaction.text, index, redaction })
    cursor = redaction.start + redaction.length
  })
  if (cursor < content.length) segments.push({ kind: 'text', start: cursor, text: content.slice(cursor) })
  return segments
}

export function splitPlaceholders(text: string, start: number) {
  const pieces: { start: number; text: string; placeholder: boolean }[] = []
  let cursor = 0
  for (const match of text.matchAll(placeholderPattern)) {
    const index = match.index ?? 0
    if (index > cursor) pieces.push({ start: start + cursor, text: text.slice(cursor, index), placeholder: false })
    pieces.push({ start: start + index, text: match[0], placeholder: true })
    cursor = index + match[0].length
  }
  if (cursor < text.length) pieces.push({ start: start + cursor, text: text.slice(cursor), placeholder: false })
  return pieces
}

export function withStatus(redactions: Redaction[], index: number, status: RedactionStatus) {
  return redactions.map((r, i) => (i === index ? { ...r, status } : r))
}

export function approveAllSuggested(redactions: Redaction[]) {
  return redactions.map((r) => (r.status === 'Suggested' ? { ...r, status: 'Approved' as const } : r))
}

export function addManual(redactions: Redaction[], content: string, start: number, end: number) {
  let from = Math.max(0, Math.min(start, end))
  let to = Math.min(content.length, Math.max(start, end))
  while (from < to && trimmable.test(content[from])) from++
  while (to > from && trimmable.test(content[to - 1])) to--
  if (to <= from) return redactions
  const kept = redactions.filter((r) => r.start + r.length <= from || r.start >= to)
  const added: Redaction = {
    start: from,
    length: to - from,
    text: content.slice(from, to),
    category: 'Other',
    status: 'Approved',
    source: 'Manual',
  }
  return sortRedactions([...kept, added])
}

function escapeRegExp(text: string) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

export function addPhrase(redactions: Redaction[], content: string, phrase: string) {
  const needle = phrase.trim()
  if (!needle) return { redactions, added: 0 }
  let result = redactions
  let added = 0
  for (const match of content.matchAll(new RegExp(escapeRegExp(needle), 'giu'))) {
    const from = match.index
    const to = from + match[0].length
    if (!result.some((r) => r.start < to && from < r.start + r.length && r.status === 'Approved')) {
      result = addManual(result, content, from, to)
      added++
    }
  }
  return { redactions: result, added }
}

export function countByStatus(redactions: Redaction[]) {
  return {
    suggested: redactions.filter((r) => r.status === 'Suggested').length,
    approved: redactions.filter((r) => r.status === 'Approved').length,
    rejected: redactions.filter((r) => r.status === 'Rejected').length,
  }
}

export function offsetWithin(container: HTMLElement, node: Node, offset: number): number | null {
  const element = node.nodeType === Node.TEXT_NODE ? node.parentElement : (node as HTMLElement)
  const segment = element?.closest<HTMLElement>('[data-offset]')
  if (!segment || !container.contains(segment)) return null
  const base = Number(segment.dataset.offset)
  if (node.nodeType !== Node.TEXT_NODE) return base + (offset > 0 ? (segment.textContent?.length ?? 0) : 0)
  return base + offset
}
