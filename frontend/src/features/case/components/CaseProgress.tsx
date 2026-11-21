import { Box, Flex, Text, chakra } from '@chakra-ui/react'
import { Check } from 'lucide-react'
import type { RequestDetail } from '@/api/types'
import { formatDate } from '@/lib/dates'

function currentStep(status: RequestDetail['status']) {
  switch (status) {
    case 'NoAction':
      return 1
    case 'QueriesSent':
    case 'Extended':
      return 2
    case 'ResponsePrepared':
      return 3
    case 'ResponseSent':
      return 4
  }
}

export function CaseProgress({ request }: { request: RequestDetail }) {
  const current = currentStep(request.status)
  const asked = request.departments.filter((d) => d.askedOn).length
  const steps = [
    { title: 'Rejestracja', detail: `Wpłynął ${formatDate(request.receivedOn)}` },
    {
      title: 'Zapytania do wydziałów',
      detail:
        request.status === 'Extended' && request.extendedDueOn
          ? `Kwerenda do ${formatDate(request.extendedDueOn)}`
          : asked > 0
            ? `Wysłano: ${asked} z ${request.departments.length}`
            : 'Nie wysłano',
    },
    { title: 'Odpowiedź przygotowana', detail: request.draft ? 'Projekt gotowy' : 'Brak projektu' },
    {
      title: 'Odpowiedź wysłana',
      detail: request.respondedOn ? formatDate(request.respondedOn) : `Termin ${formatDate(request.effectiveDueOn)}`,
    },
  ]

  return (
    <chakra.ol
      aria-label="Etapy obsługi wniosku"
      display="grid"
      gridTemplateColumns={{ base: '1fr 1fr', md: 'repeat(4, 1fr)' }}
      gap={{ base: 3, md: 0 }}
      listStyleType="none"
      m="0"
      p="0"
      bg="bg.panel"
      borderWidth="1px"
      borderColor="border"
      borderRadius="md"
      px="4"
      py="3"
    >
      {steps.map((step, index) => {
        const number = index + 1
        const done = number < current || request.status === 'ResponseSent'
        const active = number === current && !done
        return (
          <chakra.li key={step.title} aria-current={active ? 'step' : undefined} display="flex" gap="3" alignItems="center" position="relative">
            <Flex
              boxSize="8"
              flexShrink={0}
              borderRadius="full"
              align="center"
              justify="center"
              fontWeight="bold"
              fontSize="sm"
              bg={done ? 'brand.solid' : active ? 'brand.subtle' : 'bg.muted'}
              color={done ? 'brand.contrast' : active ? 'brand.fg' : 'fg.muted'}
              borderWidth="2px"
              borderColor={done || active ? 'brand.solid' : 'border'}
            >
              {done ? <Check size={16} aria-hidden strokeWidth={3} /> : number}
            </Flex>
            <Box minW="0" pr="2">
              <Text fontWeight={active ? 'bold' : 'semibold'} fontSize="sm" color={done || active ? 'fg' : 'fg.muted'}>
                {step.title}
                <Box as="span" srOnly>
                  {done ? ' — zakończony' : active ? ' — bieżący etap' : ' — przed nami'}
                </Box>
              </Text>
              <Text fontSize="xs" color="fg.muted" truncate>
                {step.detail}
              </Text>
            </Box>
          </chakra.li>
        )
      })}
    </chakra.ol>
  )
}
