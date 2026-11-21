import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError, api, postEventStream } from '@/api/client'
import type {
  AssistantAction,
  AssistantConversation,
  AssistantConversationSummary,
  AssistantMessage,
  AssistantStreamEvent,
  SendAssistantMessage,
} from '@/api/types'
import { caseKeys } from '@/features/case/api'
import { withAction } from './chatState'
import { readSseEvents, type SseEvent } from './sse'

export const assistantKeys = {
  conversations: ['assistant', 'conversations'] as const,
  conversation: (id: string) => ['assistant', 'conversation', id] as const,
}

export const conversationsQuery = queryOptions({
  queryKey: assistantKeys.conversations,
  queryFn: () => api<AssistantConversationSummary[]>('/api/assistant/conversations'),
})

export const conversationQuery = (id: string) =>
  queryOptions({
    queryKey: assistantKeys.conversation(id),
    queryFn: () => api<AssistantConversation>(`/api/assistant/conversations/${encodeURIComponent(id)}`),
  })

function isMessage(value: unknown): value is AssistantMessage {
  if (!value || typeof value !== 'object') return false
  const message = value as Partial<AssistantMessage>
  return typeof message.id === 'string' && typeof message.content === 'string' && Array.isArray(message.actions)
}

const text = (value: unknown) => (typeof value === 'string' ? value : null)

export function toStreamEvent({ event, data }: SseEvent): AssistantStreamEvent | null {
  let payload: Record<string, unknown>
  try {
    const parsed: unknown = JSON.parse(data)
    if (!parsed || typeof parsed !== 'object') return null
    payload = parsed as Record<string, unknown>
  } catch {
    return null
  }
  switch (event) {
    case 'conversation': {
      const conversationId = text(payload.conversationId)
      return conversationId && isMessage(payload.userMessage)
        ? { type: 'conversation', conversationId, title: text(payload.title) ?? '', userMessage: payload.userMessage }
        : null
    }
    case 'status':
    case 'delta': {
      const value = text(payload.text)
      return value === null ? null : { type: event, text: value }
    }
    case 'done':
      return isMessage(payload.message)
        ? {
            type: 'done',
            message: payload.message,
            warnings: Array.isArray(payload.warnings) ? payload.warnings.filter((w): w is string => typeof w === 'string') : [],
          }
        : null
    case 'error':
      return {
        type: 'error',
        message: text(payload.message) || 'Nie udało się dokończyć odpowiedzi.',
        assistantMessage: isMessage(payload.assistantMessage) ? payload.assistantMessage : null,
      }
    default:
      return null
  }
}

export async function streamAssistantMessage(
  body: SendAssistantMessage,
  signal: AbortSignal,
  onEvent: (event: AssistantStreamEvent) => void,
): Promise<void> {
  const response = await postEventStream('/api/assistant/messages', body, signal)
  if (!response.body) return
  for await (const sseEvent of readSseEvents(response.body)) {
    const event = toStreamEvent(sseEvent)
    if (event) onEvent(event)
  }
}

export function useDeleteConversation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/assistant/conversations/${encodeURIComponent(id)}`, { method: 'DELETE' }),
    onSuccess: (_, id) => queryClient.removeQueries({ queryKey: assistantKeys.conversation(id) }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: assistantKeys.conversations }),
  })
}

export type ActionDecision = 'confirm' | 'reject'

export function useDecideAction(conversationId: string) {
  const queryClient = useQueryClient()
  const conversationKey = assistantKeys.conversation(conversationId)
  return useMutation({
    mutationFn: ({ actionId, decision }: { actionId: string; decision: ActionDecision }) =>
      api<AssistantAction>(`/api/assistant/actions/${encodeURIComponent(actionId)}/${decision}`, { method: 'POST' }),
    onSuccess: (action, { decision }) => {
      queryClient.setQueryData<AssistantConversation>(conversationKey, (conversation) =>
        conversation ? withAction(conversation, action) : conversation,
      )
      if (decision === 'confirm') {
        void queryClient.invalidateQueries({ queryKey: caseKeys.detail(action.requestId) })
        void queryClient.invalidateQueries({ queryKey: ['requests'] })
        void queryClient.invalidateQueries({ queryKey: ['search'] })
      }
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) void queryClient.invalidateQueries({ queryKey: conversationKey })
    },
  })
}
