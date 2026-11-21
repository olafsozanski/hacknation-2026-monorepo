import { Alert, Badge, Box, Button, Field, Flex, Link, Stack, Text, Textarea } from '@chakra-ui/react'
import { Check, CircleCheck, CircleX, Hourglass, TriangleAlert, X, type LucideIcon } from 'lucide-react'
import { Link as RouterLink } from 'react-router'
import type { AssistantAction, AssistantActionStatus } from '@/api/types'
import { CopyButton } from '@/components/common/CopyButton'
import { assistantActionKindLabels, assistantActionStatusLabels } from '@/lib/labels'
import { useDecideAction, type ActionDecision } from '../api'
import { useAssistant } from '../context'

const statusStyles: Record<AssistantActionStatus, { icon: LucideIcon; palette: string }> = {
  Proposed: { icon: Hourglass, palette: 'blue' },
  Executed: { icon: CircleCheck, palette: 'green' },
  Rejected: { icon: CircleX, palette: 'gray' },
  Failed: { icon: TriangleAlert, palette: 'red' },
}

function resultAnnouncement(action: AssistantAction) {
  const outcome = action.outcome ? ` ${action.outcome}` : ''
  if (action.status === 'Executed') return `Wykonano działanie: ${action.summary}.${outcome}`
  if (action.status === 'Failed') return `Nie udało się wykonać działania: ${action.summary}.${outcome}`
  return `Odrzucono propozycję: ${action.summary}.`
}

export function ActionCard({ action, conversationId }: { action: AssistantAction; conversationId: string }) {
  const { announce } = useAssistant()
  const decide = useDecideAction(conversationId)
  const { icon: StatusIcon, palette } = statusStyles[action.status]
  const pendingDecision = decide.isPending ? decide.variables.decision : null
  const headingId = `assistant-action-${action.id}`

  const run = (decision: ActionDecision) =>
    decide.mutate({ actionId: action.id, decision }, { onSuccess: (updated) => announce(resultAnnouncement(updated)) })

  return (
    <Box
      as="section"
      aria-labelledby={headingId}
      borderWidth="1px"
      borderColor="border.emphasized"
      borderRadius="md"
      bg="bg.panel"
      p="3"
    >
      <Stack gap="2.5">
        <Flex gap="2" align="center" wrap="wrap" justify="space-between">
          <Text fontSize="xs" fontWeight="bold" color="fg.muted" textTransform="uppercase" letterSpacing="wide">
            Propozycja · {assistantActionKindLabels[action.kind]}
          </Text>
          <Badge colorPalette={palette} variant="subtle" gap="1">
            <StatusIcon size={14} aria-hidden />
            {assistantActionStatusLabels[action.status]}
          </Badge>
        </Flex>
        <Text id={headingId} fontWeight="semibold">
          {action.summary}
        </Text>
        {action.details.length > 0 && (
          <Box as="ul" ps="5" listStyleType="disc" fontSize="sm">
            {action.details.map((detail, index) => (
              <li key={index}>{detail}</li>
            ))}
          </Box>
        )}
        <Link asChild color="brand.fg" textDecoration="underline" fontSize="sm" alignSelf="flex-start">
          <RouterLink to={`/wnioski/${encodeURIComponent(action.requestId)}`}>Przejdź do sprawy {action.caseNumber}</RouterLink>
        </Link>
        {action.outcome && <Text fontSize="sm">{action.outcome}</Text>}
        {action.letter && (
          <Field.Root>
            <Field.Label fontSize="sm">Projekt pisma do skopiowania</Field.Label>
            <Textarea readOnly value={action.letter} rows={8} fontSize="sm" lineHeight="1.6" />
            <CopyButton text={action.letter} label="Kopiuj pismo" size="sm" mt="2" />
          </Field.Root>
        )}
        {decide.isError && (
          <Alert.Root status="error" variant="outline" size="sm" role="alert">
            <Alert.Indicator />
            <Alert.Title>{decide.error.message}</Alert.Title>
          </Alert.Root>
        )}
        {action.status === 'Proposed' && (
          <Stack gap="2">
            <Text fontSize="sm" color="fg.muted">
              Działanie zostanie wykonane dopiero po Twoim zatwierdzeniu.
            </Text>
            <Flex gap="2" wrap="wrap">
              <Button
                size="sm"
                onClick={() => run('confirm')}
                loading={pendingDecision === 'confirm'}
                loadingText="Wykonuję…"
                disabled={decide.isPending}
              >
                <Check size={16} aria-hidden /> Zatwierdź i wykonaj
              </Button>
              <Button
                size="sm"
                variant="outline"
                onClick={() => run('reject')}
                loading={pendingDecision === 'reject'}
                loadingText="Odrzucam…"
                disabled={decide.isPending}
              >
                <X size={16} aria-hidden /> Odrzuć
              </Button>
            </Flex>
          </Stack>
        )}
      </Stack>
    </Box>
  )
}
