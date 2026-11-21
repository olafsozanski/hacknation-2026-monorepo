import { ApiError } from '@/api/client'
import type { AssistantAction, AssistantConversation, AssistantMessage } from '@/api/types'

export const maxQuestionLength = 4000

export type PendingTurn = { question: string; saved: boolean; status: string; answer: string }
export type Failure = { question: string; saved: boolean; message: string }
export type MessageWarnings = { messageId: string; items: string[] }

export type ChatState = {
  conversationId: string | null
  draft: string
  turn: PendingTurn | null
  failure: Failure | null
  warnings: MessageWarnings | null
}

export type ChatAction =
  | { type: 'draftChanged'; text: string }
  | { type: 'started'; question: string }
  | { type: 'saved'; conversationId: string }
  | { type: 'status'; text: string }
  | { type: 'delta'; text: string }
  | { type: 'finished'; messageId: string; warnings: string[] }
  | { type: 'failed'; message: string }
  | { type: 'stopped' }
  | { type: 'selected'; conversationId: string | null }
  | { type: 'reset' }

export const initialChatState: ChatState = { conversationId: null, draft: '', turn: null, failure: null, warnings: null }

export const sendingStatus = 'Wysyłam pytanie…'
export const analysingStatus = 'Analizuję pytanie…'

export function chatReducer(state: ChatState, action: ChatAction): ChatState {
  switch (action.type) {
    case 'draftChanged':
      return { ...state, draft: action.text }
    case 'started':
      return { ...state, failure: null, turn: { question: action.question, saved: false, status: sendingStatus, answer: '' } }
    case 'saved':
      return state.turn
        ? { ...state, conversationId: action.conversationId, turn: { ...state.turn, saved: true, status: analysingStatus } }
        : state
    case 'status':
      return state.turn ? { ...state, turn: { ...state.turn, status: action.text } } : state
    case 'delta':
      return state.turn ? { ...state, turn: { ...state.turn, answer: state.turn.answer + action.text } } : state
    case 'finished':
      return {
        ...state,
        turn: null,
        warnings: action.warnings.length > 0 ? { messageId: action.messageId, items: action.warnings } : null,
      }
    case 'failed':
      return {
        ...state,
        turn: null,
        failure: { question: state.turn?.question ?? '', saved: state.turn?.saved ?? false, message: action.message },
      }
    case 'stopped': {
      const restoreQuestion = state.turn !== null && !state.turn.saved && state.draft === ''
      return { ...state, turn: null, draft: restoreQuestion ? (state.turn?.question ?? '') : state.draft }
    }
    case 'selected':
      return { ...initialChatState, conversationId: action.conversationId, draft: state.draft }
    case 'reset':
      return initialChatState
  }
}

export function withMessage(
  conversation: AssistantConversation | undefined,
  summary: { id: string; title: string },
  message: AssistantMessage,
): AssistantConversation {
  const base = conversation ?? { ...summary, updatedAt: message.createdAt, messages: [] }
  const exists = base.messages.some((m) => m.id === message.id)
  return {
    ...base,
    title: summary.title || base.title,
    updatedAt: message.createdAt,
    messages: exists ? base.messages.map((m) => (m.id === message.id ? message : m)) : [...base.messages, message],
  }
}

export function withAction(conversation: AssistantConversation, action: AssistantAction): AssistantConversation {
  return {
    ...conversation,
    messages: conversation.messages.map((message) =>
      message.actions.some((a) => a.id === action.id)
        ? { ...message, actions: message.actions.map((a) => (a.id === action.id ? action : a)) }
        : message,
    ),
  }
}

export function interruptedMessage(answer: string): AssistantMessage {
  return {
    id: `local-${Date.now()}`,
    role: 'Assistant',
    content: answer,
    createdAt: new Date().toISOString(),
    aiUsed: true,
    interrupted: true,
    actions: [],
  }
}

export function describeAssistantError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 0) return 'Brak połączenia z serwerem. Sprawdź sieć i spróbuj ponownie.'
    if (error.status === 401) return 'Sesja wygasła. Zaloguj się ponownie, aby kontynuować rozmowę.'
    if (error.status === 404) return 'Nie znaleziono tej rozmowy — mogła zostać usunięta. Rozpocznij nową rozmowę.'
    if (error.status === 429) return 'Wysłano zbyt wiele pytań w krótkim czasie. Odczekaj chwilę i spróbuj ponownie.'
    if (error.status === 400) {
      const details = Object.values(error.fieldErrors)
      return details.length > 0 ? details.join(' ') : error.message
    }
    return error.message
  }
  if (error instanceof TypeError) return 'Połączenie z serwerem zostało przerwane. Sprawdź sieć i spróbuj ponownie.'
  return 'Nie udało się uzyskać odpowiedzi. Spróbuj ponownie.'
}

export const incompleteStreamMessage = 'Odpowiedź nie dotarła w całości. Spróbuj ponownie.'
