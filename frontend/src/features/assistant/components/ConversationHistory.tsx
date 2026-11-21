import { Box, Flex, IconButton, Stack, Text, chakra } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { Trash2 } from 'lucide-react'
import { useRef, useState } from 'react'
import type { AssistantConversationSummary } from '@/api/types'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { Panel } from '@/components/common/Panel'
import { ErrorState } from '@/components/common/States'
import { notifyError, notifySuccess } from '@/components/ui/toaster'
import { useSystemInfo } from '@/features/platform/api'
import { days, formatDateTime } from '@/lib/dates'
import { conversationsQuery, useDeleteConversation } from '../api'
import { useAssistant } from '../context'

const titleOf = (conversation: AssistantConversationSummary) => conversation.title.trim() || 'Rozmowa bez tytułu'

type ConversationItemProps = {
  conversation: AssistantConversationSummary
  current: boolean
  onOpen: () => void
  onDelete: () => void
}

function ConversationItem({ conversation, current, onOpen, onDelete }: ConversationItemProps) {
  return (
    <Flex
      as="li"
      align="center"
      gap="1"
      pe="1"
      borderLeftWidth="4px"
      borderLeftColor={current ? 'brand.solid' : 'transparent'}
      borderRadius="0 var(--chakra-radii-sm) var(--chakra-radii-sm) 0"
      bg={current ? 'brand.subtle' : 'transparent'}
    >
      <chakra.button
        type="button"
        flex="1"
        minW="0"
        textAlign="start"
        px="3"
        py="2"
        borderRadius="sm"
        cursor="pointer"
        _hover={{ bg: current ? 'brand.subtle' : 'bg.muted' }}
        aria-current={current ? 'true' : undefined}
        onClick={onOpen}
      >
        <Text fontWeight={current ? 'bold' : 'medium'} color={current ? 'brand.fg' : 'fg'} lineClamp={2}>
          {titleOf(conversation)}
        </Text>
        <Text fontSize="xs" color="fg.muted">
          {formatDateTime(conversation.updatedAt)}
        </Text>
      </chakra.button>
      <IconButton
        size="xs"
        variant="ghost"
        colorPalette="red"
        aria-label={`Usuń rozmowę „${titleOf(conversation)}”`}
        onClick={onDelete}
      >
        <Trash2 size={14} aria-hidden />
      </IconButton>
    </Flex>
  )
}

export function ConversationHistory({ onOpened }: { onOpened: () => void }) {
  const { conversationId, selectConversation, forgetConversation, announce } = useAssistant()
  const conversations = useQuery(conversationsQuery)
  const { data: info } = useSystemInfo()
  const remove = useDeleteConversation()
  const [deleting, setDeleting] = useState<AssistantConversationSummary | null>(null)
  const listRef = useRef<HTMLDivElement>(null)
  const deletedRef = useRef(false)

  const openConversation = (conversation: AssistantConversationSummary) => {
    selectConversation(conversation.id)
    announce(`Otwarto rozmowę „${titleOf(conversation)}”.`)
    onOpened()
  }

  const confirmDelete = () => {
    if (!deleting) return
    const target = deleting
    remove.mutate(target.id, {
      onSuccess: () => {
        deletedRef.current = true
        forgetConversation(target.id)
        setDeleting(null)
        notifySuccess('Usunięto rozmowę', titleOf(target))
      },
      onError: (error) => {
        setDeleting(null)
        notifyError(error, 'Nie udało się usunąć rozmowy')
      },
    })
  }

  const retention = info?.assistantRetentionDays
  const items = conversations.data ?? []

  return (
    <Panel
      id="assistant-history"
      title="Historia rozmów"
      description={retention ? `Rozmowy są przechowywane przez ${days(retention)}.` : undefined}
    >
      <Box ref={listRef} tabIndex={-1} outline="none">
        {conversations.isPending ? (
          <Text color="fg.muted" role="status">
            Wczytywanie rozmów…
          </Text>
        ) : conversations.isError ? (
          <ErrorState error={conversations.error} onRetry={() => void conversations.refetch()} title="Nie udało się wczytać historii" />
        ) : items.length === 0 ? (
          <Text color="fg.muted" fontSize="sm">
            Nie masz jeszcze zapisanych rozmów.
          </Text>
        ) : (
          <Stack as="ul" gap="1" listStyleType="none" m="0" p="0" aria-label="Zapisane rozmowy">
            {items.map((conversation) => (
              <ConversationItem
                key={conversation.id}
                conversation={conversation}
                current={conversation.id === conversationId}
                onOpen={() => openConversation(conversation)}
                onDelete={() => setDeleting(conversation)}
              />
            ))}
          </Stack>
        )}
      </Box>
      <ConfirmDialog
        open={deleting !== null}
        title="Usunąć rozmowę?"
        confirmLabel="Usuń rozmowę"
        destructive
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={confirmDelete}
        finalFocus={() => {
          const focusList = deletedRef.current
          deletedRef.current = false
          return focusList ? listRef.current : null
        }}
      >
        {`Rozmowa „${deleting ? titleOf(deleting) : ''}” zostanie trwale usunięta. Tej operacji nie można cofnąć.`}
      </ConfirmDialog>
    </Panel>
  )
}
