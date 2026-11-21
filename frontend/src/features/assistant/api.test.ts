import { describe, expect, it } from 'vitest'
import type { AssistantMessage } from '@/api/types'
import { toStreamEvent } from './api'

const message: AssistantMessage = {
  id: 'm1',
  role: 'Assistant',
  content: 'Odpowiedź',
  createdAt: '2026-11-20T10:00:00Z',
  aiUsed: false,
  interrupted: false,
  actions: [],
}

describe('toStreamEvent', () => {
  it('maps contract events', () => {
    expect(toStreamEvent({ event: 'status', data: '{"text":"Szukam w rejestrze…"}' })).toEqual({ type: 'status', text: 'Szukam w rejestrze…' })
    expect(toStreamEvent({ event: 'delta', data: '{"text":"Ala"}' })).toEqual({ type: 'delta', text: 'Ala' })
    expect(
      toStreamEvent({ event: 'conversation', data: JSON.stringify({ conversationId: 'c1', title: 'Tytuł', userMessage: message }) }),
    ).toEqual({ type: 'conversation', conversationId: 'c1', title: 'Tytuł', userMessage: message })
    expect(toStreamEvent({ event: 'done', data: JSON.stringify({ message, warnings: ['Uwaga', 3] }) })).toEqual({
      type: 'done',
      message,
      warnings: ['Uwaga'],
    })
    expect(toStreamEvent({ event: 'error', data: JSON.stringify({ message: 'Przerwano', assistantMessage: null }) })).toEqual({
      type: 'error',
      message: 'Przerwano',
      assistantMessage: null,
    })
  })

  it('ignores malformed and unknown events', () => {
    expect(toStreamEvent({ event: 'delta', data: 'nie json' })).toBeNull()
    expect(toStreamEvent({ event: 'delta', data: '"tekst"' })).toBeNull()
    expect(toStreamEvent({ event: 'delta', data: '{"text":5}' })).toBeNull()
    expect(toStreamEvent({ event: 'done', data: '{"message":{}}' })).toBeNull()
    expect(toStreamEvent({ event: 'message', data: '{"text":"x"}' })).toBeNull()
  })

  it('falls back to a Polish message when an error event has none', () => {
    expect(toStreamEvent({ event: 'error', data: '{}' })).toEqual({
      type: 'error',
      message: 'Nie udało się dokończyć odpowiedzi.',
      assistantMessage: null,
    })
  })
})
