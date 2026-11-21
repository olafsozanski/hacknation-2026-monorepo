import { Box, Button, Field, Flex, Textarea } from '@chakra-ui/react'
import { Send } from 'lucide-react'
import type { FormEvent, KeyboardEvent, RefObject } from 'react'
import { plural } from '@/lib/dates'
import { maxQuestionLength } from '../chatState'
import { useAssistant } from '../context'

const counterThreshold = maxQuestionLength - 500

export function Composer({ textareaRef }: { textareaRef: RefObject<HTMLTextAreaElement | null> }) {
  const { draft, setDraft, submitDraft, streaming } = useAssistant()
  const remaining = maxQuestionLength - draft.length
  const canSend = !streaming && draft.trim().length > 0

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!canSend) return
    submitDraft()
    textareaRef.current?.focus()
  }

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key !== 'Enter' || event.shiftKey || event.nativeEvent.isComposing) return
    event.preventDefault()
    if (canSend) submitDraft()
  }

  return (
    <Box as="form" onSubmit={submit} px="4" py="3" borderTopWidth="1px" borderColor="border" bg="bg.panel">
      <Field.Root>
        <Field.Label fontSize="sm">Twoje pytanie</Field.Label>
        <Textarea
          ref={textareaRef}
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={onKeyDown}
          maxLength={maxQuestionLength}
          rows={2}
          autoresize
          maxH="40"
          placeholder="Zapytaj o aplikację, terminy lub bieżącą sprawę"
        />
        <Flex mt="2" gap="3" align="center" justify="space-between" w="full">
          <Field.HelperText mt="0">
            Enter – wyślij, Shift+Enter – nowa linia.
            {draft.length >= counterThreshold &&
              ` Pozostało ${remaining} ${plural(remaining, 'znak', 'znaki', 'znaków')}.`}
          </Field.HelperText>
          <Button type="submit" size="sm" disabled={!canSend} flexShrink={0}>
            <Send size={16} aria-hidden /> Wyślij
          </Button>
        </Flex>
      </Field.Root>
    </Box>
  )
}
