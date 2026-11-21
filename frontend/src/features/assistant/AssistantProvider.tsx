import { Box } from '@chakra-ui/react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useReducer, useRef, useState, type ReactNode } from 'react'
import { useLocation } from 'react-router'
import type { AssistantConversation, AssistantMessage, ScreenContext } from '@/api/types'
import { sessionQuery } from '@/features/auth/session'
import { plural } from '@/lib/dates'
import { assistantKeys, streamAssistantMessage } from './api'
import {
  chatReducer,
  describeAssistantError,
  incompleteStreamMessage,
  initialChatState,
  interruptedMessage,
  maxQuestionLength,
  withMessage,
} from './chatState'
import { AssistantContext, assistantPath, type AssistantContextValue } from './context'
import { toPlainText } from './markdown'
import { assistantUiProps, collectScreenContext, pathOnlyContext } from './screenContext'

function answerAnnouncement(message: AssistantMessage) {
  const parts = [`Odpowiedź asystenta: ${toPlainText(message.content)}`]
  if (!message.aiUsed) parts.push('Odpowiedź w trybie podstawowym, bez modelu AI.')
  const count = message.actions.length
  if (count > 0) parts.push(`Asystent proponuje ${count} ${plural(count, 'działanie', 'działania', 'działań')} do zatwierdzenia.`)
  return parts.join(' ')
}

function readScreen(onAssistantPage: boolean, contextPath: string, currentPath: string): ScreenContext {
  if (onAssistantPage) return pathOnlyContext(contextPath)
  try {
    return collectScreenContext(currentPath)
  } catch {
    return pathOnlyContext(currentPath)
  }
}

export function AssistantProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const { data: user } = useQuery(sessionQuery)
  const userId = user?.id ?? null
  const location = useLocation()
  const currentPath = location.pathname + location.search
  const onAssistantPage = location.pathname === assistantPath
  const [lastAppPath, setLastAppPath] = useState(onAssistantPage ? null : currentPath)
  if (!onAssistantPage && lastAppPath !== currentPath) setLastAppPath(currentPath)
  const [open, setOpen] = useState(false)
  if (onAssistantPage && open) setOpen(false)
  const [state, dispatch] = useReducer(chatReducer, initialChatState)
  const [announcement, setAnnouncement] = useState({ id: 0, text: '' })
  const runRef = useRef(0)
  const controllerRef = useRef<AbortController | null>(null)
  const contextPath = onAssistantPage ? (lastAppPath ?? assistantPath) : currentPath

  const announce = useCallback((text: string) => setAnnouncement((previous) => ({ id: previous.id + 1, text })), [])

  const storeMessage = useCallback(
    (conversationId: string, title: string, message: AssistantMessage) =>
      queryClient.setQueryData<AssistantConversation>(assistantKeys.conversation(conversationId), (conversation) =>
        withMessage(conversation, { id: conversationId, title }, message),
      ),
    [queryClient],
  )

  const cancelRun = useCallback(
    (conversationId: string | null) => {
      if (!controllerRef.current) return
      runRef.current += 1
      controllerRef.current.abort()
      controllerRef.current = null
      void queryClient.invalidateQueries({ queryKey: assistantKeys.conversations })
      if (conversationId)
        void queryClient.invalidateQueries({ queryKey: assistantKeys.conversation(conversationId), refetchType: 'none' })
    },
    [queryClient],
  )

  useEffect(
    () => () => {
      runRef.current += 1
      controllerRef.current?.abort()
      controllerRef.current = null
      dispatch({ type: 'reset' })
      setOpen(false)
    },
    [userId],
  )

  const start = useCallback(
    async (question: string, conversationId: string | null) => {
      const text = question.trim().slice(0, maxQuestionLength)
      if (!text || controllerRef.current) return
      const run = ++runRef.current
      const isCurrent = () => runRef.current === run
      const controller = new AbortController()
      controllerRef.current = controller
      const progress = { conversationId: null as string | null, title: '', answer: '', settled: false }
      const screen = readScreen(onAssistantPage, contextPath, currentPath)
      dispatch({ type: 'started', question: text })
      announce('Wysłano pytanie. Asystent przygotowuje odpowiedź.')

      try {
        await streamAssistantMessage({ conversationId, text, screen }, controller.signal, (event) => {
          if (!isCurrent()) return
          switch (event.type) {
            case 'conversation':
              progress.conversationId = event.conversationId
              progress.title = event.title
              void queryClient.cancelQueries({ queryKey: assistantKeys.conversation(event.conversationId) })
              storeMessage(event.conversationId, event.title, event.userMessage)
              void queryClient.invalidateQueries({ queryKey: assistantKeys.conversations })
              dispatch({ type: 'saved', conversationId: event.conversationId })
              break
            case 'status':
              dispatch({ type: 'status', text: event.text })
              announce(event.text)
              break
            case 'delta':
              progress.answer += event.text
              dispatch({ type: 'delta', text: event.text })
              break
            case 'done':
              progress.settled = true
              if (progress.conversationId) storeMessage(progress.conversationId, progress.title, event.message)
              dispatch({ type: 'finished', messageId: event.message.id, warnings: event.warnings })
              announce(answerAnnouncement(event.message))
              break
            case 'error':
              progress.settled = true
              if (progress.conversationId && event.assistantMessage)
                storeMessage(progress.conversationId, progress.title, event.assistantMessage)
              dispatch({ type: 'failed', message: event.message })
              break
          }
        })
        if (isCurrent() && !progress.settled) dispatch({ type: 'failed', message: incompleteStreamMessage })
      } catch (error) {
        if (!isCurrent()) return
        if (controller.signal.aborted) {
          if (progress.conversationId && progress.answer.trim())
            storeMessage(progress.conversationId, progress.title, interruptedMessage(progress.answer))
          dispatch({ type: 'stopped' })
          announce('Zatrzymano odpowiedź.')
        } else {
          dispatch({ type: 'failed', message: describeAssistantError(error) })
        }
      } finally {
        if (isCurrent()) {
          controllerRef.current = null
          void queryClient.invalidateQueries({ queryKey: assistantKeys.conversations })
          if (progress.conversationId && !controller.signal.aborted)
            void queryClient.invalidateQueries({ queryKey: assistantKeys.conversation(progress.conversationId) })
        }
      }
    },
    [announce, contextPath, currentPath, onAssistantPage, queryClient, storeMessage],
  )

  const { conversationId, draft, failure } = state

  const value = useMemo<AssistantContextValue>(
    () => ({
      ...state,
      open,
      setOpen,
      streaming: state.turn !== null,
      contextPath,
      onAssistantPage,
      announce,
      setDraft: (text) => dispatch({ type: 'draftChanged', text }),
      submitDraft: () => {
        if (!draft.trim() || controllerRef.current) return
        dispatch({ type: 'draftChanged', text: '' })
        void start(draft, conversationId)
      },
      ask: (question) => void start(question, conversationId),
      retry: () => {
        if (failure) void start(failure.question, conversationId)
      },
      stop: () => controllerRef.current?.abort(),
      newConversation: () => {
        cancelRun(conversationId)
        dispatch({ type: 'selected', conversationId: null })
        announce('Rozpoczęto nową rozmowę.')
      },
      selectConversation: (id) => {
        if (id === conversationId) return
        cancelRun(conversationId)
        dispatch({ type: 'selected', conversationId: id })
      },
      forgetConversation: (id) => {
        if (id !== conversationId) return
        cancelRun(null)
        dispatch({ type: 'selected', conversationId: null })
      },
    }),
    [state, open, contextPath, onAssistantPage, announce, draft, conversationId, failure, start, cancelRun],
  )

  return (
    <AssistantContext.Provider value={value}>
      {children}
      <Box {...assistantUiProps} srOnly role="status" aria-live="polite" aria-atomic="true">
        {announcement.text && <span key={announcement.id}>{announcement.text}</span>}
      </Box>
    </AssistantContext.Provider>
  )
}
