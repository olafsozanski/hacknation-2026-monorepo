import { describe, expect, it } from 'vitest'
import { ApiError } from '@/api/client'
import type { AssistantAction, AssistantConversation, AssistantMessage } from '@/api/types'
import {
  analysingStatus,
  chatReducer,
  describeAssistantError,
  initialChatState,
  sendingStatus,
  withAction,
  withMessage,
  type ChatAction,
  type ChatState,
} from './chatState'

const run = (actions: ChatAction[], state: ChatState = initialChatState) => actions.reduce(chatReducer, state)

const message = (id: string, role: AssistantMessage['role'], content: string, actions: AssistantAction[] = []): AssistantMessage => ({
  id,
  role,
  content,
  createdAt: '2026-11-20T10:00:00Z',
  aiUsed: true,
  interrupted: false,
  actions,
})

const action: AssistantAction = {
  id: 'a1',
  kind: 'ExtendDeadline',
  requestId: 'r1',
  caseNumber: 'BKS.IV.1431.3.2026',
  summary: 'Przedłużenie terminu',
  details: [],
  status: 'Proposed',
  outcome: null,
  letter: null,
  createdAt: '2026-11-20T10:00:00Z',
}

describe('chatReducer', () => {
  it('tracks a streamed answer from question to completion', () => {
    const started = run([{ type: 'started', question: 'Jak liczyć termin?' }])
    expect(started.turn).toEqual({ question: 'Jak liczyć termin?', saved: false, status: sendingStatus, answer: '' })

    const streaming = run(
      [
        { type: 'saved', conversationId: 'c1' },
        { type: 'status', text: 'Przygotowuję odpowiedź…' },
        { type: 'delta', text: 'Termin ' },
        { type: 'delta', text: 'wynosi 14 dni.' },
      ],
      started,
    )
    expect(streaming.conversationId).toBe('c1')
    expect(streaming.turn).toEqual({ question: 'Jak liczyć termin?', saved: true, status: 'Przygotowuję odpowiedź…', answer: 'Termin wynosi 14 dni.' })

    const finished = chatReducer(streaming, { type: 'finished', messageId: 'm2', warnings: ['Model AI jest wyłączony.'] })
    expect(finished.turn).toBeNull()
    expect(finished.warnings).toEqual({ messageId: 'm2', items: ['Model AI jest wyłączony.'] })
  })

  it('shows the analysing status once the question is saved', () => {
    expect(run([{ type: 'started', question: 'x' }, { type: 'saved', conversationId: 'c1' }]).turn?.status).toBe(analysingStatus)
  })

  it('keeps the question of a failed turn for retrying', () => {
    const failed = run([{ type: 'started', question: 'Pytanie' }, { type: 'failed', message: 'Brak połączenia' }])
    expect(failed.turn).toBeNull()
    expect(failed.failure).toEqual({ question: 'Pytanie', saved: false, message: 'Brak połączenia' })
    expect(chatReducer(failed, { type: 'started', question: 'Pytanie' }).failure).toBeNull()
  })

  it('puts an unsent question back into an empty composer when stopped', () => {
    expect(run([{ type: 'started', question: 'Pytanie' }, { type: 'stopped' }]).draft).toBe('Pytanie')
    const typing = run([{ type: 'started', question: 'Pytanie' }, { type: 'draftChanged', text: 'Nowe' }, { type: 'stopped' }])
    expect(typing.draft).toBe('Nowe')
    const saved = run([{ type: 'started', question: 'Pytanie' }, { type: 'saved', conversationId: 'c1' }, { type: 'stopped' }])
    expect(saved.draft).toBe('')
  })

  it('ignores stream events when no answer is in progress', () => {
    expect(run([{ type: 'delta', text: 'x' }, { type: 'status', text: 'y' }, { type: 'saved', conversationId: 'c1' }])).toEqual(initialChatState)
  })

  it('switches conversations but keeps the draft', () => {
    const state = run([{ type: 'draftChanged', text: 'Szkic' }, { type: 'started', question: 'x' }, { type: 'selected', conversationId: 'c2' }])
    expect(state).toEqual({ ...initialChatState, conversationId: 'c2', draft: 'Szkic' })
    expect(chatReducer(state, { type: 'reset' })).toEqual(initialChatState)
  })
})

describe('conversation cache helpers', () => {
  it('creates a conversation from its first message and appends or replaces later ones', () => {
    const first = withMessage(undefined, { id: 'c1', title: 'Jak liczyć termin?' }, message('m1', 'User', 'Jak liczyć termin?'))
    expect(first).toEqual({ id: 'c1', title: 'Jak liczyć termin?', updatedAt: '2026-11-20T10:00:00Z', messages: [message('m1', 'User', 'Jak liczyć termin?')] })

    const answered = withMessage(first, { id: 'c1', title: '' }, message('m2', 'Assistant', 'Częściowa'))
    const replaced = withMessage(answered, { id: 'c1', title: '' }, message('m2', 'Assistant', 'Pełna odpowiedź'))
    expect(replaced.title).toBe('Jak liczyć termin?')
    expect(replaced.messages.map((m) => m.content)).toEqual(['Jak liczyć termin?', 'Pełna odpowiedź'])
  })

  it('replaces an updated action inside its message', () => {
    const conversation: AssistantConversation = {
      id: 'c1',
      title: 't',
      updatedAt: '2026-11-20T10:00:00Z',
      messages: [message('m1', 'User', 'q'), message('m2', 'Assistant', 'a', [action])],
    }
    const executed = { ...action, status: 'Executed' as const, outcome: 'Przedłużono termin.' }
    expect(withAction(conversation, executed).messages[1].actions).toEqual([executed])
    expect(withAction(conversation, executed).messages[0]).toBe(conversation.messages[0])
  })
})

describe('describeAssistantError', () => {
  it('explains common failures in Polish', () => {
    expect(describeAssistantError(new ApiError(0, {}))).toMatch(/Brak połączenia/)
    expect(describeAssistantError(new ApiError(429, {}))).toMatch(/zbyt wiele pytań/)
    expect(describeAssistantError(new ApiError(401, {}))).toMatch(/Sesja wygasła/)
    expect(describeAssistantError(new ApiError(404, {}))).toMatch(/Nie znaleziono tej rozmowy/)
    expect(describeAssistantError(new ApiError(400, { errors: { text: ['Pytanie może mieć najwyżej 4000 znaków.'] } }))).toBe(
      'Pytanie może mieć najwyżej 4000 znaków.',
    )
    expect(describeAssistantError(new ApiError(500, {}))).toMatch(/błąd serwera/)
    expect(describeAssistantError(new TypeError('network error'))).toMatch(/przerwane/)
  })
})
