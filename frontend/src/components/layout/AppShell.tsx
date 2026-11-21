import { Box, CloseButton, Drawer, Flex, Link, Portal, Text } from '@chakra-ui/react'
import { useEffect, useRef, useState } from 'react'
import { Outlet, Link as RouterLink, useLocation } from 'react-router'
import { AssistantProvider } from '@/features/assistant/AssistantProvider'
import { AssistantPopup } from '@/features/assistant/components/AssistantPopup'
import { A11yControls } from './A11yControls'
import { AppHeader } from './AppHeader'
import { SideNav } from './SideNav'

function useFocusHeadingOnNavigation() {
  const { pathname } = useLocation()
  const previous = useRef(pathname)
  useEffect(() => {
    if (previous.current === pathname) return
    previous.current = pathname
    const timer = window.setTimeout(() => {
      const heading = document.querySelector<HTMLElement>('#main-content h1')
      ;(heading ?? document.getElementById('main-content'))?.focus()
    }, 50)
    return () => window.clearTimeout(timer)
  }, [pathname])
}

export function AppShell() {
  const [menuOpen, setMenuOpen] = useState(false)
  useFocusHeadingOnNavigation()

  return (
    <AssistantProvider>
      <Flex direction="column" minH="100dvh">
        <Link
          href="#main-content"
          onClick={(event) => {
            event.preventDefault()
            const main = document.getElementById('main-content')
            main?.focus()
            main?.scrollIntoView()
          }}
          position="absolute"
          left="4"
          top="-20"
          zIndex="skipNav"
          bg="brand.solid"
          color="brand.contrast"
          px="4"
          py="2"
          borderRadius="sm"
          fontWeight="semibold"
          _focus={{ top: '3' }}
        >
          Przejdź do treści głównej
        </Link>
        <AppHeader onOpenMenu={() => setMenuOpen(true)} />
        <Flex flex="1" align="stretch">
          <Box
            as="aside"
            w="64"
            flexShrink={0}
            bg="bg"
            borderRightWidth="1px"
            borderColor="border"
            hideBelow="lg"
            position="sticky"
            top="16"
            alignSelf="flex-start"
            h="calc(100dvh - var(--chakra-sizes-16))"
            overflowY="auto"
          >
            <SideNav />
          </Box>
          <Flex direction="column" flex="1" minW="0">
            <Box
              as="main"
              id="main-content"
              tabIndex={-1}
              outline="none"
              flex="1"
              w="full"
              maxW="1480px"
              mx="auto"
              px={{ base: 4, md: 8 }}
              py={{ base: 5, md: 7 }}
            >
              <Outlet />
            </Box>
            <Box
              as="footer"
              borderTopWidth="1px"
              borderColor="border"
              ps={{ base: 4, md: 8 }}
              pe={{ base: 4, md: 44 }}
              pt="4"
              pb={{ base: 20, md: 4 }}
              bg="bg"
            >
              <Flex gap="4" wrap="wrap" justify="space-between" fontSize="sm" color="fg.muted">
                <Text>
                  Asystent UDIP jest narzędziem pomocniczym. Program niczego nie wysyła samodzielnie — decyzję zawsze
                  podejmuje urzędnik.
                </Text>
                <Link asChild color="brand.fg" textDecoration="underline">
                  <RouterLink to="/dostepnosc">Deklaracja dostępności</RouterLink>
                </Link>
              </Flex>
            </Box>
          </Flex>
        </Flex>

        <Drawer.Root open={menuOpen} onOpenChange={(e) => setMenuOpen(e.open)} placement="start">
          <Portal>
            <Drawer.Backdrop />
            <Drawer.Positioner>
              <Drawer.Content maxW="80">
                <Drawer.Header borderBottomWidth="1px" borderColor="border">
                  <Drawer.Title>Menu</Drawer.Title>
                </Drawer.Header>
                <Drawer.Body p="0">
                  <SideNav onNavigate={() => setMenuOpen(false)} />
                  <Box px="4" pb="6" hideFrom="sm">
                    <A11yControls />
                  </Box>
                </Drawer.Body>
                <Drawer.CloseTrigger asChild>
                  <CloseButton aria-label="Zamknij menu" />
                </Drawer.CloseTrigger>
              </Drawer.Content>
            </Drawer.Positioner>
          </Portal>
        </Drawer.Root>
        <AssistantPopup />
      </Flex>
    </AssistantProvider>
  )
}
