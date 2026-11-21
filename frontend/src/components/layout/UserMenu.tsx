import { Avatar, Box, Button, Menu, Portal, Text } from '@chakra-ui/react'
import { ChevronDown, KeyRound, LogOut } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { notifyError } from '@/components/ui/toaster'
import { ChangePasswordDialog } from '@/features/auth/ChangePasswordDialog'
import { useCurrentUser, useLogout } from '@/features/auth/session'

export function UserMenu() {
  const user = useCurrentUser()
  const logout = useLogout()
  const navigate = useNavigate()
  const [changingPassword, setChangingPassword] = useState(false)

  const signOut = () =>
    logout.mutate(undefined, {
      onSuccess: () => navigate('/logowanie', { replace: true }),
      onError: (error) => notifyError(error, 'Nie udało się wylogować'),
    })

  return (
    <>
      <Menu.Root positioning={{ placement: 'bottom-end' }}>
        <Menu.Trigger asChild>
          <Button variant="ghost" size="sm" px="2" gap="2" aria-label={`Konto: ${user.fullName}`}>
            <Avatar.Root size="xs" colorPalette="brand" variant="solid">
              <Avatar.Fallback>{user.initials}</Avatar.Fallback>
            </Avatar.Root>
            <Box as="span" hideBelow="lg" fontWeight="semibold">
              {user.fullName}
            </Box>
            <ChevronDown size={16} aria-hidden />
          </Button>
        </Menu.Trigger>
        <Portal>
          <Menu.Positioner>
            <Menu.Content minW="56">
              <Box px="3" py="2">
                <Text fontWeight="semibold">{user.fullName}</Text>
                <Text fontSize="sm" color="fg.muted">
                  {user.userName} · {user.isAdmin ? 'administrator' : 'urzędnik'}
                </Text>
              </Box>
              <Menu.Separator />
              <Menu.Item value="password" onSelect={() => setChangingPassword(true)}>
                <KeyRound size={16} aria-hidden /> Zmień hasło
              </Menu.Item>
              <Menu.Item value="logout" onSelect={signOut}>
                <LogOut size={16} aria-hidden /> Wyloguj się
              </Menu.Item>
            </Menu.Content>
          </Menu.Positioner>
        </Portal>
      </Menu.Root>
      <ChangePasswordDialog open={changingPassword} onClose={() => setChangingPassword(false)} />
    </>
  )
}
