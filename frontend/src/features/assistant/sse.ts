export type SseEvent = { event: string; data: string }

export function createSseParser() {
  let buffer = ''
  let skipLineFeed = false
  let eventType = ''
  let dataLines: string[] = []

  const processLine = (line: string, events: SseEvent[]) => {
    if (line === '') {
      if (dataLines.length > 0) events.push({ event: eventType || 'message', data: dataLines.join('\n') })
      eventType = ''
      dataLines = []
      return
    }
    if (line.startsWith(':')) return
    const colon = line.indexOf(':')
    const field = colon === -1 ? line : line.slice(0, colon)
    const raw = colon === -1 ? '' : line.slice(colon + 1)
    const value = raw.startsWith(' ') ? raw.slice(1) : raw
    if (field === 'event') eventType = value
    else if (field === 'data') dataLines.push(value)
  }

  return {
    push(chunk: string): SseEvent[] {
      if (chunk === '') return []
      const text = skipLineFeed && chunk.startsWith('\n') ? chunk.slice(1) : chunk
      skipLineFeed = text.endsWith('\r')
      const lines = (buffer + text).split(/\r\n|\r|\n/)
      buffer = lines.pop() ?? ''
      const events: SseEvent[] = []
      for (const line of lines) processLine(line, events)
      return events
    },
  }
}

export async function* readSseEvents(stream: ReadableStream<Uint8Array>): AsyncGenerator<SseEvent> {
  const reader = stream.getReader()
  const decoder = new TextDecoder()
  const parser = createSseParser()
  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      yield* parser.push(decoder.decode(value, { stream: true }))
    }
    yield* parser.push(decoder.decode())
  } finally {
    reader.cancel().catch(() => undefined)
  }
}
