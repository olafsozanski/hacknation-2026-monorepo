import { Tour, useTour, type TourStepPlacement } from '@ark-ui/react/tour'
import { Box, Button, CloseButton, Flex, Portal, chakra } from '@chakra-ui/react'
import { CircleHelp } from 'lucide-react'
import { useEffect, useEffectEvent, type ReactNode } from 'react'

export type WalkthroughStep = {
  id: string
  title: string
  description: ReactNode
  target?: () => HTMLElement | null
  placement?: TourStepPlacement
}

type WalkthroughProps = {
  steps: WalkthroughStep[]
  storageKey: string
  onStart?: () => void
}

const layer = { '--tour-z-index': 'zIndex.modal', zIndex: 'calc(var(--tour-z-index) + var(--tour-layer))' }

const Backdrop = chakra(Tour.Backdrop, { base: { ...layer, bg: 'blackAlpha.600' } })

const Spotlight = chakra(Tour.Spotlight, {
  base: {
    ...layer,
    boxShadow: '0 0 0 3px var(--chakra-colors-focus-ring)',
    transitionProperty: 'width, height, left, top',
    transitionDuration: 'moderate',
  },
})

const Positioner = chakra(Tour.Positioner, {
  base: {
    '--tour-z-index': 'zIndex.modal',
    '&[data-type=dialog]': {
      position: 'fixed',
      inset: '0',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 'calc(var(--tour-z-index) + 2)',
      p: '4',
    },
  },
})

const Content = chakra(Tour.Content, {
  base: {
    position: 'relative',
    bg: 'bg.panel',
    color: 'fg',
    borderWidth: '1px',
    borderColor: 'border',
    borderRadius: 'l3',
    boxShadow: 'lg',
    p: '5',
    w: '24rem',
    maxW: 'calc(100vw - 2rem)',
    outline: 'none',
  },
})

const Arrow = chakra(Tour.Arrow, { base: { '--arrow-size': 'sizes.3', '--arrow-background': 'colors.bg.panel' } })

const ArrowTip = chakra(Tour.ArrowTip, { base: { borderTopWidth: '1px', borderInlineStartWidth: '1px', borderColor: 'border' } })

function isSeen(key: string) {
  try {
    return localStorage.getItem(key) === 'seen'
  } catch {
    return true
  }
}

function markSeen(key: string) {
  try {
    localStorage.setItem(key, 'seen')
  } catch {
    // Without storage the walkthrough starts again on the next visit, which is harmless.
  }
}

export function Walkthrough({ steps, storageKey, onStart }: WalkthroughProps) {
  const tour = useTour({
    steps: steps.map((step) => ({ ...step, type: step.target ? 'tooltip' : 'dialog' })),
    closeOnInteractOutside: false,
    spotlightRadius: 6,
    translations: {
      progressText: ({ current, total }) => `Krok ${current + 1} z ${total}`,
      close: 'Zamknij przewodnik',
    },
  })

  const start = () => {
    markSeen(storageKey)
    onStart?.()
    tour.start()
  }
  const startOnFirstVisit = useEffectEvent(() => {
    if (!isSeen(storageKey)) start()
  })

  useEffect(() => {
    const frame = requestAnimationFrame(startOnFirstVisit)
    return () => cancelAnimationFrame(frame)
  }, [])

  return (
    <>
      <Button variant="ghost" onClick={start}>
        <CircleHelp size={16} aria-hidden /> Jak to działa?
      </Button>
      <Tour.Root tour={tour}>
        <Portal>
          <Backdrop />
          <Spotlight />
          <Positioner>
            <Content>
              <Arrow>
                <ArrowTip />
              </Arrow>
              <Flex align="center" justify="space-between" mt="-1.5" mb="1">
                <Tour.ProgressText asChild>
                  <chakra.p fontSize="sm" fontWeight="semibold" color="fg.muted">
                    {tour.getProgressText()}
                  </chakra.p>
                </Tour.ProgressText>
                <Tour.CloseTrigger asChild>
                  <CloseButton size="sm" me="-2" />
                </Tour.CloseTrigger>
              </Flex>
              <Tour.Title asChild>
                <chakra.h2 fontSize="lg" fontWeight="bold" lineHeight="1.3" mb="2">
                  {tour.step?.title}
                </chakra.h2>
              </Tour.Title>
              <Tour.Description asChild>
                <Box fontSize="sm" lineHeight="1.6">
                  {tour.step?.description}
                </Box>
              </Tour.Description>
              <Flex gap="2" justify="flex-end" mt="5">
                {tour.hasPrevStep && (
                  <Tour.ActionTrigger action={{ label: 'Wstecz', action: 'prev' }} asChild>
                    <Button size="sm" variant="outline">
                      Wstecz
                    </Button>
                  </Tour.ActionTrigger>
                )}
                <Tour.ActionTrigger action={{ label: 'Dalej', action: tour.lastStep ? 'dismiss' : 'next' }} asChild>
                  <Button size="sm">{tour.lastStep ? 'Zakończ' : 'Dalej'}</Button>
                </Tour.ActionTrigger>
              </Flex>
            </Content>
          </Positioner>
        </Portal>
      </Tour.Root>
    </>
  )
}
