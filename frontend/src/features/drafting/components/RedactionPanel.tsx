import { Badge, Box, Button, Field, Flex, Heading, IconButton, Input, Stack, Text, chakra } from '@chakra-ui/react'
import { Check, CheckCheck, Crosshair, RotateCcw, ScanSearch, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import type { Redaction } from '@/api/types'
import { redactionCategoryLabels, redactionSourceLabels } from '@/lib/labels'
import { countByStatus } from '../redactions'

type RedactionPanelProps = {
  redactions: Redaction[]
  detecting: boolean
  onSetStatus: (index: number, status: Redaction['status']) => void
  onApproveAll: () => void
  onDetect: () => void
  onAddPhrase: (phrase: string) => number
}

function focusMark(index: number) {
  const mark = document.getElementById(`redaction-${index}`)
  mark?.scrollIntoView({ block: 'center', behavior: 'smooth' })
  mark?.focus({ preventScroll: true })
}

function Swatch({ approved }: { approved: boolean }) {
  return (
    <Box
      as="span"
      display="inline-block"
      w="6"
      h="3.5"
      borderRadius="xs"
      bg={approved ? 'fg' : 'bg.emphasized'}
      borderBottomWidth={approved ? 0 : '2px'}
      borderBottomStyle="dashed"
      borderBottomColor="fg.muted"
      aria-hidden
    />
  )
}

function Group({ title, items, onSetStatus }: {
  title: string
  items: { redaction: Redaction; index: number }[]
  onSetStatus: RedactionPanelProps['onSetStatus']
}) {
  if (items.length === 0) return null
  return (
    <Box>
      <Heading as="h3" size="xs" color="fg.muted" textTransform="uppercase" letterSpacing="wider" mb="2">
        {title} ({items.length})
      </Heading>
      <Stack as="ul" gap="1.5" m="0" p="0" listStyleType="none">
        {items.map(({ redaction, index }) => (
          <Flex as="li" key={index} gap="2" align="center" borderWidth="1px" borderColor="border" borderRadius="sm" px="2.5" py="1.5" bg="bg.panel">
            <Box flex="1" minW="0">
              <Text fontSize="sm" fontWeight="semibold" truncate title={redaction.text}>
                {redaction.text}
              </Text>
              <Text fontSize="xs" color="fg.muted">
                {redactionCategoryLabels[redaction.category]} · {redactionSourceLabels[redaction.source]}
              </Text>
            </Box>
            {redaction.status !== 'Rejected' && (
              <IconButton size="xs" variant="ghost" aria-label={`Pokaż w tekście: ${redaction.text}`} onClick={() => focusMark(index)}>
                <Crosshair size={14} aria-hidden />
              </IconButton>
            )}
            {redaction.status !== 'Approved' && (
              <IconButton size="xs" variant="outline" aria-label={`Ukryj: ${redaction.text}`} onClick={() => onSetStatus(index, 'Approved')}>
                <Check size={14} aria-hidden />
              </IconButton>
            )}
            {redaction.status === 'Suggested' && (
              <IconButton size="xs" variant="ghost" aria-label={`Pozostaw jawne: ${redaction.text}`} onClick={() => onSetStatus(index, 'Rejected')}>
                <X size={14} aria-hidden />
              </IconButton>
            )}
            {redaction.status !== 'Suggested' && (
              <IconButton size="xs" variant="ghost" aria-label={`Przywróć do weryfikacji: ${redaction.text}`} onClick={() => onSetStatus(index, 'Suggested')}>
                <RotateCcw size={14} aria-hidden />
              </IconButton>
            )}
          </Flex>
        ))}
      </Stack>
    </Box>
  )
}

export function RedactionPanel({ redactions, detecting, onSetStatus, onApproveAll, onDetect, onAddPhrase }: RedactionPanelProps) {
  const [phrase, setPhrase] = useState('')
  const [feedback, setFeedback] = useState('')
  const counts = countByStatus(redactions)
  const indexed = redactions.map((redaction, index) => ({ redaction, index }))

  const addPhrase = (event: FormEvent) => {
    event.preventDefault()
    const added = onAddPhrase(phrase)
    setFeedback(added > 0 ? `Ukryto wystąpienia: ${added}.` : 'Nie znaleziono frazy w treści.')
    if (added > 0) setPhrase('')
  }

  return (
    <Stack gap="5" as="section" aria-labelledby="redaction-panel-heading">
      <Box>
        <Heading as="h2" id="redaction-panel-heading" size="md" mb="1">
          Anonimizacja
        </Heading>
        <Text fontSize="sm" color="fg.muted">
          Asystent zaznaczył dane, które mogą wymagać ukrycia. Zdecyduj o każdym fragmencie.
        </Text>
      </Box>
      <Stack gap="1.5" fontSize="sm">
        <Flex align="center" gap="2">
          <Swatch approved={false} />
          <Text flex="1">Do weryfikacji</Text>
          <Badge colorPalette={counts.suggested > 0 ? 'orange' : 'gray'} variant="subtle">
            {counts.suggested}
          </Badge>
        </Flex>
        <Flex align="center" gap="2">
          <Swatch approved />
          <Text flex="1">Zostanie ukryte</Text>
          <Badge variant="subtle">{counts.approved}</Badge>
        </Flex>
        <Flex align="center" gap="2">
          <Box as="span" w="6" textAlign="center" aria-hidden>
            Aa
          </Box>
          <Text flex="1">Pozostawione jawne</Text>
          <Badge variant="subtle">{counts.rejected}</Badge>
        </Flex>
      </Stack>
      <Flex gap="2" wrap="wrap">
        <Button size="sm" onClick={onApproveAll} disabled={counts.suggested === 0}>
          <CheckCheck size={14} aria-hidden /> Ukryj wszystkie sugestie
        </Button>
        <Button size="sm" variant="outline" onClick={onDetect} loading={detecting} loadingText="Wykrywanie…">
          <ScanSearch size={14} aria-hidden /> Wykryj ponownie
        </Button>
      </Flex>
      <chakra.form onSubmit={addPhrase}>
        <Field.Root>
          <Field.Label fontSize="sm">Ukryj frazę w całym piśmie</Field.Label>
          <Flex gap="2" w="full">
            <Input size="sm" value={phrase} onChange={(e) => setPhrase(e.target.value)} placeholder="Np. nazwisko lub numer" />
            <Button size="sm" type="submit" variant="outline" disabled={!phrase.trim()}>
              Ukryj
            </Button>
          </Flex>
          <Field.HelperText aria-live="polite">{feedback || 'Możesz też zaznaczyć fragment w tekście myszą.'}</Field.HelperText>
        </Field.Root>
      </chakra.form>
      <Group title="Do weryfikacji" items={indexed.filter((i) => i.redaction.status === 'Suggested')} onSetStatus={onSetStatus} />
      <Group title="Zostanie ukryte" items={indexed.filter((i) => i.redaction.status === 'Approved')} onSetStatus={onSetStatus} />
      <Group title="Pozostawione jawne" items={indexed.filter((i) => i.redaction.status === 'Rejected')} onSetStatus={onSetStatus} />
    </Stack>
  )
}
