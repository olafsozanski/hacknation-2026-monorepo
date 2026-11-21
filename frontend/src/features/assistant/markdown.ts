export type Inline =
  | { kind: 'text'; text: string }
  | { kind: 'strong'; children: Inline[] }
  | { kind: 'code'; text: string }
  | { kind: 'link'; text: string; href: string }

export type ParagraphBlock = { kind: 'paragraph'; lines: Inline[][] }
export type ListBlock = { kind: 'list'; ordered: boolean; start: number; items: Inline[][] }
export type Block = ParagraphBlock | ListBlock

const bulletPattern = /^\s{0,3}[-*•]\s+(.*)$/
const numberPattern = /^\s{0,3}(\d{1,3})[.)]\s+(.*)$/
const headingPattern = /^\s{0,3}#{1,6}\s+(.*)$/
const inlinePattern = /\*\*(.+?)\*\*|`([^`]+)`|\[([^\]]+)\]\(([^)\s]+)\)/g
const appOrigin = 'https://app.invalid'

export function isAppPath(href: string): boolean {
  if (!/^\/(?![/\\])[^\s\\]*$/.test(href)) return false
  try {
    return new URL(href, appOrigin).origin === appOrigin
  } catch {
    return false
  }
}

export function parseInline(text: string): Inline[] {
  const result: Inline[] = []
  let last = 0
  for (const match of text.matchAll(inlinePattern)) {
    const [whole, strong, code, linkText, href] = match
    if (match.index > last) result.push({ kind: 'text', text: text.slice(last, match.index) })
    if (strong !== undefined) result.push({ kind: 'strong', children: parseInline(strong) })
    else if (code !== undefined) result.push({ kind: 'code', text: code })
    else if (isAppPath(href)) result.push({ kind: 'link', text: linkText, href })
    else result.push({ kind: 'text', text: `${linkText} (${href})` })
    last = match.index + whole.length
  }
  if (last < text.length) result.push({ kind: 'text', text: text.slice(last) })
  return result
}

function listItem(line: string) {
  const bullet = bulletPattern.exec(line)
  if (bullet) return { ordered: false, number: 1, content: bullet[1] }
  const numbered = numberPattern.exec(line)
  if (numbered) return { ordered: true, number: Number(numbered[1]), content: numbered[2] }
  return null
}

function lineContent(line: string): Inline[] {
  const heading = headingPattern.exec(line)
  return heading ? [{ kind: 'strong', children: parseInline(heading[1].trim()) }] : parseInline(line.trim())
}

export function parseMarkdown(source: string): Block[] {
  const blocks: Block[] = []
  let current: Block | null = null
  for (const line of source.replace(/\r\n?/g, '\n').split('\n')) {
    if (line.trim() === '') {
      current = null
      continue
    }
    const item = listItem(line)
    if (item) {
      if (current?.kind !== 'list' || current.ordered !== item.ordered) {
        current = { kind: 'list', ordered: item.ordered, start: item.number, items: [] }
        blocks.push(current)
      }
      current.items.push(parseInline(item.content.trim()))
      continue
    }
    if (current?.kind === 'list' && /^\s/.test(line)) {
      const last = current.items.length - 1
      current.items[last] = [...current.items[last], { kind: 'text', text: ' ' }, ...parseInline(line.trim())]
      continue
    }
    if (current?.kind !== 'paragraph') {
      current = { kind: 'paragraph', lines: [] }
      blocks.push(current)
    }
    current.lines.push(lineContent(line))
  }
  return blocks
}

function inlineText(nodes: Inline[]): string {
  return nodes.map((node) => (node.kind === 'strong' ? inlineText(node.children) : node.text)).join('')
}

export function toPlainText(source: string): string {
  return parseMarkdown(source)
    .flatMap((block) => (block.kind === 'paragraph' ? block.lines : block.items))
    .map(inlineText)
    .join(' ')
}
