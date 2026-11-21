import { Box, Button, Flex, Heading, IconButton, Portal, Spinner, Tooltip } from '@chakra-ui/react'
import { Maximize2, MessageCirclePlus, MessageCircleQuestion, X } from 'lucide-react'
import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { assistantPath, useAssistant } from '../context'
import { assistantUiProps } from '../screenContext'
import { AssistantChat } from './AssistantChat'

function HeaderIconButton({ label, onClick, children }: { label: string; onClick: () => void; children: ReactNode }) {
  return (
    <Tooltip.Root openDelay={300} positioning={{ placement: 'top' }}>
      <Tooltip.Trigger asChild>
        <IconButton size="sm" variant="ghost" aria-label={label} onClick={onClick}>
          {children}
        </IconButton>
      </Tooltip.Trigger>
      <Portal>
        <Tooltip.Positioner>
          <Tooltip.Content>{label}</Tooltip.Content>
        </Tooltip.Positioner>
      </Portal>
    </Tooltip.Root>
  )
}

export function AssistantPopup() {
  const { open, setOpen, onAssistantPage, streaming, newConversation } = useAssistant()
  const navigate = useNavigate()
  const launcherRef = useRef<HTMLButtonElement>(null)
  const panelId = useId()
  const titleId = useId()
  if (onAssistantPage) return null

  const close = () => {
    setOpen(false)
    launcherRef.current?.focus()
  }

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key !== 'Escape') return
    event.stopPropagation()
    close()
  }

  return (
    <Box {...assistantUiProps}>
      <Button
        ref={launcherRef}
        position="fixed"
        zIndex="banner"
        right="calc(1rem + env(safe-area-inset-right))"
        bottom="calc(1rem + env(safe-area-inset-bottom))"
        borderRadius="full"
        boxShadow="lg"
        px={{ base: 3, sm: 4 }}
        aria-label="Asystent"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen(!open)}
      >
        {streaming ? <Spinner size="sm" color="brand.contrast" /> : <MessageCircleQuestion size={20} aria-hidden />}
        <Box as="span" hideBelow="sm">
          Asystent
        </Box>
      </Button>
      <Box
        id={panelId}
        role="dialog"
        aria-modal={false}
        aria-labelledby={titleId}
        hidden={!open}
        display={open ? 'flex' : 'none'}
        flexDirection="column"
        position="fixed"
        zIndex="banner"
        left={{ base: 0, md: 'auto' }}
        right={{ base: 0, md: 'calc(1rem + env(safe-area-inset-right))' }}
        bottom={{ base: 0, md: 'calc(4.75rem + env(safe-area-inset-bottom))' }}
        w={{ base: 'full', md: '25rem' }}
        maxW={{ md: 'calc(100vw - 2rem)' }}
        h={{ base: 'min(85dvh, calc(100dvh - 4rem))', md: '37.5rem' }}
        maxH={{ md: 'calc(100dvh - 10rem)' }}
        pb={{ base: 'env(safe-area-inset-bottom)', md: '0' }}
        bg="bg.panel"
        borderWidth="1px"
        borderColor="border.emphasized"
        borderTopRadius="lg"
        borderBottomRadius={{ base: '0', md: 'lg' }}
        boxShadow="xl"
        overflow="hidden"
        animationName="fade-in"
        animationDuration="fast"
        onKeyDown={onKeyDown}
      >
        {open && (
          <>
            <Flex align="center" gap="1" ps="4" pe="2" py="2" borderBottomWidth="1px" borderColor="border">
              <Heading as="h2" id={titleId} size="md" flex="1" minW="0">
                Asystent
              </Heading>
              <Button size="sm" variant="ghost" onClick={newConversation}>
                <MessageCirclePlus size={16} aria-hidden /> Nowa rozmowa
              </Button>
              <HeaderIconButton label="Otwórz na pełnej stronie" onClick={() => navigate(assistantPath)}>
                <Maximize2 aria-hidden />
              </HeaderIconButton>
              <HeaderIconButton label="Zamknij asystenta" onClick={close}>
                <X aria-hidden />
              </HeaderIconButton>
            </Flex>
            <Box flex="1" minH="0">
              <AssistantChat autoFocus />
            </Box>
          </>
        )}
      </Box>
    </Box>
  )
}
