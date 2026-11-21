import { Box, Flex, Stack, Text, chakra } from '@chakra-ui/react'
import { ArrowRight, Check, EyeOff, TextCursor, X } from 'lucide-react'
import type { ReactNode } from 'react'
import { Walkthrough, type WalkthroughStep } from '@/components/common/Walkthrough'

const find =
  (...selectors: string[]) =>
  () =>
    selectors.map((selector) => document.querySelector<HTMLElement>(selector)).find(Boolean) ?? null

function Example({ children }: { children: ReactNode }) {
  return (
    <Box aria-hidden mb="3" px="3" py="2.5" borderWidth="1px" borderColor="border" borderRadius="md" bg="bg.subtle" color="fg" lineHeight="1.8">
      {children}
    </Box>
  )
}

function MarkExample({ hidden }: { hidden: boolean }) {
  return (
    <chakra.span
      px="1"
      borderRadius="xs"
      bg={hidden ? 'fg' : 'bg.emphasized'}
      color={hidden ? 'bg' : 'fg'}
      borderBottomWidth={hidden ? 0 : '2px'}
      borderBottomStyle="dashed"
      borderBottomColor="fg.muted"
    >
      Jan Kowalski
    </chakra.span>
  )
}

function ClickExample() {
  return (
    <Example>
      <Flex align="center" gap="2" wrap="wrap">
        <MarkExample hidden={false} />
        <ArrowRight size={14} />
        <MarkExample hidden />
      </Flex>
    </Example>
  )
}

function SelectionExample() {
  return (
    <Example>
      <Text>
        zamieszkały przy ul.{' '}
        <chakra.span bg="brand.muted">Długiej 12</chakra.span>
        <Box as="span" display="inline-flex" verticalAlign="middle" color="fg.muted" ms="-1" me="0.5">
          <TextCursor size={16} />
        </Box>
        w Bydgoszczy
      </Text>
      <Flex
        display="inline-flex"
        align="center"
        gap="1.5"
        mt="1.5"
        px="2.5"
        py="1"
        borderRadius="l2"
        bg="brand.solid"
        color="brand.contrast"
        fontWeight="semibold"
        boxShadow="md"
      >
        <EyeOff size={14} /> Ukryj zaznaczony fragment
      </Flex>
    </Example>
  )
}

function InlineIcon({ icon: Glyph, label }: { icon: typeof Check; label: string }) {
  return (
    <Box as="span" display="inline-flex" verticalAlign="middle" borderWidth="1px" borderColor="border.emphasized" borderRadius="xs" p="0.5">
      <Glyph size={12} aria-label={label} role="img" />
    </Box>
  )
}

const steps: WalkthroughStep[] = [
  {
    id: 'suggestion',
    target: find('[data-walkthrough="document"] button[aria-pressed="false"]', '[data-walkthrough="document"] article'),
    placement: 'bottom',
    title: 'Kliknij szary fragment, aby go ukryć',
    description: (
      <>
        <ClickExample />
        <Text>
          Asystent zaznaczył na szaro dane, które mogą wymagać anonimizacji. Kliknięty fragment robi się czarny — zostanie
          ukryty. Kliknij go ponownie, aby cofnąć decyzję.
        </Text>
      </>
    ),
  },
  {
    id: 'selection',
    target: find('[data-walkthrough="hint"]'),
    placement: 'bottom',
    title: 'Ukryj tekst, którego asystent nie zaznaczył',
    description: (
      <>
        <SelectionExample />
        <Text>
          Zaznacz myszą fragment tekstu, przeciągając po nim kursorem. Pod zaznaczeniem pojawi się przycisk{' '}
          <strong>„Ukryj zaznaczony fragment”</strong> — kliknij go, a fragment zostanie ukryty. Ta wskazówka będzie stale
          widoczna nad dokumentem.
        </Text>
      </>
    ),
  },
  {
    id: 'panel',
    target: find('[data-walkthrough="panel"] li', '[data-walkthrough="panel"]'),
    placement: 'bottom',
    title: 'Zdecyduj o każdym fragmencie',
    description: (
      <Stack gap="2">
        <Text>
          Na liście zaznaczeń kliknij <InlineIcon icon={Check} label="Ukryj" />, aby ukryć fragment, albo{' '}
          <InlineIcon icon={X} label="Pozostaw jawne" />, aby pozostawić go jawnym.
        </Text>
        <Text>
          Nad listą znajdziesz przycisk <strong>„Ukryj wszystkie sugestie”</strong> oraz pole{' '}
          <strong>„Ukryj frazę w całym piśmie”</strong>, które ukrywa każde wystąpienie wpisanego tekstu — także bez użycia
          myszy.
        </Text>
      </Stack>
    ),
  },
  {
    id: 'preview',
    target: find('[data-walkthrough="view"]'),
    placement: 'bottom-end',
    title: 'Sprawdź efekt',
    description: (
      <Text>
        Widok <strong>„Podgląd po anonimizacji”</strong> pokazuje dokument tak, jak zobaczy go odbiorca — ukryte dane zastępuje
        napis [dane zanonimizowane].
      </Text>
    ),
  },
  {
    id: 'download',
    target: find('[data-walkthrough="download"]'),
    placement: 'bottom-end',
    title: 'Pobierz zanonimizowany dokument',
    description: (
      <Text>
        Gdy skończysz, pobierz dokument jako plik Word lub tekstowy. Fragmenty, które nadal czekają na decyzję, nie zostaną
        ukryte.
      </Text>
    ),
  },
]

export function AnonymizationWalkthrough({ onStart }: { onStart: () => void }) {
  return <Walkthrough steps={steps} storageKey="udip.walkthrough.anonymization" onStart={onStart} />
}
