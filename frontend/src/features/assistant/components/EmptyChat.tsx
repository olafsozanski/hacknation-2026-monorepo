import { Box, Button, Flex, Stack, Text } from '@chakra-ui/react'
import { MessageCircleQuestion } from 'lucide-react'
import { useId, useState } from 'react'
import { useSystemInfo } from '@/features/platform/api'
import { useAssistant } from '../context'
import { hasInvalidFields } from '../screenContext'
import { suggestedQuestions } from '../suggestions'

export function EmptyChat() {
  const { contextPath, onAssistantPage, ask } = useAssistant()
  const { data: info } = useSystemInfo()
  const [fieldErrors] = useState(() => !onAssistantPage && hasInvalidFields())
  const listLabelId = useId()
  const questions = suggestedQuestions(contextPath, fieldErrors)
  const proposesActions = info?.ai.enabled === true && info.ai.assistantTools

  return (
    <Stack gap="5" py="2">
      <Box>
        <Text fontWeight="semibold" fontSize="lg">
          W czym mogę pomóc?
        </Text>
        <Text color="fg.muted" fontSize="sm" mt="1">
          Odpowiadam na pytania o działanie aplikacji, terminy i bieżącą sprawę, biorąc pod uwagę ekran, na którym
          jesteś.
          {proposesActions && ' Mogę też zaproponować działanie — wykonam je dopiero po Twoim zatwierdzeniu.'}
        </Text>
      </Box>
      <Box>
        <Text id={listLabelId} fontSize="sm" fontWeight="semibold" mb="2">
          Przykładowe pytania
        </Text>
        <Flex as="ul" aria-labelledby={listLabelId} gap="2" wrap="wrap" listStyleType="none" m="0" p="0">
          {questions.map((question) => (
            <Box as="li" key={question} maxW="full">
              <Button
                variant="outline"
                size="sm"
                maxW="full"
                h="auto"
                minH="8"
                py="1.5"
                textAlign="start"
                whiteSpace="normal"
                onClick={() => ask(question)}
              >
                <MessageCircleQuestion size={16} aria-hidden />
                {question}
              </Button>
            </Box>
          ))}
        </Flex>
      </Box>
    </Stack>
  )
}
