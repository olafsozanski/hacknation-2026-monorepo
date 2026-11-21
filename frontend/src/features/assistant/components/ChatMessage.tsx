import { Box, Stack, Text } from '@chakra-ui/react'
import { CircleStop, PowerOff, TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import type { AssistantMessage } from '@/api/types'
import { formatDateTime, formatTime } from '@/lib/dates'
import { AssistantMarkdown } from './AssistantMarkdown'
import { ActionCard } from './ActionCard'
import { NoticeLine } from './NoticeLine'

function timeLabel(iso: string) {
  const date = new Date(iso)
  return date.toDateString() === new Date().toDateString() ? formatTime(date) : formatDateTime(iso)
}

export function UserBubble({ text, caption }: { text: string; caption: string }) {
  return (
    <Box as="li" alignSelf="flex-end" maxW="85%" minW="0">
      <Text fontSize="xs" color="fg.muted" textAlign="end" mb="1">
        {caption}
      </Text>
      <Box
        bg="brand.subtle"
        borderWidth="1px"
        borderColor="brand.muted"
        borderRadius="lg"
        px="3"
        py="2"
        whiteSpace="pre-wrap"
        overflowWrap="anywhere"
      >
        {text}
      </Box>
    </Box>
  )
}

export function AssistantBubble({ caption, children }: { caption: string; children: ReactNode }) {
  return (
    <Box as="li" alignSelf="stretch" minW="0">
      <Text fontSize="xs" color="fg.muted" mb="1">
        {caption}
      </Text>
      <Box bg="bg.subtle" borderWidth="1px" borderColor="border" borderRadius="lg" px="3" py="2.5">
        {children}
      </Box>
    </Box>
  )
}

type ChatMessageProps = { message: AssistantMessage; conversationId: string; warnings: string[] }

export function ChatMessage({ message, conversationId, warnings }: ChatMessageProps) {
  const time = timeLabel(message.createdAt)
  if (message.role === 'User') return <UserBubble text={message.content} caption={`Ty · ${time}`} />

  const basicModeNotices = message.aiUsed ? warnings : warnings.length > 0 ? warnings : ['Odpowiedź w trybie podstawowym — bez modelu AI']
  return (
    <AssistantBubble caption={`Asystent · ${time}`}>
      <Stack gap="3">
        {message.content.trim() ? (
          <AssistantMarkdown content={message.content} />
        ) : (
          <Text color="fg.muted">Brak treści odpowiedzi.</Text>
        )}
        {(message.interrupted || basicModeNotices.length > 0) && (
          <Stack gap="1">
            {message.interrupted && <NoticeLine icon={CircleStop}>Odpowiedź przerwana — może być niepełna</NoticeLine>}
            {basicModeNotices.map((notice) => (
              <NoticeLine key={notice} icon={message.aiUsed ? TriangleAlert : PowerOff}>
                {notice}
              </NoticeLine>
            ))}
          </Stack>
        )}
        {message.actions.map((action) => (
          <ActionCard key={action.id} action={action} conversationId={conversationId} />
        ))}
      </Stack>
    </AssistantBubble>
  )
}
