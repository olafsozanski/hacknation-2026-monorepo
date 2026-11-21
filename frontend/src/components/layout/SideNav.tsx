import { Box, Button, Stack, Text } from '@chakra-ui/react'
import {
  Archive,
  BookOpen,
  Building2,
  FilePlus2,
  Inbox,
  MessageCircleQuestion,
  ScrollText,
  ShieldCheck,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { Link as RouterLink, NavLink } from 'react-router'
import { useCurrentUser } from '@/features/auth/session'

type Item = { to: string; label: string; icon: LucideIcon; end?: boolean }

const mainItems: Item[] = [
  { to: '/', label: 'Rejestr wniosków', icon: Inbox, end: true },
  { to: '/archiwum', label: 'Archiwum spraw', icon: Archive },
  { to: '/anonimizacja', label: 'Anonimizacja', icon: ShieldCheck },
  { to: '/baza-wiedzy', label: 'Baza wiedzy', icon: BookOpen },
  { to: '/wydzialy', label: 'Wydziały i biura', icon: Building2 },
  { to: '/asystent', label: 'Asystent', icon: MessageCircleQuestion },
]

const adminItems: Item[] = [
  { to: '/administracja/uzytkownicy', label: 'Użytkownicy', icon: Users },
  { to: '/administracja/dziennik', label: 'Dziennik zdarzeń', icon: ScrollText },
]

function NavItem({ item, onNavigate }: { item: Item; onNavigate?: () => void }) {
  const Icon = item.icon
  return (
    <Box as="li" listStyleType="none">
      <NavLink to={item.to} end={item.end} onClick={onNavigate} style={{ display: 'block' }}>
        {({ isActive }) => (
          <Box
            display="flex"
            alignItems="center"
            gap="3"
            px="4"
            py="2.5"
            borderRadius="0 var(--chakra-radii-sm) var(--chakra-radii-sm) 0"
            borderLeftWidth="4px"
            borderLeftColor={isActive ? 'brand.solid' : 'transparent'}
            fontWeight={isActive ? 'bold' : 'medium'}
            bg={isActive ? 'brand.subtle' : 'transparent'}
            color={isActive ? 'brand.fg' : 'fg'}
            _hover={{ bg: isActive ? 'brand.subtle' : 'bg.muted', color: 'brand.fg' }}
            transition="background 0.15s, color 0.15s"
          >
            <Icon size={18} aria-hidden />
            {item.label}
          </Box>
        )}
      </NavLink>
    </Box>
  )
}

export function SideNav({ onNavigate }: { onNavigate?: () => void }) {
  const user = useCurrentUser()
  return (
    <Stack gap="6" p="4">
      <Button asChild w="full">
        <RouterLink to="/wnioski/nowy" onClick={onNavigate}>
          <FilePlus2 size={18} aria-hidden /> Zarejestruj wniosek
        </RouterLink>
      </Button>
      <nav aria-label="Menu główne">
        <Stack as="ul" gap="1" m="0" p="0">
          {mainItems.map((item) => (
            <NavItem key={item.to} item={item} onNavigate={onNavigate} />
          ))}
        </Stack>
      </nav>
      {user.isAdmin && (
        <nav aria-label="Administracja">
          <Text px="4" mb="2" fontSize="xs" fontWeight="bold" color="fg.muted" textTransform="uppercase" letterSpacing="wider">
            Administracja
          </Text>
          <Stack as="ul" gap="1" m="0" p="0">
            {adminItems.map((item) => (
              <NavItem key={item.to} item={item} onNavigate={onNavigate} />
            ))}
          </Stack>
        </nav>
      )}
    </Stack>
  )
}
