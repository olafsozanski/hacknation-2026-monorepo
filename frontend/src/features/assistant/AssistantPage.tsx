import { Box, Button, Card, Grid } from '@chakra-ui/react'
import { MessageCirclePlus } from 'lucide-react'
import { useRef } from 'react'
import { PageHeader } from '@/components/common/PageHeader'
import { useSystemInfo } from '@/features/platform/api'
import { AssistantChat } from './components/AssistantChat'
import { ConversationHistory } from './components/ConversationHistory'
import { useAssistant } from './context'

const intro = 'Zapytaj o działanie aplikacji, terminy lub sprawę, którą prowadzisz.'
const actionsNote = ' Asystent może zaproponować działanie — wykona je dopiero po Twoim zatwierdzeniu.'

export function AssistantPage() {
  const { newConversation } = useAssistant()
  const { data: info } = useSystemInfo()
  const proposesActions = info?.ai.enabled === true && info.ai.assistantTools
  const chatRef = useRef<HTMLDivElement>(null)

  const revealChat = () => {
    const chat = chatRef.current
    if (chat && chat.getBoundingClientRect().top < 0) chat.scrollIntoView({ block: 'start' })
  }

  return (
    <>
      <PageHeader
        title="Asystent"
        description={proposesActions ? intro + actionsNote : intro}
        actions={
          <Button onClick={newConversation}>
            <MessageCirclePlus size={16} aria-hidden /> Nowa rozmowa
          </Button>
        }
      />
      <Grid templateColumns={{ base: '1fr', xl: 'minmax(0, 1fr) 340px' }} gap="5" alignItems="start">
        <Card.Root
          ref={chatRef}
          variant="outline"
          bg="bg.panel"
          borderRadius="md"
          overflow="hidden"
          h={{ base: '75dvh', xl: 'calc(100dvh - 15rem)' }}
          minH="30rem"
        >
          <AssistantChat />
        </Card.Root>
        <Box as="aside" aria-label="Historia rozmów">
          <ConversationHistory onOpened={revealChat} />
        </Box>
      </Grid>
    </>
  )
}
