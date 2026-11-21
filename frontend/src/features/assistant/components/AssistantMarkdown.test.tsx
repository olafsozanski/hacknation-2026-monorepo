import { ChakraProvider } from '@chakra-ui/react'
import { cleanup, render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { system } from '@/theme'
import { AssistantMarkdown } from './AssistantMarkdown'

function renderMarkdown(content: string) {
  return render(
    <ChakraProvider value={system}>
      <MemoryRouter>
        <AssistantMarkdown content={content} />
      </MemoryRouter>
    </ChakraProvider>,
  ).container
}

afterEach(cleanup)

describe('AssistantMarkdown', () => {
  it('renders app links as router links and everything else as text', () => {
    const container = renderMarkdown(
      'Zobacz [rejestr](/) i [nowy wniosek](/wnioski/nowy), nie [BIP](https://bip.example.pl), [to](//evil.example) ani [to](javascript:alert(1)).',
    )
    const links = Array.from(container.querySelectorAll('a'), (a) => [a.textContent, a.getAttribute('href')])
    expect(links).toEqual([
      ['rejestr', '/'],
      ['nowy wniosek', '/wnioski/nowy'],
    ])
    expect(container.textContent).toContain('BIP (https://bip.example.pl)')
    expect(container.textContent).toContain('to (//evil.example)')
  })

  it('never turns answer text into HTML', () => {
    const html = '<img src="x" onerror="alert(1)"><script>alert(2)</script><a href="https://evil.example">klik</a>'
    const container = renderMarkdown(html)
    expect(container.querySelector('img, script, a')).toBeNull()
    expect(container.textContent).toBe(html)
  })

  it('renders lists, bold text and code', () => {
    const container = renderMarkdown('**Kroki:**\n1. Otwórz `Sprawę`\n2. Zapisz\n\n- punkt')
    expect(container.querySelector('strong')?.textContent).toBe('Kroki:')
    expect(container.querySelectorAll('ol > li')).toHaveLength(2)
    expect(container.querySelectorAll('ul > li')).toHaveLength(1)
    expect(container.querySelector('code')?.textContent).toBe('Sprawę')
  })
})
