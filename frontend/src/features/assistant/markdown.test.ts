import { describe, expect, it } from 'vitest'
import { isAppPath, parseInline, parseMarkdown, toPlainText } from './markdown'

describe('isAppPath', () => {
  it('accepts app-relative paths', () => {
    expect(['/', '/wnioski/nowy', '/wnioski/1b2c/odpowiedz', '/archiwum?q=drzewa#wyniki'].every(isAppPath)).toBe(true)
  })

  it('rejects external, protocol-relative and script links', () => {
    const unsafe = [
      'https://example.com',
      '//example.com',
      '/\\example.com',
      '\\\\example.com',
      'javascript:alert(1)',
      'wnioski/nowy',
      '/wnioski nowy',
      '',
    ]
    expect(unsafe.filter(isAppPath)).toEqual([])
  })
})

describe('parseInline', () => {
  it('parses bold, code and app links', () => {
    expect(parseInline('Kliknij **Zapisz** w `Rejestrze` lub [nowy wniosek](/wnioski/nowy).')).toEqual([
      { kind: 'text', text: 'Kliknij ' },
      { kind: 'strong', children: [{ kind: 'text', text: 'Zapisz' }] },
      { kind: 'text', text: ' w ' },
      { kind: 'code', text: 'Rejestrze' },
      { kind: 'text', text: ' lub ' },
      { kind: 'link', text: 'nowy wniosek', href: '/wnioski/nowy' },
      { kind: 'text', text: '.' },
    ])
  })

  it('keeps links to other sites and protocol-relative links as plain text', () => {
    expect(parseInline('[BIP](https://bip.example.pl) [x](//evil.example)')).toEqual([
      { kind: 'text', text: 'BIP (https://bip.example.pl)' },
      { kind: 'text', text: ' ' },
      { kind: 'text', text: 'x (//evil.example)' },
    ])
  })

  it('treats HTML as text and leaves unclosed markers alone', () => {
    expect(parseInline('<img src=x onerror=alert(1)> **niedokończone')).toEqual([
      { kind: 'text', text: '<img src=x onerror=alert(1)> **niedokończone' },
    ])
  })
})

describe('parseMarkdown', () => {
  it('splits paragraphs on blank lines and keeps single line breaks', () => {
    expect(parseMarkdown('Pierwszy\ndrugi wiersz\n\nNastępny akapit')).toEqual([
      { kind: 'paragraph', lines: [[{ kind: 'text', text: 'Pierwszy' }], [{ kind: 'text', text: 'drugi wiersz' }]] },
      { kind: 'paragraph', lines: [[{ kind: 'text', text: 'Następny akapit' }]] },
    ])
  })

  it('recognises bullet and numbered lists directly after a paragraph', () => {
    const blocks = parseMarkdown('Kroki:\n1. Otwórz sprawę\n2. Przedłuż termin\n   do 30 dni\n- uwaga\nKoniec.')
    expect(blocks.map((block) => block.kind)).toEqual(['paragraph', 'list', 'list', 'paragraph'])
    expect(blocks[1]).toEqual({
      kind: 'list',
      ordered: true,
      start: 1,
      items: [
        [{ kind: 'text', text: 'Otwórz sprawę' }],
        [{ kind: 'text', text: 'Przedłuż termin' }, { kind: 'text', text: ' ' }, { kind: 'text', text: 'do 30 dni' }],
      ],
    })
    expect(blocks[2]).toMatchObject({ kind: 'list', ordered: false })
  })

  it('keeps the start number of a numbered list', () => {
    expect(parseMarkdown('3. trzeci')).toEqual([{ kind: 'list', ordered: true, start: 3, items: [[{ kind: 'text', text: 'trzeci' }]] }])
  })

  it('renders headings as bold paragraphs', () => {
    expect(parseMarkdown('## Terminy')).toEqual([
      { kind: 'paragraph', lines: [[{ kind: 'strong', children: [{ kind: 'text', text: 'Terminy' }] }]] },
    ])
  })
})

describe('toPlainText', () => {
  it('strips formatting for screen reader announcements', () => {
    expect(toPlainText('Masz **2** sprawy:\n- [pierwsza](/wnioski/1)\n- `druga`')).toBe('Masz 2 sprawy: pierwsza druga')
  })
})
