import type { ScreenContext, ScreenFieldError } from '@/api/types'

export const assistantUiAttribute = 'data-assistant-ui'
export const assistantUiProps = { [assistantUiAttribute]: '' }

const limits = { path: 300, text: 300, alert: 500, fieldErrors: 20, alerts: 10 }
const inlineElements = new Set(['a', 'abbr', 'b', 'code', 'em', 'i', 'kbd', 'label', 'mark', 's', 'small', 'span', 'strong', 'sub', 'sup', 'time', 'u'])
const skippedElements = new Set(['script', 'style', 'template', 'noscript', 'svg'])
const fieldRoot = '[data-scope="field"][data-part="root"]'
const missingMessage = 'Pole oznaczone jako niepoprawne.'

export function clip(value: string, max: number): string {
  const text = value.replace(/\s+/g, ' ').trim()
  if (text.length <= max) return text
  const cut = text.slice(0, max)
  return /[\uD800-\uDBFF]$/.test(cut) ? cut.slice(0, -1) : cut
}

function optional(value: string, max = limits.text): string | null {
  return clip(value, max) || null
}

function collectText(node: Node): string {
  if (node.nodeType === Node.TEXT_NODE) return node.nodeValue ?? ''
  if (node.nodeType !== Node.ELEMENT_NODE) return ''
  const element = node as Element
  const name = element.localName
  if (skippedElements.has(name) || element.getAttribute('aria-hidden') === 'true' || element.hasAttribute('hidden')) return ''
  if (name === 'br') return ' '
  const inner = Array.from(element.childNodes, collectText).join('')
  return inlineElements.has(name) ? inner : ` ${inner} `
}

export function readableText(node: Node): string {
  return collectText(node).replace(/\s+/g, ' ').trim()
}

function isVisuallyHidden(element: Element) {
  const style = element.ownerDocument.defaultView?.getComputedStyle(element)
  return style?.clip === 'rect(0px, 0px, 0px, 0px)' || style?.clipPath === 'inset(50%)'
}

function isVisible(element: Element) {
  if (element.closest(`[${assistantUiAttribute}], [hidden], [inert]`) || isVisuallyHidden(element)) return false
  return typeof element.checkVisibility !== 'function' || element.checkVisibility()
}

function textOfIds(ids: string | null, doc: Document): string {
  if (!ids) return ''
  return ids
    .split(/\s+/)
    .map((id) => doc.getElementById(id))
    .filter((element): element is HTMLElement => element !== null)
    .map(readableText)
    .join(' ')
}

function fieldLabel(element: Element, doc: Document): string {
  const labelledBy = textOfIds(element.getAttribute('aria-labelledby'), doc)
  if (labelledBy) return labelledBy
  const labels = (element as HTMLInputElement).labels
  if (labels && labels.length > 0) return Array.from(labels, readableText).join(' ')
  const ariaLabel = element.getAttribute('aria-label')
  if (ariaLabel) return ariaLabel
  const label = element.closest(fieldRoot)?.querySelector('[data-scope="field"][data-part="label"]')
  return label ? readableText(label) : ''
}

function fieldMessage(element: Element, doc: Document): string {
  const explicit = textOfIds(element.getAttribute('aria-errormessage'), doc)
  if (explicit) return explicit
  const describedBy = (element.getAttribute('aria-describedby') ?? '')
    .split(/\s+/)
    .map((id) => (id ? doc.getElementById(id) : null))
    .find((candidate) => candidate?.matches('[data-part="error-text"]'))
  const errorText = describedBy ?? element.closest(fieldRoot)?.querySelector('[data-part="error-text"]')
  return errorText ? readableText(errorText) : ''
}

function collectFieldErrors(doc: Document): ScreenFieldError[] {
  const result: ScreenFieldError[] = []
  const seen = new Set<string>()
  for (const element of doc.querySelectorAll('[aria-invalid="true"]')) {
    if (result.length >= limits.fieldErrors) break
    if (!isVisible(element)) continue
    const label = clip(fieldLabel(element, doc), limits.text)
    if (!label) continue
    const message = clip(fieldMessage(element, doc), limits.text) || missingMessage
    const key = `${label}\n${message}`
    if (seen.has(key)) continue
    seen.add(key)
    result.push({ label, message })
  }
  return result
}

function collectAlerts(doc: Document): string[] {
  const result: string[] = []
  const selector = '[role="alert"], [role="status"]'
  for (const element of doc.querySelectorAll(selector)) {
    if (result.length >= limits.alerts) break
    if (!isVisible(element) || element.parentElement?.closest(selector)) continue
    const text = clip(readableText(element), limits.alert)
    if (text && !result.includes(text)) result.push(text)
  }
  return result
}

function openDialogTitle(doc: Document): string {
  let title = ''
  for (const dialog of doc.querySelectorAll('[role="dialog"], [role="alertdialog"]')) {
    if (!isVisible(dialog) || dialog.getAttribute('data-state') === 'closed') continue
    const text = textOfIds(dialog.getAttribute('aria-labelledby'), doc) || dialog.getAttribute('aria-label') || ''
    if (text.trim()) title = text
  }
  return title
}

function pageHeading(doc: Document): string {
  const heading = Array.from(doc.querySelectorAll('#main-content h1')).find(isVisible)
  return heading ? readableText(heading) : ''
}

export function hasInvalidFields(doc: Document = document): boolean {
  return collectFieldErrors(doc).length > 0
}

export function pathOnlyContext(path: string): ScreenContext {
  return { path: clip(path, limits.path), title: null, heading: null, dialog: null, fieldErrors: [], alerts: [] }
}

export function collectScreenContext(path: string, doc: Document = document): ScreenContext {
  return {
    path: clip(path, limits.path),
    title: optional(doc.title),
    heading: optional(pageHeading(doc)),
    dialog: optional(openDialogTitle(doc)),
    fieldErrors: collectFieldErrors(doc),
    alerts: collectAlerts(doc),
  }
}
