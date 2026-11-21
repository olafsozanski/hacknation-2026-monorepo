import { describe, expect, it } from 'vitest'
import { createSseParser, readSseEvents, type SseEvent } from './sse'

function parseChunks(chunks: string[]): SseEvent[] {
  const parser = createSseParser()
  return chunks.flatMap((chunk) => parser.push(chunk))
}

function streamOf(chunks: Uint8Array[]) {
  return new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(chunk)
      controller.close()
    },
  })
}

describe('createSseParser', () => {
  it('parses typed events with JSON data', () => {
    expect(parseChunks(['event: status\ndata: {"text":"Szukam w rejestrze…"}\n\n'])).toEqual([
      { event: 'status', data: '{"text":"Szukam w rejestrze…"}' },
    ])
  })

  it('joins events split across arbitrary chunk boundaries', () => {
    const source = 'event: delta\ndata: {"text":"Ala"}\n\nevent: delta\ndata: {"text":" ma kota"}\n\n'
    const chunks = source.match(/.{1,3}/gs) ?? []
    expect(parseChunks(chunks)).toEqual([
      { event: 'delta', data: '{"text":"Ala"}' },
      { event: 'delta', data: '{"text":" ma kota"}' },
    ])
  })

  it('handles CRLF and a CR at the end of a chunk followed by LF in the next one', () => {
    expect(parseChunks(['event: done\r', '\ndata: {}\r\n\r', '\n'])).toEqual([{ event: 'done', data: '{}' }])
  })

  it('accepts lone CR line endings', () => {
    expect(parseChunks(['data: a\r\r'])).toEqual([{ event: 'message', data: 'a' }])
  })

  it('joins multi-line data with line feeds and ignores comments and unknown fields', () => {
    expect(parseChunks([': keep-alive\nid: 7\nretry: 100\ndata: first\ndata:second\n\n'])).toEqual([
      { event: 'message', data: 'first\nsecond' },
    ])
  })

  it('does not dispatch events without data and drops an unterminated last event', () => {
    expect(parseChunks(['event: status\n\n', 'event: delta\ndata: {"text":"x"}'])).toEqual([])
  })

  it('resets the event type after each dispatch', () => {
    expect(parseChunks(['event: status\ndata: 1\n\ndata: 2\n\n'])).toEqual([
      { event: 'status', data: '1' },
      { event: 'message', data: '2' },
    ])
  })
})

describe('readSseEvents', () => {
  it('decodes multi-byte characters split between network chunks', async () => {
    const bytes = new TextEncoder().encode('event: delta\ndata: {"text":"Zażółć gęślą jaźń…"}\n\n')
    const split = bytes.indexOf(0xc5) + 1
    const events: SseEvent[] = []
    for await (const event of readSseEvents(streamOf([bytes.slice(0, split), bytes.slice(split)]))) events.push(event)
    expect(events).toEqual([{ event: 'delta', data: '{"text":"Zażółć gęślą jaźń…"}' }])
  })
})
