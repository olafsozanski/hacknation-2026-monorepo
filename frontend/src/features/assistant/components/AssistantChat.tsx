import { Alert, Box, Button, Flex, Spinner, Stack, Text } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { MessageCirclePlus, RotateCw, Square } from 'lucide-react'
import { useEffect, useLayoutEffect, useRef, type RefObject } from 'react'
import { ApiError } from '@/api/client'
import { ErrorState, LoadingState } from '@/components/common/States'
import { conversationQuery } from '../api'
import { useAssistant } from '../context'
import { assistantUiProps } from '../screenContext'
import { AssistantMarkdown } from './AssistantMarkdown'
import { AssistantBubble, ChatMessage, UserBubble } from './ChatMessage'
import { Composer } from './Composer'
import { EmptyChat } from './EmptyChat'
import { ModeNotice } from './ModeNotice'

function StatusBar({ status, onStop }: { status: string; onStop: () => void }) {
  return (
    <Flex align="center" gap="3" px="4" py="2" borderTopWidth="1px" borderColor="border" bg="bg.subtle">
      <Spinner size="sm" color="brand.solid" flexShrink={0} />
      <Text fontSize="sm" flex="1" minW="0">
        {status}
      </Text>
      <Button size="sm" variant="outline" onClick={onStop} flexShrink={0}>
        <Square size={14} aria-hidden /> Zatrzymaj
      </Button>
    </Flex>
  )
}

function FailureAlert({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <Box px="4" pt="3" bg="bg.panel">
      <Alert.Root status="error" variant="outline" size="sm" role="alert">
        <Alert.Indicator />
        <Alert.Content>
          <Alert.Title>Nie udało się uzyskać odpowiedzi</Alert.Title>
          <Alert.Description>{message}</Alert.Description>
          <Button size="sm" variant="outline" mt="2" alignSelf="flex-start" onClick={onRetry}>
            <RotateCw size={16} aria-hidden /> Spróbuj ponownie
          </Button>
        </Alert.Content>
      </Alert.Root>
    </Box>
  )
}

function ConversationError({ error, onRetry, onNew }: { error: Error; onRetry: () => void; onNew: () => void }) {
  if (error instanceof ApiError && error.status === 404) {
    return (
      <Alert.Root status="info" variant="subtle" role="alert">
        <Alert.Indicator />
        <Alert.Content>
          <Alert.Title>Ta rozmowa nie jest już dostępna</Alert.Title>
          <Alert.Description>Mogła zostać usunięta albo minął okres jej przechowywania.</Alert.Description>
          <Button size="sm" variant="outline" mt="2" alignSelf="flex-start" onClick={onNew}>
            <MessageCirclePlus size={16} aria-hidden /> Nowa rozmowa
          </Button>
        </Alert.Content>
      </Alert.Root>
    )
  }
  return <ErrorState error={error} onRetry={onRetry} title="Nie udało się wczytać rozmowy" />
}

function useRestoreComposerFocus(textareaRef: RefObject<HTMLTextAreaElement | null>, streaming: boolean, failure: unknown) {
  const previous = useRef({ streaming, failure })
  useEffect(() => {
    const changed = previous.current.streaming !== streaming || previous.current.failure !== failure
    previous.current = { streaming, failure }
    if (!changed) return
    const active = document.activeElement
    if (!active || active === document.body) textareaRef.current?.focus()
  }, [textareaRef, streaming, failure])
}

function useStickToBottom(conversationId: string | null, question: string | null) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const stick = useRef(true)
  const lastSeen = useRef<{ conversationId: string | null; question: string | null }>({ conversationId: null, question: null })

  useLayoutEffect(() => {
    const element = scrollRef.current
    if (!element) return
    const currentQuestion = question ?? lastSeen.current.question
    const changed = lastSeen.current.conversationId !== conversationId || lastSeen.current.question !== currentQuestion
    lastSeen.current = { conversationId, question: currentQuestion }
    if (changed || stick.current) element.scrollTop = element.scrollHeight
  })

  const onScroll = () => {
    const element = scrollRef.current
    if (element) stick.current = element.scrollHeight - element.scrollTop - element.clientHeight < 48
  }

  return { scrollRef, onScroll }
}

export function AssistantChat({ autoFocus = false }: { autoFocus?: boolean }) {
  const { conversationId, turn, failure, warnings, streaming, stop, retry, newConversation, contextPath } = useAssistant()
  const conversation = useQuery({ ...conversationQuery(conversationId ?? ''), enabled: conversationId !== null })
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const { scrollRef, onScroll } = useStickToBottom(conversationId, turn?.question ?? null)
  const messages = conversationId ? (conversation.data?.messages ?? []) : []
  const loading = conversationId !== null && conversation.isPending
  const loadError = conversationId !== null && conversation.isError && !conversation.data ? conversation.error : null
  const empty = messages.length === 0 && !turn && !failure && !loading && !loadError

  useEffect(() => {
    if (autoFocus) textareaRef.current?.focus()
  }, [autoFocus])

  useRestoreComposerFocus(textareaRef, streaming, failure)

  return (
    <Flex direction="column" h="full" minH="0" {...assistantUiProps}>
      <ModeNotice />
      <Box
        ref={scrollRef}
        onScroll={onScroll}
        flex="1"
        minH="0"
        overflowY="auto"
        px="4"
        py="4"
        tabIndex={0}
        role="region"
        aria-label="Rozmowa z asystentem"
        _focusVisible={{ outlineOffset: '-3px' }}
      >
        {loading ? (
          <LoadingState label="Wczytywanie rozmowy…" />
        ) : loadError ? (
          <ConversationError error={loadError} onRetry={() => void conversation.refetch()} onNew={newConversation} />
        ) : empty ? (
          <EmptyChat key={contextPath} />
        ) : (
          <Stack as="ol" aria-label="Wiadomości" aria-busy={streaming} gap="4" listStyleType="none" m="0" p="0">
            {messages.map((message) => (
              <ChatMessage
                key={message.id}
                message={message}
                conversationId={conversationId ?? ''}
                warnings={warnings?.messageId === message.id ? warnings.items : []}
              />
            ))}
            {turn && !turn.saved && <UserBubble text={turn.question} caption="Ty · wysyłanie…" />}
            {failure && !failure.saved && <UserBubble text={failure.question} caption="Ty · pytanie nie zostało wysłane" />}
            {turn?.answer && (
              <AssistantBubble caption="Asystent · pisze odpowiedź…">
                <AssistantMarkdown content={turn.answer} />
              </AssistantBubble>
            )}
          </Stack>
        )}
      </Box>
      {turn && <StatusBar status={turn.status} onStop={stop} />}
      {failure && <FailureAlert message={failure.message} onRetry={retry} />}
      <Composer textareaRef={textareaRef} />
    </Flex>
  )
}
