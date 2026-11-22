import { Box, Flex, IconButton, Spacer, Text } from '@chakra-ui/react'
import { Menu } from 'lucide-react'
import { Link as RouterLink } from 'react-router'
import { AppMark } from '@/components/common/AppMark'
import { useSystemInfo } from '@/features/platform/api'
import { A11yControls } from './A11yControls'
import { AiModeBadge } from './AiModeBadge'
import { UserMenu } from './UserMenu'

export function AppHeader({ onOpenMenu }: { onOpenMenu: () => void }) {
  const { data: info } = useSystemInfo()
  return (
    <Box as="header" bg="bg" borderBottomWidth="1px" borderColor="border" position="sticky" top="0" zIndex="sticky">
      <Flex h="16" px={{ base: 3, md: 6 }} align="center" gap={{ base: 2, md: 4 }}>
        <IconButton variant="ghost" aria-label="Otwórz menu" hideFrom="lg" onClick={onOpenMenu}>
          <Menu aria-hidden />
        </IconButton>
        <RouterLink to="/" aria-label="SOIP — System Obsługi Informacji Publicznej – strona główna">
          <Flex align="center" gap="3">
            <AppMark boxSize="9" />
            <Text fontWeight="bold" fontSize="lg" lineHeight="1.1" whiteSpace="nowrap">
              SOIP
            </Text>
          </Flex>
        </RouterLink>
        <Box w="2px" h="9" bg="accent.red" hideBelow="md" aria-hidden />
        {info && (
          <Box hideBelow="md" lineHeight="1.25" minW="0">
            <Text fontSize="sm" fontWeight="semibold" truncate>
              {info.organizationName}
            </Text>
            <Text fontSize="xs" color="fg.muted" truncate>
              {info.unitName}
            </Text>
          </Box>
        )}
        <Spacer />
        <AiModeBadge />
        <Box hideBelow="sm">
          <A11yControls />
        </Box>
        <UserMenu />
      </Flex>
    </Box>
  )
}
