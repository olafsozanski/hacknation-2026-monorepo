import { createContext, useContext } from 'react'
import type { ChatState } from './chatState'

export const assistantPath = '/asystent'

export type AssistantContextValue = ChatState & {
  open: boolean
  setOpen: (open: boolean) => void
  streaming: boolean
  contextPath: string
  onAssistantPage: boolean
  setDraft: (text: string) => void
  submitDraft: () => void
  ask: (question: string) => void
  retry: () => void
  stop: () => void
  newConversation: () => void
  selectConversation: (id: string) => void
  forgetConversation: (id: string) => void
  announce: (text: string) => void
}

export const AssistantContext = createContext<AssistantContextValue | null>(null)

export function useAssistant() {
  const context = useContext(AssistantContext)
  if (!context) throw new Error('useAssistant must be used within AssistantProvider.')
  return context
}
